namespace ReConstruct.Services

open System

open ReConstruct.Core
open ReConstruct.Core.Numeric
open ReConstruct.Core.IO

open ReConstruct.Data.Dicom
open ReConstruct.Data.Dicom.Utils

module internal DicomParser =
    [<Literal>]
    let STANDARD_PREAMBLE = "DICM"

    let readExplicitVR reader = 
        Tags.VR_FIELD_SIZE |> reader.readBytes |> VRTypes.Dictionary.TryFind |> Option.defaultValue UNKNOWN

    let getVRType reader (group, element, vrEncoding) =
        let _, vrType = Tags.tagInfo(group, element)
        let isEvenGroup, isPrivateCode = even group, element <= 0xFFus

        match vrType, vrEncoding, isEvenGroup, isPrivateCode with
        | DELIMITER, _, _, _            -> vrType
        | _, VREncoding.Explicit, _, _  -> reader |> readExplicitVR
        | _, _, true, _                 -> vrType
        | _, _, false, true             -> LONG_STRING
        | _, _, false, false            -> UNKNOWN        

    let asU16 = (INT16_SIZE, toUInt16)
    let asU32 = (INT32_SIZE, toUInt32)
    let asI16 = (INT16_SIZE, toInt16)
    let asI32 = (INT32_SIZE, toInt32)
    let asF32 = (FLOAT32_SIZE, toFloat)
    let asD64 = (DOUBLE64_SIZE, toDouble)

    let readNumber (size, convert) reader = size |> reader |> convert

    let readU16 = readNumber asU16
    let readU32 = readNumber asU32 
    let readI16 = readNumber asI16
    let readI32 = readNumber asI32
    let readF32 = readNumber asF32
    let readD64 = readNumber asD64
    let readU16x2 reader = (readU16 reader, readU16 reader)

    let parseSyntax =
        function
        | TransferSyntax.ExplicitLE -> { VREncoding = VREncoding.Explicit; EndianEncoding = EndianEncoding.LittleEndian; }
        | TransferSyntax.ExplicitBE -> { VREncoding = VREncoding.Explicit; EndianEncoding = EndianEncoding.BigEndian; }
        | TransferSyntax.ImplicitLE -> { VREncoding = VREncoding.Implicit; EndianEncoding = EndianEncoding.LittleEndian; }
        | _                         -> { VREncoding = VREncoding.Explicit; EndianEncoding = EndianEncoding.LittleEndian; }
    
    let getNextTag reader syntax =

        // First pass to get get transfer syntax based on lookup of group number.
        // Then rewind and start reading this time using the specified encoding.
        let groupNumberLookup = reader.readAndRewind |> readU16

        let transferSyntax = match groupNumberLookup with
                             | 0x0002us -> { VREncoding = VREncoding.Explicit; EndianEncoding = EndianEncoding.LittleEndian; }
                             | _        -> syntax |> parseSyntax

        let readEndian = transferSyntax.EndianEncoding |> reader.endianReader

        let group, element = readEndian |> readU16x2

        let inline lengthAndPosition length = 
            { 
                ValueLength = int64 length;
                StreamPosition = reader.position();
            }

        let readVREncodingLength = 
            match transferSyntax.VREncoding with
            | VREncoding.Explicit -> fun _ -> readEndian |> readI16 |> lengthAndPosition
            | _                   -> fun _ -> readEndian |> readI32 |> lengthAndPosition

        let skipReserved read =
            if transferSyntax.VREncoding = VREncoding.Explicit then
                2L |> reader.forward
            read

        let readNumberSeries format length =
            let size = format |> fst |> int64
            let readOne() = readEndian |> readNumber format
            seq { 1L..size..length } |> Seq.fold(fun (value, vm) _ -> (value + readOne().ToString() + " ", vm + 1L)) ("", 0L)

        let vr = getVRType reader (group, element, transferSyntax.VREncoding)

        let basicTag marker value =
            {
                Tag = (group, element);
                TransferSyntax = transferSyntax;
                VR = vr;                
                VM = None;
                Marker = marker;
                ValueField = value;
            }

        let textTag marker = marker.ValueLength |> int32 |> reader.readString |> basicTag marker

        let vmTag marker value vm =
            {
                Tag = (group, element);
                TransferSyntax = transferSyntax;
                VR = vr;                
                VM = vm |> Some;
                Marker = marker;
                ValueField = value;
            }

        let numberTag parseInfo marker = 
            let valueField, vm = marker.ValueLength |> readNumberSeries parseInfo

            {
                Tag = (group, element);
                TransferSyntax = transferSyntax;
                Marker = marker;
                VR = vr;
                ValueField = valueField;
                VM = vm |> Some;
            }

        let ignoreTag length = 
            let marker = length |> lengthAndPosition 
            basicTag marker String.Empty

        match vr with
        | OTHER_BYTE | OTHER_FLOAT | OTHER_WORD | UNKNOWN ->
            let marker = readEndian |> skipReserved |> readI32 |> lengthAndPosition

            let valueField = match marker.ValueLength > 10L with
                             | true -> (10 |> reader.readHex) + "..."
                             | false -> marker.ValueLength |> int32 |> reader.readHex

            reader.goTo (marker.StreamPosition + marker.ValueLength)

            valueField.Trim() |> basicTag marker

        | UNLIMITED_TEXT -> readEndian |> skipReserved |> readI32 |> lengthAndPosition |> textTag

        | SEQUENCE_OF_ITEMS -> readEndian |> skipReserved |> readI32 |> ignoreTag

        | APPLICATION_ENTITY | AGE_STRING | CODE_STRING | DATE | DATE_TIME 
        | LONG_TEXT | PERSON_NAME | SHORT_STRING | SHORT_TEXT | TIME | UID -> readVREncodingLength() |> textTag

        | DECIMAL_STRING | INTEGER_STRING | LONG_STRING ->
            let marker = readVREncodingLength()
            let valueField = marker.ValueLength |> int32 |> reader.readString
            valueField.Split([| '\\' |]) |> Array.length |> int64 |> vmTag marker valueField

        | ATTRIBUTE -> // 2 bytes value length, 4 bytes value.
            let marker = readVREncodingLength()
            let tag = readEndian |> readU16x2

            {
                Tag = tag;
                TransferSyntax = transferSyntax;
                Marker = marker;
                VR = vr;
                ValueField = String.Empty;
                VM = None;
            }

        | UNSIGNED_LONG -> readVREncodingLength() |> numberTag asU32

        | UNSIGNED_SHORT -> readVREncodingLength() |> numberTag asU16

        | SIGNED_LONG -> readVREncodingLength() |> numberTag asI32

        | SIGNED_SHORT -> readVREncodingLength() |> numberTag asI16

        | FLOAT -> readVREncodingLength() |> numberTag asF32

        | DOUBLE -> readVREncodingLength() |> numberTag asD64            

        | DELIMITER -> readEndian |> readI32 |> ignoreTag // (FFFE,E000) Item | (FFFE,E00D) Item Delimitation Item | (FFFE,E0DD) Sequence Delimitation Item

    let rec parseDataSet reader parent syntax limitPosition =
        let tag = getNextTag reader syntax
        let newNode = DicomTree.newNode tag
        parent.Children.[tag.Tag] <- newNode

        if reader.position() < limitPosition && tag.Tag <> Tags.SequenceDelimiter then
            let newSyntax = match tag.Tag = Tags.TransferSyntaxUID with
                            | true -> tag.ValueField
                            | false -> syntax

            let parseChild = parseDataSet reader newNode newSyntax

            match tag.VR, tag.Marker.ValueLength with
            | SEQUENCE_OF_ITEMS, -1L -> reader.length() |> parseChild
            | SEQUENCE_OF_ITEMS, _   -> (reader.position() + tag.Marker.ValueLength) |> parseChild
            | _, _                   -> ()

            parseDataSet reader parent newSyntax limitPosition

    let getDicomTree buffer =

        // Dicom file header,
        // - Fixed preamble not to be used: 128 bytes.
        // - DICOM Prefix "DICM": 4 bytes.
        // - File Meta Information: sequence of FileMetaAttribute.
        //   FileMetaAttribute structure: (0002,xxxx), encoded with ExplicitVRLittleEndian Transfer Syntax.
        let preambleLength, dicmMarkLength = 128L, 4

        use reader =  buffer |> memoryStream |> binaryReader |> Utils.DicomReader.New

        reader.goTo preambleLength
    
        let dicmMark = dicmMarkLength |> reader.readString
        if dicmMark <> STANDARD_PREAMBLE then
            reader.goTo 0L

        let root = 
            {
                Tag = (0us, 0us);
                VR = VRType.UNKNOWN;
                TransferSyntax = { VREncoding = VREncoding.Explicit; EndianEncoding = EndianEncoding.LittleEndian; };
                VM = None;                
                ValueField = "???";
                Marker = {
                    ValueLength = 0L;
                    StreamPosition = 0L;
                };                
            } |> DicomTree.newNode

        parseDataSet reader root "???" (reader.length())

        root
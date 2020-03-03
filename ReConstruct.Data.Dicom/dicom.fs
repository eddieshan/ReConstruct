namespace ReConstruct.Data.Dicom

open System
open System.Collections.Generic

open System.Windows.Media.Imaging

module BaseTypes =

    type VREncoding =
        | Explicit
        | Implicit

    type EndianEncoding =
        | LittleEndian
        | BigEndian

    type TransferSyntaxType =
        {
            VREncoding: VREncoding;
            EndianEncoding: EndianEncoding;
        }

    type VRType =
        | OTHER_BYTE
        | OTHER_FLOAT
        | OTHER_WORD
        | UNKNOWN
        | UNLIMITED_TEXT
        | SEQUENCE_OF_ITEMS
        | APPLICATION_ENTITY
        | AGE_STRING
        | CODE_STRING
        | DATE
        | DATE_TIME
        | LONG_TEXT
        | PERSON_NAME
        | SHORT_STRING
        | SHORT_TEXT
        | TIME
        | DECIMAL_STRING
        | INTEGER_STRING
        | LONG_STRING
        | UID
        | ATTRIBUTE
        | UNSIGNED_LONG
        | UNSIGNED_SHORT
        | SIGNED_LONG
        | SIGNED_SHORT
        | FLOAT
        | DOUBLE
        | DELIMITER

type TagMarker =
    {
        ValueLength: int64;
        StreamPosition: int64;
    }

// Dicom data element. Contains parsed dicom tag data.
type DicomDataElement =
    {
        Tag: uint16*uint16;
        TransferSyntax: BaseTypes.TransferSyntaxType;
        VR: BaseTypes.VRType;
        VM: int64 option;
        Marker: TagMarker;
        ValueField: string;
    }

type DicomTree = 
    {
        Tag: DicomDataElement;
        Children: IDictionary<uint16*uint16, DicomTree>;
    }

module Utils =
    open System.Globalization

    let parseFloat (s: string) =
        let items = s.Split([| '\\' |])
        Convert.ToDouble(items.[items.Length - 1], CultureInfo.InvariantCulture)


module DicomTree =

    let newNode tag = { Tag = tag; Children = new Dictionary<uint16*uint16, DicomTree>(); }

    let rec findNode (node: DicomTree) key =
        if node.Tag.Tag = key then
            node.Tag |> Some
        else if node.Children.Values |> Seq.isEmpty then
            None
        else
            match node.Children.TryGetValue key with
            | true, childNode -> childNode.Tag |> Some
            | false, _ -> node.Children.Values |> Seq.tryFind(fun v -> key |> findNode v |> Option.isSome) |> Option.map(fun n -> n.Tag)

    let findTagValue root key = key |> findNode root |> Option.map(fun v -> v.ValueField)
    let findTagValueAsNumber root f key = key |> findNode root |> Option.map(fun v -> v.ValueField |> Utils.parseFloat |> f)

type PixelSpacing =
    {
        X: double;
        Y: double;
    }

type SliceDimensions =
    {
        Columns: int;
        Rows: int;
    }

// Encapsulate CAT slice geometry params.
type SliceParams =
    {
        Dimensions: SliceDimensions;
        UpperLeft: double[];
        PixelSpacing: PixelSpacing;
        WindowCenter: int;
        WindowWidth: int;
    } 
    member x.AdjustToCenter (cx, cy, cz) =
        x.UpperLeft.[0] <- x.UpperLeft.[0] - cx
        x.UpperLeft.[1] <- x.UpperLeft.[1] - cy
        x.UpperLeft.[2] <- x.UpperLeft.[2] - cz

// CAT slice.
type CatSlice =
    {
        SliceParams: SliceParams;
        HounsfieldBuffer: int[,];
        GetBitmap: unit -> BitmapSource;
    }

// A Dicom instance.
type DicomInstance =
    {
        DicomTree: DicomTree; // Tree of Dicom tags represented by an XML document.
        FileName: string;
        StudyInstanceUID: string;
        SeriesInstanceUID: string;
        SOPInstanceUID: string;
        SOPClassUID: string;
        SOPClassName: string;
        PatientName: string;
        TransferSyntaxUID: string;
        SortOrder: double;
        CatSlice: CatSlice option; // CAT slice image, whenever there is one.
    }

// A dataset contains analysis metadata and an array of Iods.
type DatasetEntry =
    {
        Id: int;
        Name: string;
        SopClass: string;
        Study: string;
        Series: string;
        HasPixelData: bool;
        Iods: DicomInstance[];
    }
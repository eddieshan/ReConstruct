namespace ReConstruct.Data.Dicom

open ReConstruct.Data.Dicom.BaseTypes

module VRTypes =
    // VRTypes codes must be only two chars long. A tuple char*char is used as key to prevent malformed codes.
    let Dictionary = [|
                            (('O', 'B') (*"OB"*), OTHER_BYTE) //Other Byte String
                            (('O', 'F') (*"OF"*), OTHER_FLOAT) //Other Float String
                            (('O', 'W') (*"OW"*), OTHER_WORD) //Other Word String
                            (('U', 'N') (*"UN"*), UNKNOWN) //Unknown content
                            (('U', 'T') (*"UT"*), UNLIMITED_TEXT) //Unlimited Text
                            (('S', 'Q') (*"SQ"*), SEQUENCE_OF_ITEMS) //Sequence of Items 
                            (('A', 'E') (*"AE"*), APPLICATION_ENTITY) //Application Entity
                            (('A', 'S') (*"AS"*), AGE_STRING) //Age String
                            (('C', 'S') (*"CS"*), CODE_STRING) //Code String
                            (('D', 'A') (*"DA"*), DATE) //Date
                            (('D', 'T') (*"DT"*), DATE_TIME) //Date Time
                            (('L', 'T') (*"LT"*), LONG_TEXT) //Long Text
                            (('P', 'N') (*"PN"*), PERSON_NAME) //Person Name
                            (('S', 'H') (*"SH"*), SHORT_STRING) //Short String
                            (('S', 'T') (*"ST"*), SHORT_TEXT) //Short Text
                            (('T', 'M') (*"TM"*), TIME) //Time
                            (('D', 'S') (*"DS"*), DECIMAL_STRING) //Decimal String
                            (('I', 'S') (*"IS"*), INTEGER_STRING) //Integer String
                            (('L', 'O') (*"LO"*), LONG_STRING)  // Long String
                            (('U', 'I') (*"UI"*), UID)  // Unique Identifier (UID)
                            (('A', 'T') (*"AT"*), ATTRIBUTE) // Attribute Tag
                            (('U', 'L') (*"UL"*), UNSIGNED_LONG)  // Unsigned Long (32 Bit, 4 Bytes)
                            (('U', 'S') (*"US"*), UNSIGNED_SHORT)  // Unsigned Short
                            (('S', 'L') (*"SL"*), SIGNED_LONG)  // Signed long (32 Bit, 4 Bytes)
                            (('S', 'S') (*"SS"*), SIGNED_SHORT)  // Signed short (16 Bit, 2 Bytes)
                            (('F', 'L') (*"FL"*), FLOAT)  // Floating Point Single (32 Bit, 4 Byte)
                            (('F', 'D') (*"FD"*), DOUBLE)  // Floating Point Double (64 Bit, 8 Byte)
                            (('D', 'L') (*"DL"*), DELIMITER)  // Special SQ related Data Elements Items:
                       |]
                       |> Seq.map (fun (code, value) -> ([| code |> fst |> byte; code |> snd |> byte |], value))
                       |> Map.ofSeq
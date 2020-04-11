namespace ReConstruct.Data.Dicom

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
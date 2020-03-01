namespace ReConstruct.UI.Core.Actions

open ReConstruct.Core.Types

type FileAction = 
    | Open

type DicomAction =
    | DatasetEntry of int
    | LoadIod of int*int
    | LoadVolume of int*float
    | LoadSlices of int
    | LoadTags of int

type AppAction =
    | File of FileAction
    | Dicom of DicomAction
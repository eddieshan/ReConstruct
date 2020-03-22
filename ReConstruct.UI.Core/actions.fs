namespace ReConstruct.UI.Core.Actions

open ReConstruct.Core.Types

type FileAction = 
    | Open

type DicomAction =
    | DatasetEntry of int
    | LoadIod of int*int
    | LoadVolume of int*float32
    | LoadSlices of int
    | LoadTags of int

type ToolAction = 
    | OpenTransformPanel
    //| OpenGeometryPanel

type AppAction =
    | File of FileAction
    | Dicom of DicomAction
    | Tool of ToolAction
﻿namespace ReConstruct.UI.Core.Actions

open ReConstruct.Core.Types

type FileAction = 
    | Open

type DicomAction =
    | DatasetEntry of int
    | LoadIod of int*int
    | LoadVolume of int*int
    | LoadSlices of int
    | LoadTags of int

type ToolAction = 
    | OpenTransformPanel
    | OpenScalarFieldPanel of int

type AppAction =
    | File of FileAction
    | Dicom of DicomAction
    | Tool of ToolAction
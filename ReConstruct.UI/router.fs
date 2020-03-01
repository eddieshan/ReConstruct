namespace ReConstruct.UI

open ReConstruct.UI.Core.Actions
open ReConstruct.UI.Controllers

module Router =
    let handle = function
                 | File(a) -> a |> FileController.handle
                 | Dicom(a) -> a |> DicomController.handle

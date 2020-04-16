namespace ReConstruct.UI.View

open ReConstruct.Data.Dicom

module AppState =
    let mutable Level: Option<int> = Some Imaging.BONES_ISOVALUE

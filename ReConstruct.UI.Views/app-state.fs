namespace ReConstruct.UI.View

open ReConstruct.Data.Dicom

module AppState =
    let mutable Level: Option<int16> = Some Imaging.BONES_ISOVALUE

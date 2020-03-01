namespace ReConstruct.UI.Controllers

open ReConstruct.Core.Async

open ReConstruct.Services

open ReConstruct.UI.Core
open ReConstruct.UI.Core.Actions
open ReConstruct.UI.View

module DicomController =

    let handle = function
                 | DatasetEntry id -> 
                    let view, loadContent = DatasetMainView.New()

                    // Background processing on the controller side. UI is updated with a continuation.
                    async { return id |> DicomService.getDataset } 
                    |> onThreadPool
                    |> doBusyAsync loadContent
                    view |> Mvc.basicView

                 | LoadVolume (id, isoValue) -> id |> DicomService.getVolume |> VolumeView.New isoValue |> Mvc.basicView
                 //| LoadVolume (id, isoValue) -> id |> DicomService.getVolume |> VolumeViewOpenGL.New isoValue
                 | LoadSlices id -> id |> DicomService.getIods |> SlicesView.New |> Mvc.basicView
                 | LoadIod (id, index) -> (id, index) |>  DicomService.getIod |> IodView.New |> Mvc.basicView
                 | LoadTags id -> id |> DicomService.getTags |> TagsView.New |> Mvc.basicView
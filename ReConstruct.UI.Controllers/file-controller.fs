namespace ReConstruct.UI.Controllers

open ReConstruct.UI.Core
open ReConstruct.UI.Core.Actions
open ReConstruct.UI.View

open ReConstruct.Services

module FileController =

    let handle = function
                 | Open -> DicomService.availableAnalysis() |> DatasetsView.New |> Mvc.basicView

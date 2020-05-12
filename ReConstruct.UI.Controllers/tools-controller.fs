namespace ReConstruct.UI.Controllers

open ReConstruct.Services

open ReConstruct.UI.Core
open ReConstruct.UI.Core.Actions
open ReConstruct.UI.View

module ToolsController =

    let handle = function
                 | OpenTransformPanel -> TransformView.New() |> Mvc.floatingView "Transform tools"
                 | OpenLightingPanel -> LightingView.New() |> Mvc.floatingView "Scene lighting"
                 | OpenScalarFieldPanel id -> id |> DicomService.getVolume |> ScalarFieldView.New |> Mvc.floatingView "Field levels"
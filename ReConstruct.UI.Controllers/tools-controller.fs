namespace ReConstruct.UI.Controllers

open ReConstruct.UI.Core
open ReConstruct.UI.Core.Actions
open ReConstruct.UI.View

module ToolsController =

    let handle = function
                 | OpenTransformPanel -> TransformView.New() |> Mvc.floatingView                    
                 | OpenScalarFieldPanel -> ScalarFieldView.New() |> Mvc.floatingView
                    
                    
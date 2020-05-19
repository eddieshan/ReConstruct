namespace ReConstruct.UI.View

open System
open System.Windows
open System.Windows.Media
open System.Windows.Controls.Primitives

open ReConstruct.UI.Controls
open ReConstruct.UI.Core.UI

open ReConstruct.Render

module LightingView = 

    let New() =
        let labels = seq { "Ambient"; "Diffuse"; "Specular"; "Reflectivity" }
        let colors = seq { Colors.White; Colors.White; Colors.White }

        seq {
            yield! labels |> Seq.map(fun l -> l |> label "panel-block-caption" :> UIElement)
            yield! colors |> Seq.map(fun color -> color |> ColorPicker.create :> UIElement)
            yield (0.1f, Scene.getReflectivity()) |> Spinner.create Scene.setReflectivity Single.Parse :> UIElement
        } |> childrenOf (UniformGrid(Style = style "lighting-view"))
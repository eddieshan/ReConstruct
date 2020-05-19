namespace ReConstruct.UI.View

open System
open System.Windows
open System.Windows.Controls.Primitives

open ReConstruct.UI.Controls
open ReConstruct.UI.Core.UI

open ReConstruct.Render

module LightingView = 

    let New() =
        let labels = seq { "Ambient"; "Diffuse"; "Specular"; "Reflectivity" }
        let components = seq { 
            (Scene.getAmbient, Scene.setAmbient)
            (Scene.getDiffuse, Scene.setDiffuse) 
            (Scene.getSpecular, Scene.setSpecular) 
        }

        seq {
            yield! labels |> Seq.map(fun l -> l |> label "panel-block-caption" :> UIElement)
            yield! components |> Seq.map(fun (get, set) -> get() |> Vector3Picker.create set :> UIElement)
            yield (0.1f, Scene.getReflectivity()) |> Spinner.create Scene.setReflectivity Single.Parse :> UIElement
        } |> childrenOf (UniformGrid(Style = style "lighting-view"))
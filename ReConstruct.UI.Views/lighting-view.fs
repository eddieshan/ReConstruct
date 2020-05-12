namespace ReConstruct.UI.View

open System
open System.Drawing
open System.Windows

open ReConstruct.UI.Controls
open ReConstruct.UI.Core.UI

open ReConstruct.Render

module LightingView = 

    let New() =
        //let rotate v = fun _ -> v |> Events.OnRotation.Trigger
        //let moveCameraZ v = fun _ -> v |> Events.OnCameraMoved.Trigger
        //let scale v = fun _ -> v |> Events.OnScale.Trigger
        //let delta, zoomFactor, scaleFactor = 0.1f, 0.05f, 0.05f
        //Events.VolumeTransformed.Publish |> Event.add updateTransform

        let block caption color =
            seq {
                yield caption |> label "panel-block-caption" :> UIElement
                yield color |> ColorPicker.create :> UIElement
            } |> childrenOf (stack "panel-block")

        seq {
            yield block "Ambient" Color.White
            yield block "Diffuse" Color.White
            yield block "Specular" Color.White
        } |> childrenOf (stack "transform-view")
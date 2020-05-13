namespace ReConstruct.UI.View

open System
open System.Windows
open System.Windows.Media

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
            yield block "Ambient" Colors.White
            yield block "Diffuse" Colors.White
            yield block "Specular" Colors.White
        } |> childrenOf (stack "transform-view")
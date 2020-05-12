namespace ReConstruct.UI.View

open System
open System.Windows

open ReConstruct.UI.Assets
open ReConstruct.UI.Core.UI

open ReConstruct.Render

module TransformView = 

    let New() =
        let rotate v = fun _ -> v |> Events.OnRotation.Trigger
        let moveCameraZ v = fun _ -> v |> Events.OnCameraMoved.Trigger
        let scale v = fun _ -> v |> Events.OnScale.Trigger

        let delta, zoomFactor, scaleFactor = 0.1f, 0.05f, 0.05f

        //let cameraControls caption =
        //    seq {
        //        yield Icons.CAMERA_IN |> iconButton |> withClick (moveCameraZ zoomFactor) :> UIElement
        //        yield caption :> UIElement
        //        yield Icons.CAMERA_OUT |> iconButton |> withClick (moveCameraZ -zoomFactor) :> UIElement
        //    }

        let spinner up down =
            seq {
                Icons.CHEVRON_UP |> button "icon-button-mini" |> onClick up
                Icons.CHEVRON_DOWN |> button "icon-button-mini" |> onClick down
            } |> childrenOf (stack "spinner")

        let scaleControls caption =
            seq {                
                yield caption :> UIElement
                yield (spinner (scale scaleFactor) ((scale -scaleFactor))) :> UIElement
            }

        let rotationControls(axis, caption, label) = 
            seq {
                yield label |> textBlock "value-text" :> UIElement
                yield caption :> UIElement
                yield (spinner (rotate (axis, delta)) (rotate (axis, -delta))) :> UIElement
            }

        let transformText() = textBlock "value-text" "0.00"
        let rotXText, rotYText, rotZText, scaleText = transformText(), transformText(), transformText(), transformText()

        let updateTransform transform =
            let (rotX, rotY, rotZ), scale = transform.Rotation(), transform.Scale()
            rotXText.Text <- sprintf "%.2f" rotX
            rotYText.Text <- sprintf "%.2f" rotY
            rotZText.Text <- sprintf "%.2f" rotZ
            scaleText.Text <- sprintf "%.2f" scale

        Events.VolumeTransformed.Publish |> Event.add updateTransform

        let transformBlock name controls =
            seq {
                yield name |> label "panel-block-caption" :> UIElement
                yield controls |> childrenOf (stack "horizontal") :> UIElement
            } |> childrenOf (stack "panel-block")

        seq {
            yield seq {
                yield! rotationControls (Axis.X, rotXText, "X")
                yield! rotationControls (Axis.Y, rotYText, "Y")
                yield! rotationControls (Axis.Z, rotZText, "Z")
            } |> transformBlock "Rotate"
            yield scaleText |> scaleControls |> transformBlock "Scale"
        } |> childrenOf (stack "transform-view")
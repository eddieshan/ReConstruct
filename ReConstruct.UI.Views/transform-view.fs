namespace ReConstruct.UI.View

open System
open System.Windows

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
            let container = stack "spinner"
            Icons.CHEVRON_UP |> button "icon-button-mini" |> withClick up >- container
            Icons.CHEVRON_DOWN |> button "icon-button-mini" |> withClick down  >- container
            container

        let scaleControls caption =
            seq {                
                yield caption :> UIElement
                yield (spinner (scale scaleFactor) ((scale -scaleFactor))) :> UIElement
            }

        let rotationControls(axis, caption) = 
            seq {
                yield caption :> UIElement
                yield (spinner (rotate (axis, delta)) (rotate (axis, -delta))) :> UIElement
            }

        let view = stack "transform-view"

        let transformText() = textBlock "panel-caption" "0.00"
        let rotXText, rotYText, rotZText, scaleText = transformText(), transformText(), transformText(), transformText()

        let updateTransform transform =
            let (rotX, rotY, rotZ), scale = transform.Rotation(), transform.Scale()
            rotXText.Text <- sprintf "%.2f" rotX
            rotYText.Text <- sprintf "%.2f" rotY
            rotZText.Text <- sprintf "%.2f" rotZ
            scaleText.Text <- sprintf "%.2f" scale

        Events.VolumeTransformed.Publish |> Event.add updateTransform
        
        seq {
            yield Icons.ROTATE_RIGHT |> textBlock "icon-text" :> UIElement
            yield! rotationControls (Axis.X, rotXText)
            yield! rotationControls (Axis.Y, rotYText)
            yield! rotationControls (Axis.Z, rotZText)
            yield Icons.CAMERA_IN |> textBlock "icon-text" :> UIElement
            yield! scaleControls scaleText
        } |> Seq.iter(fun c -> c >- view)
        
        //cameraControls() |> toRow "Zoom"

        view
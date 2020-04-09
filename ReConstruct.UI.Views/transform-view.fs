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

        let scaleControls caption =
            seq {
                yield Icons.ZOOM_IN |> iconButton |> withClick (scale scaleFactor) :> UIElement
                yield caption :> UIElement
                yield Icons.ZOOM_OUT |> iconButton |> withClick (scale -scaleFactor) :> UIElement
            }

        let rotationControls(labelA, labelB, axis, caption) = 
            seq {
                yield labelA |> iconButton |> withClick (rotate (axis, delta)) :> UIElement
                yield caption :> UIElement
                yield labelB |> iconButton |> withClick (rotate (axis, -delta)) :> UIElement
            }

        let grid = columnGrid 4

        let toRow title controls =
            title |> textBlock "panel-caption" >- grid
            controls |> Seq.iter(fun c -> c >- grid)

        let transformText() = textBlock "panel-caption" "0.00"
        let rotXText, rotYText, rotZText, scaleText = transformText(), transformText(), transformText(), transformText()

        let updateTransform transform =
            let (rotX, rotY, rotZ), scale = transform.Rotation(), transform.Scale()
            rotXText.Text <- sprintf "%.2f" rotX
            rotYText.Text <- sprintf "%.2f" rotY
            rotZText.Text <- sprintf "%.2f" rotZ
            scaleText.Text <- sprintf "%.2f" scale

        Events.VolumeTransformed.Publish |> Event.add updateTransform
    
        rotationControls (Icons.CHEVRON_UP, Icons.CHEVRON_DOWN, Axis.X, rotXText) |> toRow "Rotate X"
        rotationControls (Icons.CHEVRON_LEFT, Icons.CHEVRON_RIGHT, Axis.Y, rotYText) |> toRow "Rotate Y"
        rotationControls (Icons.ROTATE_LEFT, Icons.ROTATE_RIGHT, Axis.Z, rotZText) |> toRow "Rotate Z"
        scaleControls scaleText |> toRow "Scale"
        //cameraControls() |> toRow "Zoom"

        grid
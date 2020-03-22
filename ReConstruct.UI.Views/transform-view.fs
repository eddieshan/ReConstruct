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

        let cameraControls() =
            seq {
                yield Icons.CAMERA_IN |> iconButton |> withClick (moveCameraZ zoomFactor) :> UIElement
                yield Icons.CAMERA_OUT |> iconButton |> withClick (moveCameraZ -zoomFactor) :> UIElement
            }

        let scaleControls() =
            seq {
                yield Icons.ZOOM_IN |> iconButton |> withClick (scale scaleFactor) :> UIElement
                yield Icons.ZOOM_OUT |> iconButton |> withClick (scale -scaleFactor) :> UIElement
            }

        let rotationControls(labelA, labelB, axis) = 
            seq {
                yield labelA |> iconButton |> withClick (rotate (axis, delta)) :> UIElement
                yield labelB |> iconButton |> withClick (rotate (axis, -delta)) :> UIElement
            }

        let grid = uniformGrid()

        grid.Columns <- 3

        let toRow title controls =
            title |> textBlock "panel-caption" >- grid
            controls |> Seq.iter(fun c -> c >- grid)
    
        rotationControls (Icons.CHEVRON_UP, Icons.CHEVRON_DOWN, Axis.X) |> toRow "Rotate X"
        rotationControls (Icons.CHEVRON_LEFT, Icons.CHEVRON_RIGHT,Axis.Y) |> toRow "Rotate Y"
        rotationControls (Icons.ROTATE_LEFT, Icons.ROTATE_RIGHT, Axis.Z) |> toRow "Rotate Z"
        scaleControls () |> toRow "Scale"
        cameraControls() |> toRow "Zoom"

        grid
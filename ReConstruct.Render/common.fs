namespace ReConstruct.Render

type Axis =
| X
| Y
| Z

type ModelTransform =
    {
        Rotate: Axis*float32 -> unit
        Rescale: float32 -> unit
        Rotation: unit -> float32*float32*float32
        Scale: unit -> float32
    }

module ModelTransform =
    let create () =
        let mutable rotX, rotY, rotZ, scale = 0.0f, 0.0f, 0.0f, 1.0f

        let rotate (axis, delta) =
            match axis with
            | X -> rotX <- rotX + delta
            | Y -> rotY <- rotY + delta
            | Z -> rotZ <- rotZ + delta

        {
            Rotate = rotate
            Rescale = fun delta -> scale <- scale + delta
            Rotation = fun() -> (rotX, rotY, rotZ)
            Scale = fun() -> scale
        }
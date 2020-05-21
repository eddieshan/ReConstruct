namespace ReConstruct.Geometry

open System
open System.Numerics

open ReConstruct.Data.Dicom

type Cube =
    {
        Values: int16[]
        Vertices: Vector3[]
        Gradients: Vector3[]
    }
    member x.GetIndex isoValue =
        let mutable cubeIndex = 0uy
        for i in 0..x.Values.Length-1 do
            if (x.Values.[i] <= isoValue) then
                cubeIndex <- cubeIndex ||| (1uy <<< i)
        cubeIndex |> int

module Cube =
    let create front back =
        let zFront, zBack = float32 front.UpperLeft.[2], float32 back.UpperLeft.[2]
        let stepY = float32 front.PixelSpacingY

        let top = float32 front.UpperLeft.[1]
        let bottom = top + stepY

        {
            Values = Array.zeroCreate<int16> 8
            Vertices = [|
                Vector3(0.0f, bottom, zBack)
                Vector3(0.0f, bottom, zBack)
                Vector3(0.0f, bottom, zFront)
                Vector3(0.0f, bottom, zFront)
                Vector3(0.0f, top, zBack)
                Vector3(0.0f, top, zBack)
                Vector3(0.0f, top, zFront)
                Vector3(0.0f, top, zFront)
            |]
            Gradients = Array.zeroCreate<Vector3> 8
        }
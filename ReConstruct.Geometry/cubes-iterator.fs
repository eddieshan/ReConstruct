namespace ReConstruct.Geometry

open System
open System.Numerics

open ReConstruct.Data.Dicom

module CubesIterator =

    type Cube =
        {
            IsoValue: int16
            Vertices: Vector3[]
            Values: int16[]
        }

    let iterate (front, back) isoValue polygonize = 

        let zFront, zBack = float32 front.UpperLeft.[2], float32 back.UpperLeft.[2]
        let stepX, stepY = float32 front.PixelSpacingX, float32 front.PixelSpacingY

        let top, left = float32 front.UpperLeft.[1], float32 front.UpperLeft.[0]
        let bottom, right = top + stepY, left + stepX

        let cube = 
            {
                IsoValue = isoValue
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
                Values = Array.zeroCreate<int16> 8
             }

        let setValues offset =
            let right = offset + 1
            let bottom = offset + front.Columns
            let bottomRight = bottom + 1

            cube.Values.[0] <- back.HField.[bottom]
            cube.Values.[1] <- back.HField.[bottomRight]
            cube.Values.[2] <- front.HField.[bottomRight]
            cube.Values.[3] <- front.HField.[bottom]
            cube.Values.[4] <- back.HField.[offset]
            cube.Values.[5] <- back.HField.[right]
            cube.Values.[6] <- front.HField.[right]
            cube.Values.[7] <- front.HField.[offset]


        let mutable rowOffset = 0
        for row in 0..front.Rows - 2 do
            cube.Vertices.[0].X <- left
            cube.Vertices.[1].X <- right
            cube.Vertices.[2].X <- right
            cube.Vertices.[3].X <- left
            cube.Vertices.[4].X <- left
            cube.Vertices.[5].X <- right
            cube.Vertices.[6].X <- right
            cube.Vertices.[7].X <- left

            for column in 0..front.Columns - 2 do

                setValues (rowOffset + column)
                polygonize cube

                for n in 0..7 do
                    cube.Vertices.[n].X <- cube.Vertices.[n].X + stepX

            for n in 0..7 do
                cube.Vertices.[n].Y <- cube.Vertices.[n].Y + stepY

            rowOffset <- rowOffset + front.Columns
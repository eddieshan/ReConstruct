namespace ReConstruct.Geometry

open System
open System.Numerics

open ReConstruct.Data.Dicom

module CubesIterator =

    type Cube =
        {
            IsoValue: int
            Vertices: Vector3[]
            Levels: int[]
        }

    let iterate (front, back) isoValue polygonize = 

        let zFront, zBack = float32 front.Layout.UpperLeft.[2], float32 back.Layout.UpperLeft.[2]
        let stepX, stepY = float32 front.Layout.PixelSpacing.X, float32 front.Layout.PixelSpacing.Y

        let top, left = float32 front.Layout.UpperLeft.[1], float32 front.Layout.UpperLeft.[0]
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
                Levels = Array.zeroCreate<int> 8
             }

        let setValues offset =
            let right = offset + 1
            let bottom = offset + front.Layout.Dimensions.Columns
            let bottomRight = bottom + 1

            cube.Levels.[0] <- back.HField.[bottom]
            cube.Levels.[1] <- back.HField.[bottomRight]
            cube.Levels.[2] <- front.HField.[bottomRight]
            cube.Levels.[3] <- front.HField.[bottom]
            cube.Levels.[4] <- back.HField.[offset]
            cube.Levels.[5] <- back.HField.[right]
            cube.Levels.[6] <- front.HField.[right]
            cube.Levels.[7] <- front.HField.[offset]


        let mutable rowOffset = 0
        for row in 0..front.Layout.Dimensions.Rows - 2 do
            cube.Vertices.[0].X <- left
            cube.Vertices.[1].X <- right
            cube.Vertices.[2].X <- right
            cube.Vertices.[3].X <- left
            cube.Vertices.[4].X <- left
            cube.Vertices.[5].X <- right
            cube.Vertices.[6].X <- right
            cube.Vertices.[7].X <- left

            for column in 0..front.Layout.Dimensions.Columns - 2 do

                setValues (rowOffset + column)
                polygonize cube

                for n in 0..7 do
                    cube.Vertices.[n].X <- cube.Vertices.[n].X + stepX

            for n in 0..7 do
                cube.Vertices.[n].Y <- cube.Vertices.[n].Y + stepY

            rowOffset <- rowOffset + front.Layout.Dimensions.Columns
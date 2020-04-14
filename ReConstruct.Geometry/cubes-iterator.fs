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
            AddPoint: Vector3 -> unit
        }

    let iterate (front, back) isoValue addPoints polygonize = 

        let setValues (row, column) x =
            let rowPlus1 = row + 1
            let columnPlus1 = column + 1
            let valueIndex(i, j) = i*front.Layout.Dimensions.Columns + j

            x.Levels.[0] <- back.HField.[valueIndex(rowPlus1, column)]
            x.Levels.[1] <- back.HField.[valueIndex(rowPlus1, columnPlus1)]
            x.Levels.[2] <- front.HField.[valueIndex(rowPlus1, columnPlus1)]
            x.Levels.[3] <- front.HField.[valueIndex(rowPlus1, column)]
            x.Levels.[4] <- back.HField.[valueIndex(row, column)]
            x.Levels.[5] <- back.HField.[valueIndex(row, columnPlus1)]
            x.Levels.[6] <- front.HField.[valueIndex(row, columnPlus1)]
            x.Levels.[7] <- front.HField.[valueIndex(row, column)]

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
                AddPoint = addPoints
             }

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

                setValues (row, column) cube
                polygonize cube

                for n in 0..7 do
                    cube.Vertices.[n].X <- cube.Vertices.[n].X + stepX

            for n in 0..7 do
                cube.Vertices.[n].Y <- cube.Vertices.[n].Y + stepY
namespace ReConstruct.Geometry

open System
open System.Numerics

open ReConstruct.Data.Dicom

module CubesIterator =

    type Cube =
        {
            IsoValue: float32
            Vertices: Vector3[]
            Levels: int[]
            AddPoint: Vector3 -> unit
        }

    let iterate (front, back) isoValue addPoints polygonize = 
        let cube = 
            {
                IsoValue = isoValue
                Vertices = Array.zeroCreate<Vector3> 8
                Levels = Array.zeroCreate<int> 8
                AddPoint = addPoints
             }

        let setValues (row, column) x =
            let rowPlus1 = row + 1
            let columnPlus1 = column + 1

            x.Levels.[0] <- back.HounsfieldBuffer.[rowPlus1, column]
            x.Levels.[1] <- back.HounsfieldBuffer.[rowPlus1, columnPlus1]
            x.Levels.[2] <- front.HounsfieldBuffer.[rowPlus1, columnPlus1]
            x.Levels.[3] <- front.HounsfieldBuffer.[rowPlus1, column]
            x.Levels.[4] <- back.HounsfieldBuffer.[row, column]
            x.Levels.[5] <- back.HounsfieldBuffer.[row, columnPlus1]
            x.Levels.[6] <- front.HounsfieldBuffer.[row, columnPlus1]
            x.Levels.[7] <- front.HounsfieldBuffer.[row, column]

        let zFront, zBack = float32 front.SliceParams.UpperLeft.[2], float32 back.SliceParams.UpperLeft.[2]
        let stepX, stepY = float32 front.SliceParams.PixelSpacing.X, float32 front.SliceParams.PixelSpacing.Y

        let yTopFront = float32 front.SliceParams.UpperLeft.[1]
        let yBottomFront = yTopFront + stepY

        let yTopBack = float32 back.SliceParams.UpperLeft.[1]
        let yBottomBack = yTopBack + float32 back.SliceParams.PixelSpacing.Y

        cube.Vertices.[0].Z <- zBack
        cube.Vertices.[1].Z <- zBack
        cube.Vertices.[2].Z <- zFront
        cube.Vertices.[3].Z <- zFront
        cube.Vertices.[4].Z <- zBack
        cube.Vertices.[5].Z <- zBack
        cube.Vertices.[6].Z <- zFront
        cube.Vertices.[7].Z <- zFront

        cube.Vertices.[0].Y <- yBottomBack
        cube.Vertices.[1].Y <- yBottomBack
        cube.Vertices.[2].Y <- yBottomFront
        cube.Vertices.[3].Y <- yBottomFront
        cube.Vertices.[4].Y <- yTopBack
        cube.Vertices.[5].Y <- yTopBack
        cube.Vertices.[6].Y <- yTopFront
        cube.Vertices.[7].Y <- yTopFront

        for row in 0..front.SliceParams.Dimensions.Rows - 2 do
            let xLeftFront = float32 front.SliceParams.UpperLeft.[0]
            let xRightFront = xLeftFront + stepX

            let xLeftBack = float32 back.SliceParams.UpperLeft.[0]
            let xRightBack = xLeftBack + stepX

            cube.Vertices.[0].X <- xLeftBack
            cube.Vertices.[1].X <- xRightBack
            cube.Vertices.[2].X <- xRightFront
            cube.Vertices.[3].X <- xLeftFront
            cube.Vertices.[4].X <- xLeftBack
            cube.Vertices.[5].X <- xRightBack
            cube.Vertices.[6].X <- xRightFront
            cube.Vertices.[7].X <- xLeftFront

            for column in 0..front.SliceParams.Dimensions.Columns - 2 do

                setValues (row, column) cube

                polygonize cube

                for n in 0..7 do
                    cube.Vertices.[n].X <- cube.Vertices.[n].X + stepX

            for n in 0..7 do
                cube.Vertices.[n].Y <- cube.Vertices.[n].Y + stepY
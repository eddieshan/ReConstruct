namespace ReConstruct.Geometry

open System
open System.Numerics

open ReConstruct.Data.Dicom

module CubesIterator =

    type Cube =
        {
            Front: CatSlice
            Back: CatSlice
            IsoLevel: float32
            Vertices: Vector3[]
            Levels: int[]
            AddPoint: Vector3 -> unit
        }

    let private SetValues (row, column) x =
        let rowPlus1 = row + 1
        let columnPlus1 = column + 1

        x.Levels.[0] <- x.Back.HounsfieldBuffer.[rowPlus1, column]
        x.Levels.[1] <- x.Back.HounsfieldBuffer.[rowPlus1, columnPlus1]

        x.Levels.[2] <- x.Front.HounsfieldBuffer.[rowPlus1, columnPlus1]
        x.Levels.[3] <- x.Front.HounsfieldBuffer.[rowPlus1, column]

        x.Levels.[4] <- x.Back.HounsfieldBuffer.[row, column]
        x.Levels.[5] <- x.Back.HounsfieldBuffer.[row, columnPlus1]

        x.Levels.[6] <- x.Front.HounsfieldBuffer.[row, columnPlus1]
        x.Levels.[7] <- x.Front.HounsfieldBuffer.[row, column]

    let traverseWidthFirst x polygonize =

        let zFront, zBack = float32 x.Front.SliceParams.UpperLeft.[2], float32 x.Back.SliceParams.UpperLeft.[2]

        let yTopFront = float32 x.Front.SliceParams.UpperLeft.[1]
        let yBottomFront = yTopFront + float32 x.Front.SliceParams.PixelSpacing.Y

        let yTopBack = float32 x.Back.SliceParams.UpperLeft.[1]
        let yBottomBack = yTopBack + float32 x.Back.SliceParams.PixelSpacing.Y

        x.Vertices.[0].Z <- zBack
        x.Vertices.[1].Z <- zBack
        x.Vertices.[2].Z <- zFront
        x.Vertices.[3].Z <- zFront
        x.Vertices.[4].Z <- zBack
        x.Vertices.[5].Z <- zBack
        x.Vertices.[6].Z <- zFront
        x.Vertices.[7].Z <- zFront

        x.Vertices.[0].Y <- yBottomBack
        x.Vertices.[1].Y <- yBottomBack
        x.Vertices.[2].Y <- yBottomFront
        x.Vertices.[3].Y <- yBottomFront
        x.Vertices.[4].Y <- yTopBack
        x.Vertices.[5].Y <- yTopBack
        x.Vertices.[6].Y <- yTopFront
        x.Vertices.[7].Y <- yTopFront

        for row in 0..x.Front.SliceParams.Dimensions.Rows - 2 do
            let xLeftFront = float32 x.Front.SliceParams.UpperLeft.[0]
            let xRightFront = xLeftFront + float32 x.Front.SliceParams.PixelSpacing.X

            let xLeftBack = float32 x.Back.SliceParams.UpperLeft.[0]
            let xRightBack = xLeftBack + float32 x.Back.SliceParams.PixelSpacing.X

            x.Vertices.[0].X <- xLeftBack
            x.Vertices.[1].X <- xRightBack
            x.Vertices.[2].X <- xRightFront
            x.Vertices.[3].X <- xLeftFront
            x.Vertices.[4].X <- xLeftBack
            x.Vertices.[5].X <- xRightBack
            x.Vertices.[6].X <- xRightFront
            x.Vertices.[7].X <- xLeftFront

            for column in 0..x.Front.SliceParams.Dimensions.Columns - 2 do

                SetValues (row, column) x

                polygonize (x, row, column)

                x.Vertices.[0].X <- x.Vertices.[0].X + float32 x.Back.SliceParams.PixelSpacing.X
                x.Vertices.[1].X <- x.Vertices.[1].X + float32 x.Back.SliceParams.PixelSpacing.X
                x.Vertices.[2].X <- x.Vertices.[2].X + float32 x.Front.SliceParams.PixelSpacing.X
                x.Vertices.[3].X <- x.Vertices.[3].X + float32 x.Front.SliceParams.PixelSpacing.X
                x.Vertices.[4].X <- x.Vertices.[4].X + float32 x.Back.SliceParams.PixelSpacing.X
                x.Vertices.[5].X <- x.Vertices.[5].X + float32 x.Back.SliceParams.PixelSpacing.X
                x.Vertices.[6].X <- x.Vertices.[6].X + float32 x.Front.SliceParams.PixelSpacing.X
                x.Vertices.[7].X <- x.Vertices.[7].X + float32 x.Front.SliceParams.PixelSpacing.X

            x.Vertices.[0].Y <- x.Vertices.[0].Y + float32 x.Back.SliceParams.PixelSpacing.Y
            x.Vertices.[1].Y <- x.Vertices.[1].Y + float32 x.Back.SliceParams.PixelSpacing.Y
            x.Vertices.[2].Y <- x.Vertices.[2].Y + float32 x.Front.SliceParams.PixelSpacing.Y
            x.Vertices.[3].Y <- x.Vertices.[3].Y + float32 x.Front.SliceParams.PixelSpacing.Y
            x.Vertices.[4].Y <- x.Vertices.[4].Y + float32 x.Back.SliceParams.PixelSpacing.Y
            x.Vertices.[5].Y <- x.Vertices.[5].Y + float32 x.Back.SliceParams.PixelSpacing.Y
            x.Vertices.[6].Y <- x.Vertices.[6].Y + float32 x.Front.SliceParams.PixelSpacing.Y
            x.Vertices.[7].Y <- x.Vertices.[7].Y + float32 x.Front.SliceParams.PixelSpacing.Y

    let iterate (front, back) isoValue addPoints polygonize = 
        let voxel = 
            {
                Front = front
                Back = back
                IsoLevel = isoValue
                Vertices = Array.zeroCreate<Vector3> 8
                Levels = Array.zeroCreate<int> 8
                AddPoint = addPoints
             }
        traverseWidthFirst voxel polygonize
namespace ReConstruct.Data.Imaging

open System
open System.Numerics

open ReConstruct.Data.Dicom

open ReConstruct.Data.Imaging.MarchingCubesTables

/// Marching cubes basic implementation. 
module MarchingCubesBasic =

    type private Voxel =
        {
            Front: CatSlice
            Back: CatSlice
            IsoLevel: float32
            AddPoint: Vector3 -> unit
            Vertices: Vector3[]
            Levels: int[]
        }

        member private x.SetValues (row, column) =
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

        member private x.InterpolatedVertex (index1: int, index2: int) =
            let tolerance = 0.00001f

            let val1 = float32 x.Levels.[index1]
            let val2 = float32 x.Levels.[index2]

            let point1 = x.Vertices.[index1]
            let point2 = x.Vertices.[index2]

            if Math.Abs(x.IsoLevel - val1) < tolerance then
                point1   
            elif Math.Abs(x.IsoLevel - val2) < tolerance then
                point2
            elif Math.Abs(val1 - val2) < tolerance then
                point1
            else
                let mu = (x.IsoLevel - val1) / (val2 - val1)
                let x = point1.X + mu * (point2.X - point1.X)
                let y = point1.Y + mu * (point2.Y - point1.Y)
                let z = point1.Z + mu * (point2.Z - point1.Z)
                Vector3(x, y, z)

        member private x.Polygonize(row, column) =

            x.SetValues (row, column)

            let mutable cubeIndex = 0
            let intIsoLevel = int x.IsoLevel

            for i in 0..x.Levels.Length-1 do
                if (x.Levels.[i] <= intIsoLevel) then
                    cubeIndex <- cubeIndex ||| (1 <<< i)

            if EdgeTable.[cubeIndex] <> 0 then
                let vertlist = Array.zeroCreate<Vector3> 12
            
                for i in 0..EdgeTraversal.Length-1 do
                    if (EdgeTable.[cubeIndex] &&& (1 <<< i)) > 0 then
                        vertlist.[i] <- x.InterpolatedVertex EdgeTraversal.[i]

                let mutable index = 0
                while TriTable.[cubeIndex, index] <> -1 do
                    let v0 = vertlist.[TriTable.[cubeIndex, index]]
                    let v1 = vertlist.[TriTable.[cubeIndex, index + 1]]
                    let v2 = vertlist.[TriTable.[cubeIndex, index + 2]]
                    let normal = Vector3.Cross(v2 - v0, v1 - v0) |> Vector3.Normalize

                    x.AddPoint v0
                    x.AddPoint normal
                    x.AddPoint v1
                    x.AddPoint normal
                    x.AddPoint v2
                    x.AddPoint normal

                    index <- index + 3

        member x.PolygonizeSection() =

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

                    x.Polygonize (row, column)

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

    let polygonize (front, back) isoLevel addPoint = 
        let voxel = 
            {
                Front = front
                Back = back
                IsoLevel = isoLevel
                Vertices = Array.zeroCreate<Vector3> 8
                Levels = Array.zeroCreate<int> 8
                AddPoint = addPoint
             }
        voxel.PolygonizeSection()
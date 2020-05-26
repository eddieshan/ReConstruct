namespace ReConstruct.Geometry

open System
open System.Numerics

open ReConstruct.Data.Dicom

open ReConstruct.Geometry.MarchingCubesTables

module CubesGradientIterator =

    let iterate (slices: ImageSlice[]) frontIndex isoValue addPoint = 
        let lastRow, lastColumn = slices.[frontIndex].Rows - 2, slices.[frontIndex].Columns - 2

        let cube = Cube.create slices.[frontIndex] slices.[frontIndex + 1] isoValue

        let jumpColumn = 1
        let jumpRow = slices.[frontIndex].Columns

        let vertices = Array.zeroCreate<Vector3> 12
        let gradients = Array.zeroCreate<Vector3> 12
        let front, back, next = slices.[frontIndex].HField, slices.[frontIndex + 1].HField, slices.[frontIndex + 2].HField

        let processCube tLeft row column =
            let tRight, bLeft = tLeft + jumpColumn, tLeft + jumpRow
            let bRight = bLeft + jumpColumn

            cube.Values.[0] <- back.[bLeft]
            cube.Values.[1] <- back.[bRight]
            cube.Values.[2] <- front.[bRight]
            cube.Values.[3] <- front.[bLeft]
            cube.Values.[4] <- back.[tLeft]
            cube.Values.[5] <- back.[tRight]
            cube.Values.[6] <- front.[tRight]
            cube.Values.[7] <- front.[tLeft]

            let cubeIndex = cube.GetIndex()

            if EdgeTable.[cubeIndex] <> 0 then

                let jumpUnder = (1 - row/lastRow) * jumpRow
                let uBLeft, uBRight = bLeft + jumpUnder, bRight + jumpUnder

                let jumpNextRight = (1 - column/lastColumn) * jumpColumn
                let rBRight, rTRight = bRight + jumpNextRight, tRight + jumpNextRight

                let backBLeft, backBRight = 2s*back.[bLeft], 2s*back.[bRight]
                let frontBRight, frontBLeft = 2s*front.[bRight], 2s*front.[bLeft]
                let backTLeft, backTRight = 2s*back.[tLeft], 2s*back.[tRight]
                let frontTRight, frontTLeft = 2s*front.[tRight], 2s*front.[tLeft]

                cube.Gradients.[0].X <- ((next.[bRight] + back.[bRight] - backBLeft) |> float32)*0.5f
                cube.Gradients.[0].Y <- ((next.[uBLeft] + back.[uBLeft] - backBLeft) |> float32)*0.5f
                cube.Gradients.[0].Z <- (next.[bLeft] - back.[bLeft]) |> float32

                cube.Gradients.[1].X <- ((next.[rBRight] + back.[rBRight] - backBRight) |> float32)*0.5f
                cube.Gradients.[1].Y <- ((next.[uBRight] + back.[uBRight] - backBRight) |> float32)*0.5f
                cube.Gradients.[1].Z <- (next.[bRight] - back.[bRight]) |> float32

                cube.Gradients.[2].X <- ((back.[rBRight] + front.[rBRight] - frontBRight) |> float32)*0.5f
                cube.Gradients.[2].Y <- ((back.[uBRight] + front.[uBRight] - frontBRight) |> float32)*0.5f
                cube.Gradients.[2].Z <- (back.[bRight] - front.[bRight]) |> float32

                cube.Gradients.[3].X <- ((back.[bRight] + front.[bRight] - frontBLeft) |> float32)*0.5f
                cube.Gradients.[3].Y <- ((back.[uBLeft] + front.[uBLeft] - frontBLeft) |> float32)*0.5f
                cube.Gradients.[3].Z <- (back.[bLeft] - front.[bLeft]) |> float32

                cube.Gradients.[4].X <- ((next.[tRight] + back.[tRight] - backTLeft) |> float32)*0.5f
                cube.Gradients.[4].Y <- ((next.[bLeft] + back.[bLeft] - backTLeft) |> float32)*0.5f
                cube.Gradients.[4].Z <- (next.[tLeft] - back.[tLeft]) |> float32

                cube.Gradients.[5].X <- ((next.[rTRight] + back.[rTRight] - backTRight) |> float32)*0.5f
                cube.Gradients.[5].Y <- ((next.[bRight] + back.[bRight] - backTRight) |> float32)*0.5f
                cube.Gradients.[5].Z <- (next.[tRight] - back.[tRight]) |> float32

                cube.Gradients.[6].X <- ((back.[rTRight] + front.[rTRight] - frontTRight) |> float32)*0.5f
                cube.Gradients.[6].Y <- ((back.[bRight] + front.[bRight] - frontTRight) |> float32)*0.5f
                cube.Gradients.[6].Z <- (back.[tRight] - front.[tRight]) |> float32

                cube.Gradients.[7].X <- ((back.[tRight] + front.[tRight] - frontTLeft) |> float32)*0.5f
                cube.Gradients.[7].Y <- ((back.[bLeft] + front.[bLeft] - frontTLeft) |> float32)*0.5f
                cube.Gradients.[7].Z <- (back.[tLeft] - front.[tLeft]) |> float32
        
                for i in 0..EdgeTraversal.Length-1 do
                    if (EdgeTable.[cubeIndex] &&& (1 <<< i)) > 0 then
                        let index1, index2 = int EdgeTraversal.[i].[0], int EdgeTraversal.[i].[1]
                        let v1, v2 = cube.Values.[index1], cube.Values.[index2]
                        let delta = v2 - v1

                        let mu =
                            if delta = 0s then
                                0.5f
                            else
                                float32(isoValue - v1) / (float32 delta)
                        vertices.[i] <- Vector3.Lerp(cube.Vertices.[index1], cube.Vertices.[index2], mu)
                        gradients.[i] <- Vector3.Lerp(cube.Gradients.[index1], cube.Gradients.[index2], mu)

                let triangles = TriTable2.[cubeIndex]

                for triangle in triangles do
                    vertices.[triangle.[0]] |> addPoint
                    gradients.[triangle.[0]] |> addPoint
                    
                    vertices.[triangle.[1]] |> addPoint
                    gradients.[triangle.[1]] |> addPoint

                    vertices.[triangle.[2]] |> addPoint
                    gradients.[triangle.[2]] |> addPoint


        let mutable rowOffset = 0
        let stepX, stepY = float32 slices.[frontIndex].PixelSpacingX, float32 slices.[frontIndex].PixelSpacingY
        let left = float32 slices.[frontIndex].UpperLeft.[0]
        let right = left + stepX

        for row in 0..lastRow do
            cube.Vertices.[0].X <- left
            cube.Vertices.[1].X <- right
            cube.Vertices.[2].X <- right
            cube.Vertices.[3].X <- left
            cube.Vertices.[4].X <- left
            cube.Vertices.[5].X <- right
            cube.Vertices.[6].X <- right
            cube.Vertices.[7].X <- left

            for column in 0..lastColumn do

                processCube (rowOffset + column) row column

                for n in 0..7 do
                    cube.Vertices.[n].X <- cube.Vertices.[n].X + stepX

            for n in 0..7 do
                cube.Vertices.[n].Y <- cube.Vertices.[n].Y + stepY

            rowOffset <- rowOffset + jumpRow
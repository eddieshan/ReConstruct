namespace ReConstruct.Geometry

open System
open System.Numerics

open ReConstruct.Data.Dicom

open ReConstruct.Geometry.MarchingCubesTables

module CubesGradientIterator =

    let iterate (front, back, next) isoValue addPoint = 
        let lastRow, lastColumn = front.Rows - 2, front.Columns - 2

        let cube = Cube.create front back isoValue

        let jumpRight = 1
        let jumpBottom = front.Columns
        let jumpBottomRight = jumpBottom + jumpRight

        let vertices = Array.zeroCreate<Vector3> 12
        let gradients = Array.zeroCreate<Vector3> 12

        let processCube topLeft row column =
            let tRight = topLeft + jumpRight
            let bLeft = topLeft + jumpBottom
            let bRight = topLeft + jumpBottomRight

            cube.Values.[0] <- back.HField.[bLeft]
            cube.Values.[1] <- back.HField.[bRight]
            cube.Values.[2] <- front.HField.[bRight]
            cube.Values.[3] <- front.HField.[bLeft]
            cube.Values.[4] <- back.HField.[topLeft]
            cube.Values.[5] <- back.HField.[tRight]
            cube.Values.[6] <- front.HField.[tRight]
            cube.Values.[7] <- front.HField.[topLeft]

            let cubeIndex = cube.GetIndex()

            if EdgeTable.[cubeIndex] <> 0 then

                let jumpUnder = (1 - row/lastRow) * jumpBottom
                let uBLeft, uBRight = bLeft + jumpUnder, bRight + jumpUnder

                let jumpNextRight = (1 - column/lastColumn) * jumpRight
                let rBRight, rTRight = bRight + jumpNextRight, tRight + jumpNextRight

                let backBLeft, backBRight = 2s*back.HField.[bLeft], 2s*back.HField.[bRight]
                let frontBRight, frontBLeft = 2s*front.HField.[bRight], 2s*front.HField.[bLeft]
                let backTLeft, backTRight = 2s*back.HField.[topLeft], 2s*back.HField.[tRight]
                let frontTRight, frontTLeft = 2s*front.HField.[tRight], 2s*front.HField.[topLeft]

                cube.Gradients.[0].X <- ((next.HField.[bRight] + back.HField.[bRight] - backBLeft) |> float32)*0.5f
                cube.Gradients.[0].Y <- ((next.HField.[uBLeft] + back.HField.[uBLeft] - backBLeft) |> float32)*0.5f
                cube.Gradients.[0].Z <- (next.HField.[bLeft] - back.HField.[bLeft]) |> float32

                cube.Gradients.[1].X <- ((next.HField.[rBRight] + back.HField.[rBRight] - backBRight) |> float32)*0.5f
                cube.Gradients.[1].Y <- ((next.HField.[uBRight] + back.HField.[uBRight] - backBRight) |> float32)*0.5f
                cube.Gradients.[1].Z <- (next.HField.[bRight] - back.HField.[bRight]) |> float32

                cube.Gradients.[2].X <- ((back.HField.[rBRight] + front.HField.[rBRight] - frontBRight) |> float32)*0.5f
                cube.Gradients.[2].Y <- ((back.HField.[uBRight] + front.HField.[uBRight] - frontBRight) |> float32)*0.5f
                cube.Gradients.[2].Z <- (back.HField.[bRight] - front.HField.[bRight]) |> float32

                cube.Gradients.[3].X <- ((back.HField.[bRight] + front.HField.[bRight] - frontBLeft) |> float32)*0.5f
                cube.Gradients.[3].Y <- ((back.HField.[uBLeft] + front.HField.[uBLeft] - frontBLeft) |> float32)*0.5f
                cube.Gradients.[3].Z <- (back.HField.[bLeft] - front.HField.[bLeft]) |> float32

                cube.Gradients.[4].X <- ((next.HField.[tRight] + back.HField.[tRight] - backTLeft) |> float32)*0.5f
                cube.Gradients.[4].Y <- ((next.HField.[bLeft] + back.HField.[bLeft] - backTLeft) |> float32)*0.5f
                cube.Gradients.[4].Z <- (next.HField.[topLeft] - back.HField.[topLeft]) |> float32

                cube.Gradients.[5].X <- ((next.HField.[rTRight] + back.HField.[rTRight] - backTRight) |> float32)*0.5f
                cube.Gradients.[5].Y <- ((next.HField.[bRight] + back.HField.[bRight] - backTRight) |> float32)*0.5f
                cube.Gradients.[5].Z <- (next.HField.[tRight] - back.HField.[tRight]) |> float32

                cube.Gradients.[6].X <- ((back.HField.[rTRight] + front.HField.[rTRight] - frontTRight) |> float32)*0.5f
                cube.Gradients.[6].Y <- ((back.HField.[bRight] + front.HField.[bRight] - frontTRight) |> float32)*0.5f
                cube.Gradients.[6].Z <- (back.HField.[tRight] - front.HField.[tRight]) |> float32

                cube.Gradients.[7].X <- ((back.HField.[tRight] + front.HField.[tRight] - frontTLeft) |> float32)*0.5f
                cube.Gradients.[7].Y <- ((back.HField.[bLeft] + front.HField.[bLeft] - frontTLeft) |> float32)*0.5f
                cube.Gradients.[7].Z <- (back.HField.[topLeft] - front.HField.[topLeft]) |> float32
        
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
        let stepX, stepY = float32 front.PixelSpacingX, float32 front.PixelSpacingY
        let left = float32 front.UpperLeft.[0]
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

            rowOffset <- rowOffset + front.Columns
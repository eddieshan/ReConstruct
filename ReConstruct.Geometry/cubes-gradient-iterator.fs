namespace ReConstruct.Geometry

open System
open System.Numerics

open ReConstruct.Data.Dicom

open ReConstruct.Geometry.CommonIndices
open ReConstruct.Geometry.MarchingCubesTables

module CubesGradientIterator =

    let iterate (slices: ImageSlice[]) frontIndex isoValue addPoint = 
        let lastRow, lastColumn = slices.[frontIndex].Rows - 2, slices.[frontIndex].Columns - 2

        let cube = Cube.create slices.[frontIndex] slices.[frontIndex + 1] isoValue

        let jumpColumn = 1
        let jumpRow = slices.[frontIndex].Columns
        let jumpDiagonal = jumpColumn + jumpRow

        let positions = [|
            [| 1; jumpRow; |]
            [| 1; jumpDiagonal; |]
            [| 0; jumpDiagonal; |]
            [| 0; jumpRow; |]
            [| 1; 0; |]
            [| 1; jumpColumn; |]
            [| 0; jumpColumn; |]
            [| 0; 0; |]
        |]

        let vertices = Array.zeroCreate<Vector3> 12
        let gradients = Array.zeroCreate<Vector3> 12
        let front, back = slices.[frontIndex].HField, slices.[frontIndex + 1].HField

        let backIndex = frontIndex + 1
        let gradient = Gradient(slices)

        let inline addTriangle index = 
            vertices.[index] |> addPoint
            gradients.[index] |> addPoint

        let processCube tLeft =
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

            let contributions = EdgeContributions.[cubeIndex]

            if EdgeTable.[cubeIndex] <> 0 then        
                for n in contributions do
                        let index1, index2 = int EdgeTraversal.[n].[0], int EdgeTraversal.[n].[1]
                        let v1, v2 = cube.Values.[index1], cube.Values.[index2]
                        let delta = v2 - v1

                        let mu =
                            if delta = 0s then
                                0.5f
                            else
                                float32(isoValue - v1) / (float32 delta)

                        gradient.setValue (frontIndex + positions.[index1].[0], tLeft + positions.[index1].[1], &cube.Gradients.[index1])
                        gradient.setValue (frontIndex + positions.[index2].[0], tLeft + positions.[index2].[1], &cube.Gradients.[index2])

                        vertices.[n] <- Vector3.Lerp(cube.Vertices.[index1], cube.Vertices.[index2], mu)
                        gradients.[n] <- Vector3.Lerp(cube.Gradients.[index1], cube.Gradients.[index2], mu)

                let triangles = TriTable2.[cubeIndex]

                for triangle in triangles do
                    triangle.[0] |> addTriangle
                    triangle.[1] |> addTriangle
                    triangle.[2] |> addTriangle

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

                processCube (rowOffset + column)

                for n in 0..7 do
                    cube.Vertices.[n].X <- cube.Vertices.[n].X + stepX

            for n in 0..7 do
                cube.Vertices.[n].Y <- cube.Vertices.[n].Y + stepY

            rowOffset <- rowOffset + jumpRow
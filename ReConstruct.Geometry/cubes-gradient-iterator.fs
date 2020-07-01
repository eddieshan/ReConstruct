namespace ReConstruct.Geometry

open System
open System.Numerics

open ReConstruct.Data.Dicom

open ReConstruct.Geometry.CommonIndices
open ReConstruct.Geometry.MarchingCubesTables

module CubesGradientIterator =

    let iterate (volume: UniformVolume) frontIndex addPoint = 
        let lastRow, lastColumn = volume.Slices.[frontIndex].Rows - 2, volume.Slices.[frontIndex].Columns - 2

        let cube = Cube.create volume.Slices.[frontIndex] volume.Slices.[frontIndex + 1] volume.IsoValue

        let jumpColumn = 1
        let jumpRow = volume.Slices.[frontIndex].Columns

        let vertices = Array.zeroCreate<Vector3> 12
        let gradients = Array.zeroCreate<Vector3> 12
        let front, back = volume.Slices.[frontIndex].HField, volume.Slices.[frontIndex + 1].HField

        let gradient = Gradient(volume.Slices)

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

            for n in EdgeContributions.[cubeIndex] do
                let index1, index2 = int EdgeTraversal.[n].[0], int EdgeTraversal.[n].[1]
                let v1, v2 = cube.Values.[index1], cube.Values.[index2]
                let delta = v2 - v1

                let mu =
                    if delta = 0s then
                        0.5f
                    else
                        float32(volume.IsoValue - v1) / (float32 delta)

                let gradientA = gradient.get (frontIndex + volume.CubeMap.[index1].[0], tLeft + volume.CubeMap.[index1].[1])
                let gradientB = gradient.get (frontIndex + volume.CubeMap.[index2].[0], tLeft + volume.CubeMap.[index2].[1])

                vertices.[n] <- Vector3.Lerp(cube.Vertices.[index1], cube.Vertices.[index2], mu)
                gradients.[n] <- Vector3.Lerp(gradientA, gradientB, mu)

            let triangles = TriTable2.[cubeIndex]

            for triangle in triangles do
                triangle.[0] |> addTriangle
                triangle.[1] |> addTriangle
                triangle.[2] |> addTriangle

        let mutable rowOffset = 0
        let left = volume.Slices.[frontIndex].TopLeft.X
        let right = left + volume.Slices.[frontIndex].PixelSpacing.X

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
                    cube.Vertices.[n].X <- cube.Vertices.[n].X + volume.Step.X

            for n in 0..7 do
                cube.Vertices.[n].Y <- cube.Vertices.[n].Y + volume.Step.Y

            rowOffset <- rowOffset + jumpRow
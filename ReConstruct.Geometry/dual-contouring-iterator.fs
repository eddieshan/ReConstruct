namespace ReConstruct.Geometry

open System
open System.Numerics

open ReConstruct.Data.Dicom

open ReConstruct.Geometry.CommonIndices
open ReConstruct.Geometry.MarchingCubesTables
open ReConstruct.Geometry.DualContouringTables

[<Struct>]
type DualCell = 
    {
        CubeIndex: int
        Position: Vector3
        Gradient: Vector3
    }

module DualContouringIterator =

    let iterate (volume: UniformVolume) frontIndex addPoint = 
        let lastRow, lastColumn = volume.Slices.[frontIndex].Rows - 2, volume.Slices.[frontIndex].Columns - 2
        let backIndex, nextIndex = frontIndex + 1, frontIndex + 2

        let jumpColumn, jumpRow = 1, volume.Slices.[frontIndex].Columns
        let gradient = Gradient(volume.Slices)
       
        let findInnerVertex (frontTopLeft: Vector3) tLeft cube (section: int[]) = 
            let front, back = volume.Slices.[section.[0]].HField, volume.Slices.[section.[1]].HField
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

            let mutable bestFitVertex = Vector3.Zero
            let mutable bestFitGradient = Vector3.Zero

            let cubeIndex = cube.GetIndex()

            let contributions = EdgeContributions.[cubeIndex]

            for n in contributions do
                let indexA, indexB = int EdgeTraversal.[n].[0], int EdgeTraversal.[n].[1]
                let v1, v2 = cube.Values.[indexA], cube.Values.[indexB]
                let delta = v2 - v1

                let mu =
                    if delta = 0s then
                        0.5f
                    else
                        float32(volume.IsoValue - v1) / (float32 delta)

                let gradientA = gradient.get (section.[volume.CubeMap.[indexA].[0]], tLeft + volume.CubeMap.[indexA].[1])
                let gradientB = gradient.get (section.[volume.CubeMap.[indexB].[0]], tLeft + volume.CubeMap.[indexB].[1])

                bestFitVertex <- bestFitVertex + Vector3.Lerp(volume.VertexOffsets.[indexA], volume.VertexOffsets.[indexB], mu)
                bestFitGradient <- bestFitGradient + Vector3.Lerp(gradientA, gradientB, mu)

            if contributions.Length > 0 then
                bestFitVertex <- frontTopLeft + (bestFitVertex /(float32 contributions.Length))

            {
                CubeIndex = cubeIndex
                Position = bestFitVertex
                Gradient = bestFitGradient
            }
        
        let left = volume.Slices.[frontIndex].TopLeft.X

        let nRows, nColumns = lastRow + 1, lastColumn + 1

        let sections = [| [| frontIndex; backIndex; |]; [| backIndex; nextIndex; |]; |]

        let createDualCellsRow _ = Array.zeroCreate<DualCell> nColumns
        let innerVertices = Array.init sections.Length (fun _ -> Array.init nRows createDualCellsRow)

        let mutable frontTopLeft = Vector3.Zero

        for n in 0..sections.Length-1 do
            let section = sections.[n]
            let cube = Cube.create volume.Slices.[section.[0]] volume.Slices.[section.[1]] volume.IsoValue
            let mutable rowOffset = 0

            frontTopLeft.Y <- volume.Slices.[section.[0]].TopLeft.Y
            frontTopLeft.Z <- volume.Slices.[section.[0]].TopLeft.Z

            for row in 0..lastRow do

                frontTopLeft.X <- left

                for column in 0..lastColumn do
                    let tLeft = rowOffset + column
                    innerVertices.[n].[row].[column] <- findInnerVertex frontTopLeft tLeft cube section

                    frontTopLeft.X <- frontTopLeft.X + volume.Step.X

                frontTopLeft.Y <- frontTopLeft.Y + volume.Step.Y
                rowOffset <- rowOffset + jumpRow

        for row in 0..lastRow do
            for column in 0..lastColumn do
                let edgeIndex = innerVertices.[0].[row].[column].CubeIndex
                let contributingQuads = QuadContributions.[edgeIndex]
                for n in contributingQuads do
                    for triangle in QuadsTraversal.[n] do
                        let innerVertex = innerVertices.[triangle.[0]].[row + triangle.[1]].[column + triangle.[2]]
                        innerVertex.Position |> addPoint
                        innerVertex.Gradient |> addPoint
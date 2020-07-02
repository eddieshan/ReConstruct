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

        let values = Array.zeroCreate<int16> 8

        let inline getIndex() =
            let mutable cubeIndex = 0
            for i in 0..values.Length-1 do
                if (values.[i] <= volume.IsoValue) then
                    cubeIndex <- cubeIndex ||| (1 <<< i)
            cubeIndex
       
        let findInnerVertex row column tLeft (section: int[]) = 
            let front, back = volume.Slices.[section.[0]].HField, volume.Slices.[section.[1]].HField
            let tRight, bLeft = tLeft + jumpColumn, tLeft + jumpRow
            let bRight = bLeft + jumpColumn

            values.[0] <- back.[bLeft]
            values.[1] <- back.[bRight]
            values.[2] <- front.[bRight]
            values.[3] <- front.[bLeft]
            values.[4] <- back.[tLeft]
            values.[5] <- back.[tRight]
            values.[6] <- front.[tRight]
            values.[7] <- front.[tLeft]

            let mutable bestFitVertex = Vector3.Zero
            let mutable bestFitGradient = Vector3.Zero

            let cubeIndex = getIndex()

            let contributions = EdgeContributions.[cubeIndex]

            for n in contributions do
                let indexA, indexB = int EdgeTraversal.[n].[0], int EdgeTraversal.[n].[1]
                let v1, v2 = values.[indexA], values.[indexB]
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

            // Cube front top left calculated only for contributing cubes. A more expensive calculation, done fewer times.
            // Assuming that in most cases a significant part of the volume will not contribute to the surface,
            // the net cost should be better or at least not worse if compared to the previous incremental version.
            // As additional advantage, this approach simplifies the code.
            if contributions.Length > 0 then
                let frontTopLeft = volume.Slices.[section.[0]].TopLeft + Vector3(float32 column, float32 row, 0.0f)*volume.Step
                bestFitVertex <- frontTopLeft + (bestFitVertex /(float32 contributions.Length))

            {
                CubeIndex = cubeIndex
                Position = bestFitVertex
                Gradient = bestFitGradient
            }

        let nRows, nColumns = lastRow + 1, lastColumn + 1

        let sections = [| [| frontIndex; backIndex; |]; [| backIndex; nextIndex; |]; |]

        let createDualCellsRow _ = Array.zeroCreate<DualCell> nColumns
        let innerVertices = Array.init sections.Length (fun _ -> Array.init nRows createDualCellsRow)

        for n in 0..sections.Length-1 do
            let section = sections.[n]
            let mutable rowOffset = 0

            for row in 0..lastRow do
                for column in 0..lastColumn do
                    let tLeft = rowOffset + column
                    innerVertices.[n].[row].[column] <- findInnerVertex row column tLeft section

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
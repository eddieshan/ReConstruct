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

    let iterate (slices: ImageSlice[]) frontIndex isoValue addPoint = 
        let lastRow, lastColumn = slices.[frontIndex].Rows - 2, slices.[frontIndex].Columns - 2
        let backIndex, nextIndex = frontIndex + 1, frontIndex + 2

        let stepX, stepY = slices.[frontIndex].PixelSpacing.X, slices.[frontIndex].PixelSpacing.Y
        let stepZ = slices.[backIndex].TopLeft.Z - slices.[frontIndex].TopLeft.Z

        let jumpColumn, jumpRow = 1, slices.[frontIndex].Columns
        let jumpDiagonal = jumpColumn + jumpRow
        let gradient = Gradient(slices)

        let cubeMap = [|
            [| 1; jumpRow; |]
            [| 1; jumpDiagonal; |]
            [| 0; jumpDiagonal; |]
            [| 0; jumpRow; |]
            [| 1; 0; |]
            [| 1; jumpColumn; |]
            [| 0; jumpColumn; |]
            [| 0; 0; |]
        |]

        let vertexOffset = [|
            Vector3(0.0f, stepY, stepZ)
            Vector3(stepX, stepY, stepZ)
            Vector3(stepX, stepY, 0.0f)
            Vector3(0.0f, stepY, 0.0f)
            Vector3(0.0f, 0.0f, stepZ)
            Vector3(stepX, 0.0f, stepZ)
            Vector3(stepX, 0.0f, 0.0f)
            Vector3(0.0f, 0.0f, 0.0f)
        |]
       
        let findInnerVertex (frontTopLeft: Vector3) tLeft cube (section: int[]) = 
            let front, back = slices.[section.[0]].HField, slices.[section.[1]].HField
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
                        float32(isoValue - v1) / (float32 delta)

                let gradientA = gradient.get (section.[cubeMap.[indexA].[0]], tLeft + cubeMap.[indexA].[1])
                let gradientB = gradient.get (section.[cubeMap.[indexB].[0]], tLeft + cubeMap.[indexB].[1])

                bestFitVertex <- bestFitVertex + Vector3.Lerp(vertexOffset.[indexA], vertexOffset.[indexB], mu)
                bestFitGradient <- bestFitGradient + Vector3.Lerp(gradientA, gradientB, mu)

            if contributions.Length > 0 then
                bestFitVertex <- frontTopLeft + (bestFitVertex /(float32 contributions.Length))

            {
                CubeIndex = cubeIndex
                Position = bestFitVertex
                Gradient = bestFitGradient
            }
        
        let left = slices.[frontIndex].TopLeft.X
        let right = left + slices.[frontIndex].PixelSpacing.X

        let nRows, nColumns = lastRow + 1, lastColumn + 1

        let sections = [| [| frontIndex; backIndex; |]; [| backIndex; nextIndex; |]; |]

        let createDualCellsRow _ = Array.zeroCreate<DualCell> nColumns
        let innerVertices = Array.init sections.Length (fun _ -> Array.init nRows createDualCellsRow)

        let mutable frontTopLeft = Vector3.Zero

        for n in 0..sections.Length-1 do
            let section = sections.[n]
            let cube = Cube.create slices.[section.[0]] slices.[section.[1]] isoValue
            let mutable rowOffset = 0

            frontTopLeft.Y <- slices.[section.[0]].TopLeft.Y
            frontTopLeft.Z <- slices.[section.[0]].TopLeft.Z

            for row in 0..lastRow do

                frontTopLeft.X <- left

                for column in 0..lastColumn do
                    let tLeft = rowOffset + column
                    innerVertices.[n].[row].[column] <- findInnerVertex frontTopLeft tLeft cube section

                    frontTopLeft.X <- frontTopLeft.X + stepX

                frontTopLeft.Y <- frontTopLeft.Y + stepY
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
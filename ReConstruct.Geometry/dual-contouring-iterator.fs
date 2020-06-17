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

        let jumpColumn, jumpRow = 1, slices.[frontIndex].Columns
        let jumpDiagonal = jumpColumn + jumpRow
        let gradient = Gradient(slices)

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
       
        let findInnerVertex tLeft cube offset = 
            let indexFirst = frontIndex + offset
            let indexSecond = indexFirst + 1
            let front, back = slices.[indexFirst].HField, slices.[indexSecond].HField
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

                gradient.setValue (indexFirst + positions.[indexA].[0], tLeft + positions.[indexA].[1], &cube.Gradients.[indexA])
                gradient.setValue (indexFirst + positions.[indexB].[0], tLeft + positions.[indexB].[1], &cube.Gradients.[indexB])

                bestFitVertex <- bestFitVertex + Vector3.Lerp(cube.Vertices.[indexA], cube.Vertices.[indexB], mu)
                bestFitGradient <- bestFitGradient + Vector3.Lerp(cube.Gradients.[indexA], cube.Gradients.[indexB], mu)

            if contributions.Length > 0 then
                bestFitVertex <-  bestFitVertex/(float32 contributions.Length) 

            {
                CubeIndex = cubeIndex
                Position = bestFitVertex
                Gradient = bestFitGradient
            }
        
        let stepX, stepY = float32 slices.[frontIndex].PixelSpacingX, float32 slices.[frontIndex].PixelSpacingY
        let left = float32 slices.[frontIndex].UpperLeft.[0]
        let right = left + stepX

        let nRows, nColumns = lastRow + 1, lastColumn + 1

        let innerVertices = [|
            Array.init nRows (fun _ -> Array.zeroCreate<DualCell> nColumns)
            Array.init nRows (fun _ -> Array.zeroCreate<DualCell> nColumns)
        |]

        for n in 0..1 do
            let indexFirst = frontIndex + n
            let cube = Cube.create slices.[indexFirst] slices.[indexFirst + 1] isoValue
            let mutable rowOffset = 0

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
                    let tLeft = rowOffset + column
                    innerVertices.[n].[row].[column] <- findInnerVertex tLeft cube n

                    for n in 0..7 do
                        cube.Vertices.[n].X <- cube.Vertices.[n].X + stepX

                for n in 0..7 do
                    cube.Vertices.[n].Y <- cube.Vertices.[n].Y + stepY

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
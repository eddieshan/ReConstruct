namespace ReConstruct.Geometry

open System
open System.Numerics

open ReConstruct.Data.Dicom

open ReConstruct.Geometry.CommonIndices
open ReConstruct.Geometry.MarchingCubesTables
open ReConstruct.Geometry.DualContouringTables

module DualContouringIteratorSimd =

    let iterate (slices: ImageSlice[]) frontIndex isoValue addPoint = 
        let jumpColumn, jumpRow = 1, slices.[frontIndex].Columns
        let jumpDiagonal = jumpColumn + jumpRow
        let gradient = Gradient(slices)

        let backIndex, nextIndex = frontIndex + 1, frontIndex + 2
        let stepX, stepY = float32 slices.[frontIndex].PixelSpacingX, float32 slices.[frontIndex].PixelSpacingY
        let stepZ = float32(slices.[backIndex].UpperLeft.[2] - slices.[frontIndex].UpperLeft.[2])

        let positions = [|
            [| 1; 2; |]
            [| 1; 3; |]
            [| 0; 3; |]
            [| 0; 2; |]
            [| 1; 0; |]
            [| 1; 1; |]
            [| 0; 1; |]
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
       
        let findInnerVertex (frontTopLeft: Vector3) (corners: Vector<int>) (cube: Cube) (section: int[]) = 
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

                gradient.setValue (section.[positions.[indexA].[0]], corners.[positions.[indexA].[1]], &cube.Gradients.[indexA])
                gradient.setValue (section.[positions.[indexB].[0]], corners.[positions.[indexB].[1]], &cube.Gradients.[indexB])

                bestFitVertex <- bestFitVertex + Vector3.Lerp(vertexOffset.[indexA], vertexOffset.[indexB], mu)
                bestFitGradient <- bestFitGradient + Vector3.Lerp(cube.Gradients.[indexA], cube.Gradients.[indexB], mu)

            if contributions.Length > 0 then
                bestFitVertex <- frontTopLeft + (bestFitVertex /(float32 contributions.Length))

            {
                CubeIndex = cubeIndex
                Position = bestFitVertex
                Gradient = bestFitGradient
            }        
        
        let left = float32 slices.[frontIndex].UpperLeft.[0]

        let lastRow, lastColumn = slices.[frontIndex].Rows - 2, slices.[frontIndex].Columns - 2
        let nRows, nColumns = lastRow + 1, lastColumn + 1
        
        let columnJump = Vector([| 1; 1; 1; 1; |])

        let sections = [| [| frontIndex; backIndex; |]; [| backIndex; nextIndex; |]; |]

        let createDualCellsRow _ = Array.zeroCreate<DualCell> nColumns
        let innerVertices = Array.init sections.Length (fun _ -> Array.init nRows createDualCellsRow)

        let mutable frontTopLeft = Vector3.Zero

        for n in 0..sections.Length-1 do
            let section = sections.[n]
            let cube = Cube.create slices.[section.[0]] slices.[section.[1]] isoValue
            let mutable corners = Vector([| 0; jumpColumn; jumpRow; jumpDiagonal; |])

            frontTopLeft.Y <- float32 slices.[section.[0]].UpperLeft.[1]
            frontTopLeft.Z <- float32 slices.[section.[0]].UpperLeft.[2]

            for row in 0..lastRow do
                frontTopLeft.X <- left

                for column in 0..lastColumn do

                    let front, back = slices.[section.[0]].HField, slices.[section.[1]].HField
                    cube.Values.[0] <- back.[corners.[2]]
                    cube.Values.[1] <- back.[corners.[3]]
                    cube.Values.[2] <- front.[corners.[3]]
                    cube.Values.[3] <- front.[corners.[2]]
                    cube.Values.[4] <- back.[corners.[0]]
                    cube.Values.[5] <- back.[corners.[1]]
                    cube.Values.[6] <- front.[corners.[1]]
                    cube.Values.[7] <- front.[corners.[0]]

                    innerVertices.[n].[row].[column] <- findInnerVertex frontTopLeft corners cube section

                    corners <- corners + columnJump

                    frontTopLeft.X <- frontTopLeft.X + stepX

                frontTopLeft.Y <- frontTopLeft.Y + stepY

                corners <- corners + columnJump

        for row in 0..lastRow do
            for column in 0..lastColumn do
                let edgeIndex = innerVertices.[0].[row].[column].CubeIndex
                let contributingQuads = QuadContributions.[edgeIndex]
                for n in contributingQuads do
                    for triangle in QuadsTraversal.[n] do
                        let innerVertex = innerVertices.[triangle.[0]].[row + triangle.[1]].[column + triangle.[2]]
                        innerVertex.Position |> addPoint
                        innerVertex.Gradient |> addPoint
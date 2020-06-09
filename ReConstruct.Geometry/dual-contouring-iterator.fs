namespace ReConstruct.Geometry

open System
open System.Numerics

open ReConstruct.Data.Dicom

open ReConstruct.Geometry.MarchingCubesTables

type Vertex = 
    {
        Index: int
        Position: Vector3
        Gradient: Vector3
    }

module DualContouringIterator =

    let private QuadsTable = [|
        [| [|0; 0; 0; |]; [|1; 0; 0; |]; [|0; 1; 0; |]; [|1; 0; 0; |]; [|0; 1; 0; |]; [|1; 1; 0; |]; |]
        [| [|0; 0; 0; |]; [|1; 0; 0; |]; [|0; 0; 1; |]; [|1; 0; 0; |]; [|0; 0; 1; |]; [|1; 0; 1; |]; |]
        [| [|0; 0; 0; |]; [|0; 0; 1; |]; [|0; 1; 0; |]; [|0; 0; 1; |]; [|0; 1; 0; |]; [|0; 1; 1; |]; |]
    |]

    let iterate (slices: ImageSlice[]) frontIndex isoValue addPoint = 
        let lastRow, lastColumn = slices.[frontIndex].Rows - 2, slices.[frontIndex].Columns - 2

        let cube = Cube.create slices.[frontIndex] slices.[frontIndex + 1] isoValue

        let jumpColumn = 1
        let jumpRow = slices.[frontIndex].Columns

        let backIndex = frontIndex + 1
        let gradient = Gradient(slices)
        
        let findInnerVertex tLeft (front: int16[], back: int16[]) = 
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
            let mutable contributingEdges = 0

            let cubeIndex = cube.GetIndex()

            if cubeIndex <> 0 then

                gradient.setValue (backIndex, bLeft, &cube.Gradients.[0])
                gradient.setValue (backIndex, bRight, &cube.Gradients.[1])
                gradient.setValue (frontIndex, bRight, &cube.Gradients.[2])
                gradient.setValue (frontIndex, bLeft, &cube.Gradients.[3])
                gradient.setValue (backIndex, tLeft, &cube.Gradients.[4])
                gradient.setValue (backIndex, tRight, &cube.Gradients.[5])
                gradient.setValue (frontIndex, tRight, &cube.Gradients.[6])
                gradient.setValue (frontIndex, tLeft, &cube.Gradients.[7])

                let mutable high, low = 0, 0
                for i in 0..EdgeTraversal.Length-1 do                        
                    let indexA, indexB = int EdgeTraversal.[i].[0], int EdgeTraversal.[i].[1]
                    //let signA, signB = cubeIndex &&& (1 <<< indexA), cubeIndex &&& (1 <<< indexB)
                    //if signA <> signB then
                    if (isoValue >= cube.Values.[indexA] && isoValue < cube.Values.[indexB]) || 
                        (isoValue < cube.Values.[indexA] && isoValue >= cube.Values.[indexB]) then
                        if cube.Values.[indexA] > cube.Values.[indexB] then
                            high <- indexA
                            low <- indexB
                        else
                            high <- indexB
                            low <- indexA
                                    
                        let v1, v2 = cube.Values.[low], cube.Values.[high]
                        let delta = v2 - v1

                        let mu =
                            if delta = 0s then
                                0.5f
                            else
                                float32(isoValue - v1) / (float32 delta)

                        contributingEdges <- contributingEdges + 1
                        bestFitVertex <- bestFitVertex + Vector3.Lerp(cube.Vertices.[high], cube.Vertices.[low], mu)
                        bestFitGradient <- bestFitGradient + Vector3.Lerp(cube.Gradients.[high], cube.Gradients.[low], mu)

                    //if (EdgeTable.[cubeIndex] &&& (1 <<< i)) > 0 then
                    //    let indexA, indexB = int EdgeTraversal.[i].[0], int EdgeTraversal.[i].[1]
                    //    let v1, v2 = cube.Values.[indexA], cube.Values.[indexB]
                    //    let delta = v2 - v1

                    //    let mu =
                    //        if delta = 0s then
                    //            0.5f
                    //        else
                    //            float32(isoValue - v1) / (float32 delta)
                        
                    //    contributingEdges <- contributingEdges + 1
                    //    bestFitVertex <- bestFitVertex + Vector3.Lerp(cube.Vertices.[indexA], cube.Vertices.[indexB], mu)
                    //    bestFitGradient <- bestFitGradient + Vector3.Lerp(cube.Gradients.[indexA], cube.Gradients.[indexB], mu)
            
            if contributingEdges > 0 then
                {
                    Index = cubeIndex
                    Position = bestFitVertex/(float32 contributingEdges)
                    Gradient = bestFitGradient/(float32 contributingEdges)
                }
            else
                {
                    Index = cubeIndex
                    Position = bestFitVertex
                    Gradient = bestFitGradient
                }

        let mutable rowOffset = 0
        let stepX, stepY = float32 slices.[frontIndex].PixelSpacingX, float32 slices.[frontIndex].PixelSpacingY
        let left = float32 slices.[frontIndex].UpperLeft.[0]
        let right = left + stepX

        let front, back, next = slices.[frontIndex].HField, slices.[frontIndex + 1].HField, slices.[frontIndex + 2].HField

        let innerVertices = [|
            Array.init (lastRow + 1) (fun _ -> Array.zeroCreate<Vertex> (lastColumn + 1))
            Array.init (lastRow + 1) (fun _ -> Array.zeroCreate<Vertex> (lastColumn + 1))
        |]

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
                innerVertices.[0].[row].[column] <- findInnerVertex tLeft (front, back)
                innerVertices.[1].[row].[column] <- findInnerVertex tLeft (back, next)

                for n in 0..7 do
                    cube.Vertices.[n].X <- cube.Vertices.[n].X + stepX

            for n in 0..7 do
                cube.Vertices.[n].Y <- cube.Vertices.[n].Y + stepY

            rowOffset <- rowOffset + jumpRow

        rowOffset <- 0

        let addQuad quadIndex (row, column) =
            let indices = QuadsTable.[quadIndex]
            let isZero vertex = vertex.Position.X = 0.0f && vertex.Position.Y = 0.0f && vertex.Position.Z = 0.0f
            let hasGaps = indices |> Array.exists(fun index -> innerVertices.[index.[0]].[row + index.[1]].[column + index.[2]] |> isZero)

            if not hasGaps then
                for i in 0..indices.Length-1 do
                    let index = indices.[i]
                    let innerVertex = innerVertices.[index.[0]].[row + index.[1]].[column + index.[2]]

                    innerVertex.Position |> addPoint
                    innerVertex.Gradient |> addPoint

        for row in 0..lastRow do

            for column in 0..lastColumn do
                let tLeft = rowOffset + column
                let tRight, bLeft = tLeft + jumpColumn, tLeft + jumpRow
                let bRight = bLeft + jumpColumn

                let hasLeft = (isoValue >= back.[bLeft] && isoValue < back.[bRight]) || 
                                (isoValue < back.[bLeft] && isoValue >= back.[bRight]) || 
                                (isoValue = back.[bLeft])
                let hasTop = (isoValue >= back.[bRight] && isoValue < back.[tRight]) || 
                                (isoValue < back.[bRight] && isoValue >= back.[tRight]) ||
                                (isoValue = back.[bRight])
                let hasFront = (isoValue >= front.[bRight] && isoValue < back.[bRight]) || 
                                (isoValue < front.[bRight] && isoValue >= back.[bRight]) ||
                                (isoValue = front.[bRight])

                if hasLeft then
                    addQuad 0 (row, column)
                if hasTop then
                    addQuad 1 (row, column)
                if hasFront then
                    addQuad 2 (row, column)

            rowOffset <- rowOffset + jumpRow

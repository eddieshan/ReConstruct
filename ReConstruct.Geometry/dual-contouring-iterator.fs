namespace ReConstruct.Geometry

open System
open System.Numerics

open ReConstruct.Data.Dicom

open ReConstruct.Geometry.MarchingCubesTables

type DualCell = 
    {
        DualEdges: int
        Position: Vector3
        Gradient: Vector3
    }

module DualContouringIterator =

    let private XYZEdges = [| 
        [| 0; 1 <<< 0; |]; //(0, 1) -> 0, x axis edge.
        [| 9; 1 <<< 9; |]; //(1, 5) -> 9, y axis edge.
        [| 1; 1 <<< 1; |]; //(1, 2) -> 1, z axis edge.
    |]

    let private QuadsTraversal = [|
        [| [|0; 0; 0; |]; [|1; 0; 0; |]; [|0; 1; 0; |]; [|0; 1; 0; |]; [|1; 0; 0; |]; [|1; 1; 0; |]; |] // Left face
        [| [|0; 0; 0; |]; [|1; 0; 0; |]; [|0; 0; 1; |]; [|1; 0; 0; |]; [|0; 0; 1; |]; [|1; 0; 1; |]; |] // Top face
        [| [|0; 0; 0; |]; [|0; 0; 1; |]; [|0; 1; 0; |]; [|0; 0; 1; |]; [|0; 1; 0; |]; [|0; 1; 1; |]; |] // Front face
    |]

    let iterate (slices: ImageSlice[]) frontIndex isoValue addPoint = 
        let lastRow, lastColumn = slices.[frontIndex].Rows - 2, slices.[frontIndex].Columns - 2

        let jumpColumn, jumpRow = 1, slices.[frontIndex].Columns
        let gradient = Gradient(slices)
        
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
            let mutable contributingEdges = 0

            let cubeIndex = cube.GetIndex()
            let mutable dualEdges = 0

            if cubeIndex <> 0 then

                gradient.setValue (indexSecond, bLeft, &cube.Gradients.[0])
                gradient.setValue (indexSecond, bRight, &cube.Gradients.[1])
                gradient.setValue (indexFirst, bRight, &cube.Gradients.[2])
                gradient.setValue (indexFirst, bLeft, &cube.Gradients.[3])
                gradient.setValue (indexSecond, tLeft, &cube.Gradients.[4])
                gradient.setValue (indexSecond, tRight, &cube.Gradients.[5])
                gradient.setValue (indexFirst, tRight, &cube.Gradients.[6])
                gradient.setValue (indexFirst, tLeft, &cube.Gradients.[7])

                for i in 0..EdgeTraversal.Length-1 do
                    let indexA, indexB = int EdgeTraversal.[i].[0], int EdgeTraversal.[i].[1]
                    let edgeIndex = 1 <<< i
                    if ((EdgeTable.[cubeIndex] &&& edgeIndex) > 0) || (cube.Values.[indexA] = isoValue) then                        
                        let v1, v2 = cube.Values.[indexA], cube.Values.[indexB]
                        let delta = v2 - v1

                        let mu =
                            if delta = 0s then
                                0.5f
                            else
                                float32(isoValue - v1) / (float32 delta)

                        dualEdges <- dualEdges ||| edgeIndex
                        
                        contributingEdges <- contributingEdges + 1
                        bestFitVertex <- bestFitVertex + Vector3.Lerp(cube.Vertices.[indexA], cube.Vertices.[indexB], mu)
                        bestFitGradient <- bestFitGradient + Vector3.Lerp(cube.Gradients.[indexA], cube.Gradients.[indexB], mu)

            if contributingEdges > 0 then
                bestFitVertex <-  bestFitVertex/(float32 contributingEdges) 

            {
                DualEdges = dualEdges
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

        let addQuad quadIndex (row, column) =
            let triangleVertices = QuadsTraversal.[quadIndex]            

            for i in 0..triangleVertices.Length-1 do
                let innerVertex = innerVertices.[triangleVertices.[i].[0]].[row + triangleVertices.[i].[1]].[column + triangleVertices.[i].[2]]
                innerVertex.Position |> addPoint
                innerVertex.Gradient |> addPoint

        for row in 0..lastRow do
            for column in 0..lastColumn do
                let edgeIndex = innerVertices.[0].[row].[column].DualEdges
                for i in 0..XYZEdges.Length-1 do
                    if edgeIndex &&& XYZEdges.[i].[1] > 0 then
                        addQuad i (row, column)

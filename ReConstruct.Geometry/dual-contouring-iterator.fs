namespace ReConstruct.Geometry

open System
open System.Numerics

open ReConstruct.Data.Dicom

open ReConstruct.Geometry.MarchingCubesTables

[<Struct>]
type DualCell = 
    {
        DualEdges: int
        Position: Vector3
        Gradient: Vector3
    }

module DualContouringIterator =

    let private XYZEdges = [| 
        [| 1 <<< 0; 0; |]; //(0, 1) -> 0, x axis edge.
        [| 1 <<< 9; 9; |]; //(1, 5) -> 9, y axis edge.
        [| 1 <<< 1; 1; |]; //(1, 2) -> 1, z axis edge.
    |]

    let private EdgeMasks = [|
        1 <<< 0;
        1 <<< 1;
        1 <<< 2;
        1 <<< 3;
        1 <<< 4;
        1 <<< 5;
        1 <<< 6;
        1 <<< 7;
        1 <<< 8;
        1 <<< 9;
        1 <<< 10;
        1 <<< 11;
    |]

    let private QuadsTraversal = [|
        [| [|0; 0; 0; |]; [|1; 0; 0; |]; [|0; 1; 0; |]; [|0; 1; 0; |]; [|1; 0; 0; |]; [|1; 1; 0; |]; |] // Left face
        [| [|0; 0; 0; |]; [|1; 0; 0; |]; [|0; 0; 1; |]; [|1; 0; 0; |]; [|0; 0; 1; |]; [|1; 0; 1; |]; |] // Top face
        [| [|0; 0; 0; |]; [|0; 0; 1; |]; [|0; 1; 0; |]; [|0; 0; 1; |]; [|0; 1; 0; |]; [|0; 1; 1; |]; |] // Front face
    |]

    let private edgeContributions cubeIndex = 
        seq { 0..EdgeTraversal.Length-1 } 
            |> Seq.filter(fun i -> (EdgeTable.[cubeIndex] &&& (1 <<< i)) > 0)  
            |> Seq.toArray

    let private contributions() = seq { 0..255 } |> Seq.map edgeContributions |> Seq.toArray 

    let private EdgeContributions =
        [|[||]; [|0; 3; 8|]; [|0; 1; 9|]; [|1; 3; 8; 9|]; [|1; 2; 10|];
        [|0; 1; 2; 3; 8; 10|]; [|0; 2; 9; 10|]; [|2; 3; 8; 9; 10|]; [|2; 3; 11|];
        [|0; 2; 8; 11|]; [|0; 1; 2; 3; 9; 11|]; [|1; 2; 8; 9; 11|];
        [|1; 3; 10; 11|]; [|0; 1; 8; 10; 11|]; [|0; 3; 9; 10; 11|];
        [|8; 9; 10; 11|]; [|4; 7; 8|]; [|0; 3; 4; 7|]; [|0; 1; 4; 7; 8; 9|];
        [|1; 3; 4; 7; 9|]; [|1; 2; 4; 7; 8; 10|]; [|0; 1; 2; 3; 4; 7; 10|];
        [|0; 2; 4; 7; 8; 9; 10|]; [|2; 3; 4; 7; 9; 10|]; [|2; 3; 4; 7; 8; 11|];
        [|0; 2; 4; 7; 11|]; [|0; 1; 2; 3; 4; 7; 8; 9; 11|]; [|1; 2; 4; 7; 9; 11|];
        [|1; 3; 4; 7; 8; 10; 11|]; [|0; 1; 4; 7; 10; 11|];
        [|0; 3; 4; 7; 8; 9; 10; 11|]; [|4; 7; 9; 10; 11|]; [|4; 5; 9|];
        [|0; 3; 4; 5; 8; 9|]; [|0; 1; 4; 5|]; [|1; 3; 4; 5; 8|];
        [|1; 2; 4; 5; 9; 10|]; [|0; 1; 2; 3; 4; 5; 8; 9; 10|]; [|0; 2; 4; 5; 10|];
        [|2; 3; 4; 5; 8; 10|]; [|2; 3; 4; 5; 9; 11|]; [|0; 2; 4; 5; 8; 9; 11|];
        [|0; 1; 2; 3; 4; 5; 11|]; [|1; 2; 4; 5; 8; 11|]; [|1; 3; 4; 5; 9; 10; 11|];
        [|0; 1; 4; 5; 8; 9; 10; 11|]; [|0; 3; 4; 5; 10; 11|]; [|4; 5; 8; 10; 11|];
        [|5; 7; 8; 9|]; [|0; 3; 5; 7; 9|]; [|0; 1; 5; 7; 8|]; [|1; 3; 5; 7|];
        [|1; 2; 5; 7; 8; 9; 10|]; [|0; 1; 2; 3; 5; 7; 9; 10|];
        [|0; 2; 5; 7; 8; 10|]; [|2; 3; 5; 7; 10|]; [|2; 3; 5; 7; 8; 9; 11|];
        [|0; 2; 5; 7; 9; 11|]; [|0; 1; 2; 3; 5; 7; 8; 11|]; [|1; 2; 5; 7; 11|];
        [|1; 3; 5; 7; 8; 9; 10; 11|]; [|0; 1; 5; 7; 9; 10; 11|];
        [|0; 3; 5; 7; 8; 10; 11|]; [|5; 7; 10; 11|]; [|5; 6; 10|];
        [|0; 3; 5; 6; 8; 10|]; [|0; 1; 5; 6; 9; 10|]; [|1; 3; 5; 6; 8; 9; 10|];
        [|1; 2; 5; 6|]; [|0; 1; 2; 3; 5; 6; 8|]; [|0; 2; 5; 6; 9|];
        [|2; 3; 5; 6; 8; 9|]; [|2; 3; 5; 6; 10; 11|]; [|0; 2; 5; 6; 8; 10; 11|];
        [|0; 1; 2; 3; 5; 6; 9; 10; 11|]; [|1; 2; 5; 6; 8; 9; 10; 11|];
        [|1; 3; 5; 6; 11|]; [|0; 1; 5; 6; 8; 11|]; [|0; 3; 5; 6; 9; 11|];
        [|5; 6; 8; 9; 11|]; [|4; 5; 6; 7; 8; 10|]; [|0; 3; 4; 5; 6; 7; 10|];
        [|0; 1; 4; 5; 6; 7; 8; 9; 10|]; [|1; 3; 4; 5; 6; 7; 9; 10|];
        [|1; 2; 4; 5; 6; 7; 8|]; [|0; 1; 2; 3; 4; 5; 6; 7|];
        [|0; 2; 4; 5; 6; 7; 8; 9|]; [|2; 3; 4; 5; 6; 7; 9|];
        [|2; 3; 4; 5; 6; 7; 8; 10; 11|]; [|0; 2; 4; 5; 6; 7; 10; 11|];
        [|0; 1; 2; 3; 4; 5; 6; 7; 8; 9; 10; 11|]; [|1; 2; 4; 5; 6; 7; 9; 10; 11|];
        [|1; 3; 4; 5; 6; 7; 8; 11|]; [|0; 1; 4; 5; 6; 7; 11|];
        [|0; 3; 4; 5; 6; 7; 8; 9; 11|]; [|4; 5; 6; 7; 9; 11|]; [|4; 6; 9; 10|];
        [|0; 3; 4; 6; 8; 9; 10|]; [|0; 1; 4; 6; 10|]; [|1; 3; 4; 6; 8; 10|];
        [|1; 2; 4; 6; 9|]; [|0; 1; 2; 3; 4; 6; 8; 9|]; [|0; 2; 4; 6|];
        [|2; 3; 4; 6; 8|]; [|2; 3; 4; 6; 9; 10; 11|]; [|0; 2; 4; 6; 8; 9; 10; 11|];
        [|0; 1; 2; 3; 4; 6; 10; 11|]; [|1; 2; 4; 6; 8; 10; 11|];
        [|1; 3; 4; 6; 9; 11|]; [|0; 1; 4; 6; 8; 9; 11|]; [|0; 3; 4; 6; 11|];
        [|4; 6; 8; 11|]; [|6; 7; 8; 9; 10|]; [|0; 3; 6; 7; 9; 10|];
        [|0; 1; 6; 7; 8; 10|]; [|1; 3; 6; 7; 10|]; [|1; 2; 6; 7; 8; 9|];
        [|0; 1; 2; 3; 6; 7; 9|]; [|0; 2; 6; 7; 8|]; [|2; 3; 6; 7|];
        [|2; 3; 6; 7; 8; 9; 10; 11|]; [|0; 2; 6; 7; 9; 10; 11|];
        [|0; 1; 2; 3; 6; 7; 8; 10; 11|]; [|1; 2; 6; 7; 10; 11|];
        [|1; 3; 6; 7; 8; 9; 11|]; [|0; 1; 6; 7; 9; 11|]; [|0; 3; 6; 7; 8; 11|];
        [|6; 7; 11|]; [|6; 7; 11|]; [|0; 3; 6; 7; 8; 11|]; [|0; 1; 6; 7; 9; 11|];
        [|1; 3; 6; 7; 8; 9; 11|]; [|1; 2; 6; 7; 10; 11|];
        [|0; 1; 2; 3; 6; 7; 8; 10; 11|]; [|0; 2; 6; 7; 9; 10; 11|];
        [|2; 3; 6; 7; 8; 9; 10; 11|]; [|2; 3; 6; 7|]; [|0; 2; 6; 7; 8|];
        [|0; 1; 2; 3; 6; 7; 9|]; [|1; 2; 6; 7; 8; 9|]; [|1; 3; 6; 7; 10|];
        [|0; 1; 6; 7; 8; 10|]; [|0; 3; 6; 7; 9; 10|]; [|6; 7; 8; 9; 10|];
        [|4; 6; 8; 11|]; [|0; 3; 4; 6; 11|]; [|0; 1; 4; 6; 8; 9; 11|];
        [|1; 3; 4; 6; 9; 11|]; [|1; 2; 4; 6; 8; 10; 11|];
        [|0; 1; 2; 3; 4; 6; 10; 11|]; [|0; 2; 4; 6; 8; 9; 10; 11|];
        [|2; 3; 4; 6; 9; 10; 11|]; [|2; 3; 4; 6; 8|]; [|0; 2; 4; 6|];
        [|0; 1; 2; 3; 4; 6; 8; 9|]; [|1; 2; 4; 6; 9|]; [|1; 3; 4; 6; 8; 10|];
        [|0; 1; 4; 6; 10|]; [|0; 3; 4; 6; 8; 9; 10|]; [|4; 6; 9; 10|];
        [|4; 5; 6; 7; 9; 11|]; [|0; 3; 4; 5; 6; 7; 8; 9; 11|];
        [|0; 1; 4; 5; 6; 7; 11|]; [|1; 3; 4; 5; 6; 7; 8; 11|];
        [|1; 2; 4; 5; 6; 7; 9; 10; 11|]; [|0; 1; 2; 3; 4; 5; 6; 7; 8; 9; 10; 11|];
        [|0; 2; 4; 5; 6; 7; 10; 11|]; [|2; 3; 4; 5; 6; 7; 8; 10; 11|];
        [|2; 3; 4; 5; 6; 7; 9|]; [|0; 2; 4; 5; 6; 7; 8; 9|];
        [|0; 1; 2; 3; 4; 5; 6; 7|]; [|1; 2; 4; 5; 6; 7; 8|];
        [|1; 3; 4; 5; 6; 7; 9; 10|]; [|0; 1; 4; 5; 6; 7; 8; 9; 10|];
        [|0; 3; 4; 5; 6; 7; 10|]; [|4; 5; 6; 7; 8; 10|]; [|5; 6; 8; 9; 11|];
        [|0; 3; 5; 6; 9; 11|]; [|0; 1; 5; 6; 8; 11|]; [|1; 3; 5; 6; 11|];
        [|1; 2; 5; 6; 8; 9; 10; 11|]; [|0; 1; 2; 3; 5; 6; 9; 10; 11|];
        [|0; 2; 5; 6; 8; 10; 11|]; [|2; 3; 5; 6; 10; 11|]; [|2; 3; 5; 6; 8; 9|];
        [|0; 2; 5; 6; 9|]; [|0; 1; 2; 3; 5; 6; 8|]; [|1; 2; 5; 6|];
        [|1; 3; 5; 6; 8; 9; 10|]; [|0; 1; 5; 6; 9; 10|]; [|0; 3; 5; 6; 8; 10|];
        [|5; 6; 10|]; [|5; 7; 10; 11|]; [|0; 3; 5; 7; 8; 10; 11|];
        [|0; 1; 5; 7; 9; 10; 11|]; [|1; 3; 5; 7; 8; 9; 10; 11|];
        [|1; 2; 5; 7; 11|]; [|0; 1; 2; 3; 5; 7; 8; 11|]; [|0; 2; 5; 7; 9; 11|];
        [|2; 3; 5; 7; 8; 9; 11|]; [|2; 3; 5; 7; 10|]; [|0; 2; 5; 7; 8; 10|];
        [|0; 1; 2; 3; 5; 7; 9; 10|]; [|1; 2; 5; 7; 8; 9; 10|]; [|1; 3; 5; 7|];
        [|0; 1; 5; 7; 8|]; [|0; 3; 5; 7; 9|]; [|5; 7; 8; 9|]; [|4; 5; 8; 10; 11|];
        [|0; 3; 4; 5; 10; 11|]; [|0; 1; 4; 5; 8; 9; 10; 11|];
        [|1; 3; 4; 5; 9; 10; 11|]; [|1; 2; 4; 5; 8; 11|]; [|0; 1; 2; 3; 4; 5; 11|];
        [|0; 2; 4; 5; 8; 9; 11|]; [|2; 3; 4; 5; 9; 11|]; [|2; 3; 4; 5; 8; 10|];
        [|0; 2; 4; 5; 10|]; [|0; 1; 2; 3; 4; 5; 8; 9; 10|]; [|1; 2; 4; 5; 9; 10|];
        [|1; 3; 4; 5; 8|]; [|0; 1; 4; 5|]; [|0; 3; 4; 5; 8; 9|]; [|4; 5; 9|];
        [|4; 7; 9; 10; 11|]; [|0; 3; 4; 7; 8; 9; 10; 11|]; [|0; 1; 4; 7; 10; 11|];
        [|1; 3; 4; 7; 8; 10; 11|]; [|1; 2; 4; 7; 9; 11|];
        [|0; 1; 2; 3; 4; 7; 8; 9; 11|]; [|0; 2; 4; 7; 11|]; [|2; 3; 4; 7; 8; 11|];
        [|2; 3; 4; 7; 9; 10|]; [|0; 2; 4; 7; 8; 9; 10|]; [|0; 1; 2; 3; 4; 7; 10|];
        [|1; 2; 4; 7; 8; 10|]; [|1; 3; 4; 7; 9|]; [|0; 1; 4; 7; 8; 9|];
        [|0; 3; 4; 7|]; [|4; 7; 8|]; [|8; 9; 10; 11|]; [|0; 3; 9; 10; 11|];
        [|0; 1; 8; 10; 11|]; [|1; 3; 10; 11|]; [|1; 2; 8; 9; 11|];
        [|0; 1; 2; 3; 9; 11|]; [|0; 2; 8; 11|]; [|2; 3; 11|]; [|2; 3; 8; 9; 10|];
        [|0; 2; 9; 10|]; [|0; 1; 2; 3; 8; 10|]; [|1; 2; 10|]; [|1; 3; 8; 9|];
        [|0; 1; 9|]; [|0; 3; 8|]; [||]|]

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
            let mutable contributingEdges = 0

            let cubeIndex = cube.GetIndex()
            let mutable dualEdges = 0

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

                dualEdges <- dualEdges ||| (1 <<< n)

                gradient.setValue (indexFirst + positions.[indexA].[0], tLeft + positions.[indexA].[1], &cube.Gradients.[indexA])
                gradient.setValue (indexFirst + positions.[indexB].[0], tLeft + positions.[indexB].[1], &cube.Gradients.[indexB])

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

        for row in 0..lastRow do
            for column in 0..lastColumn do
                let edgeIndex = innerVertices.[0].[row].[column].DualEdges
                for i in 0..XYZEdges.Length-1 do                
                    let edgeSign = (edgeIndex &&& XYZEdges.[i].[0]) >>> XYZEdges.[i].[1]

                    for j in 0..(edgeSign*QuadsTraversal.[i].Length)-1 do
                        let triangle = QuadsTraversal.[i].[j]
                        let innerVertex = innerVertices.[triangle.[0]].[row + triangle.[1]].[column + triangle.[2]]
                        innerVertex.Position |> addPoint
                        innerVertex.Gradient |> addPoint

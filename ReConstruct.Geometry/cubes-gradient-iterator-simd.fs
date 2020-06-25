namespace ReConstruct.Geometry

open System
open System.Numerics

open ReConstruct.Data.Dicom

open ReConstruct.Geometry.MarchingCubesTables

module CubesGradientIteratorSIMD =

    let iterate (front, back, next) isoValue addPoint = 
        let lastRow, lastColumn = front.Rows - 2, front.Columns - 2

        let cube = Cube.create front back isoValue

        let vertices = Array.zeroCreate<Vector3> 12
        let gradients = Array.zeroCreate<Vector3> 12

        //let mutable corners = Vector([| 0; 1; front.Columns; front.Columns + 1; |])
        let corners = [| 0; 1; front.Columns; front.Columns + 1; |]
        let step = Vector([| 2; front.Columns + 1; front.Columns; front.Columns; |])

        let neighbour = [| 0; 0; 0; 0; |]

        let processCube() =
            let topLeft = corners.[0]
            let topRight = corners.[1]
            let bottomLeft = corners.[2]
            let bottomRight = corners.[3]

            cube.Values.[0] <- back.HField.[bottomLeft]
            cube.Values.[1] <- back.HField.[bottomRight]
            cube.Values.[2] <- front.HField.[bottomRight]
            cube.Values.[3] <- front.HField.[bottomLeft]
            cube.Values.[4] <- back.HField.[topLeft]
            cube.Values.[5] <- back.HField.[topRight]
            cube.Values.[6] <- front.HField.[topRight]
            cube.Values.[7] <- front.HField.[topLeft]

            let cubeIndex = cube.GetIndex()

            if EdgeTable.[cubeIndex] <> 0 then

                //let neighbour = Vector.Add(corners, step)
                (Vector(corners) + step).CopyTo neighbour

                let underBottomLeft, underBottomRight = neighbour.[2], neighbour.[3]
                let rightBottomRight, rightTopRight = neighbour.[1], neighbour.[0]

                cube.Gradients.[0].X <- (back.HField.[bottomRight] - back.HField.[bottomLeft]) |> float32
                cube.Gradients.[0].Y <- (back.HField.[underBottomLeft] - back.HField.[bottomLeft]) |> float32
                cube.Gradients.[0].Z <- (next.HField.[bottomLeft] - back.HField.[bottomLeft]) |> float32

                cube.Gradients.[1].X <- (back.HField.[rightBottomRight] - back.HField.[bottomRight]) |> float32
                cube.Gradients.[1].Y <- (back.HField.[underBottomRight] - back.HField.[bottomRight]) |> float32
                cube.Gradients.[1].Z <- (next.HField.[bottomRight] - back.HField.[bottomRight]) |> float32

                cube.Gradients.[2].X <- (front.HField.[rightBottomRight] - front.HField.[bottomRight]) |> float32
                cube.Gradients.[2].Y <- (front.HField.[underBottomRight] - front.HField.[bottomRight]) |> float32
                cube.Gradients.[2].Z <- (back.HField.[bottomRight] - front.HField.[bottomRight]) |> float32

                cube.Gradients.[3].X <- (front.HField.[bottomRight] - front.HField.[bottomLeft]) |> float32
                cube.Gradients.[3].Y <- (front.HField.[underBottomLeft] - front.HField.[bottomLeft]) |> float32
                cube.Gradients.[3].Z <- (back.HField.[bottomLeft] - front.HField.[bottomLeft]) |> float32

                cube.Gradients.[4].X <- (back.HField.[topRight] - back.HField.[topLeft]) |> float32
                cube.Gradients.[4].Y <- (back.HField.[bottomLeft] - back.HField.[topLeft]) |> float32
                cube.Gradients.[4].Z <- (next.HField.[topLeft] - back.HField.[topLeft]) |> float32

                cube.Gradients.[5].X <- (back.HField.[rightTopRight] - back.HField.[topRight]) |> float32
                cube.Gradients.[5].Y <- (back.HField.[bottomRight] - back.HField.[topRight]) |> float32
                cube.Gradients.[5].Z <- (next.HField.[topRight] - back.HField.[topRight]) |> float32

                cube.Gradients.[6].X <- (front.HField.[rightTopRight] - front.HField.[topRight]) |> float32
                cube.Gradients.[6].Y <- (front.HField.[bottomRight] - front.HField.[topRight]) |> float32
                cube.Gradients.[6].Z <- (back.HField.[topRight] - front.HField.[topRight]) |> float32

                cube.Gradients.[7].X <- (front.HField.[topRight] - front.HField.[topLeft]) |> float32
                cube.Gradients.[7].Y <- (front.HField.[bottomLeft] - front.HField.[topLeft]) |> float32
                cube.Gradients.[7].Z <- (back.HField.[topLeft] - front.HField.[topLeft]) |> float32
        
                for i in 0..EdgeTraversal.Length-1 do
                    if (EdgeTable.[cubeIndex] &&& (1 <<< i)) > 0 then
                        let index1, index2 = int EdgeTraversal.[i].[0], int EdgeTraversal.[i].[1]
                        let v1, v2 = cube.Values.[index1], cube.Values.[index2]
                        let delta = v2 - v1

                        let mu =
                            if delta = 0s then
                                0.5f
                            else
                                float32(isoValue - v1) / (float32 delta)
                        vertices.[i] <- Vector3.Lerp(cube.Vertices.[index1], cube.Vertices.[index2], mu)
                        gradients.[i] <- Vector3.Lerp(cube.Gradients.[index1], cube.Gradients.[index2], mu)

                let triangles = TriTable2.[cubeIndex]

                for triangle in triangles do
                    vertices.[triangle.[0]] |> addPoint
                    gradients.[triangle.[0]] |> addPoint
                    
                    vertices.[triangle.[1]] |> addPoint
                    gradients.[triangle.[1]] |> addPoint

                    vertices.[triangle.[2]] |> addPoint
                    gradients.[triangle.[2]] |> addPoint


        let left = float32 front.UpperLeft.[0]
        let right = left + front.PixelSpacing.X

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
                processCube()
                //corners <- Vector.Add(corners, Vector.One)
                (Vector(corners) + Vector.One).CopyTo corners

                for n in 0..7 do
                    cube.Vertices.[n].X <- cube.Vertices.[n].X + front.PixelSpacing.X
            
            //corners <- Vector.Add(corners, Vector.One)
            (Vector(corners) + Vector.One).CopyTo corners
            
            for n in 0..7 do
                cube.Vertices.[n].Y <- cube.Vertices.[n].Y + front.PixelSpacing.Y
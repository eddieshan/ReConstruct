namespace ReConstruct.Geometry

open System
open System.Numerics

open ReConstruct.Data.Dicom

open ReConstruct.Geometry.MarchingCubesTables

module CubesGradientIterator =

    type Cube =
        {
            Values: int16[]
            Vertices: Vector3[]
            Gradients: Vector3[]
        }

    let newCube front back =
        let zFront, zBack = float32 front.UpperLeft.[2], float32 back.UpperLeft.[2]
        let stepY = float32 front.PixelSpacingY

        let top = float32 front.UpperLeft.[1]
        let bottom = top + stepY

        {
            Values = Array.zeroCreate<int16> 8
            Vertices = [|
                Vector3(0.0f, bottom, zBack)
                Vector3(0.0f, bottom, zBack)
                Vector3(0.0f, bottom, zFront)
                Vector3(0.0f, bottom, zFront)
                Vector3(0.0f, top, zBack)
                Vector3(0.0f, top, zBack)
                Vector3(0.0f, top, zFront)
                Vector3(0.0f, top, zFront)
            |]
            Gradients = Array.zeroCreate<Vector3> 8
        }

    let iterate (front, back, next) isoValue addPoint = 
        let lastRow, lastColumn = front.Rows - 2, front.Columns - 2

        let cube = newCube front back

        let getCubeIndex() =
            let mutable cubeIndex = 0uy
            for i in 0..cube.Values.Length-1 do
                if (cube.Values.[i] <= isoValue) then
                    cubeIndex <- cubeIndex ||| (1uy <<< i)
            cubeIndex |> int

        let jumpRight = 1
        let jumpBottom = front.Columns
        let jumpBottomRight = jumpBottom + jumpRight

        let processCube topLeft (row, column) =
            let topRight = topLeft + jumpRight
            let bottomLeft = topLeft + jumpBottom
            let bottomRight = topLeft + jumpBottomRight

            cube.Values.[0] <- back.HField.[bottomLeft]
            cube.Values.[1] <- back.HField.[bottomRight]
            cube.Values.[2] <- front.HField.[bottomRight]
            cube.Values.[3] <- front.HField.[bottomLeft]
            cube.Values.[4] <- back.HField.[topLeft]
            cube.Values.[5] <- back.HField.[topRight]
            cube.Values.[6] <- front.HField.[topRight]
            cube.Values.[7] <- front.HField.[topLeft]

            let cubeIndex = getCubeIndex()

            if EdgeTable.[cubeIndex] <> 0 then

                let jumpUnder = (1 - row/lastRow) * jumpBottom
                let underBottomLeft, underBottomRight = bottomLeft + jumpUnder, bottomRight + jumpUnder

                let jumpNextRight = (1- column/lastColumn) * jumpRight
                let rightBottomRight, rightTopRight = bottomRight + jumpNextRight, topRight + jumpNextRight

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

                let vertices = Array.zeroCreate<Vector3> 12
                let gradients = Array.zeroCreate<Vector3> 12
        
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
                        //gradients.[i] <- Vector3.Lerp(cube.Gradients.[index1], cube.Gradients.[index2], mu) |> Vector3.Normalize
                        gradients.[i] <- Vector3.Lerp(cube.Gradients.[index1], cube.Gradients.[index2], mu)

                let triangles = TriTable2.[cubeIndex]

                for triangle in triangles do
                    let v0 = vertices.[triangle.[0]]
                    let v1 = vertices.[triangle.[1]]
                    let v2 = vertices.[triangle.[2]]

                    let g0 = gradients.[triangle.[0]]
                    let g1 = gradients.[triangle.[1]]
                    let g2 = gradients.[triangle.[2]]

                    addPoint v0
                    addPoint g0
                    addPoint v1
                    addPoint g1
                    addPoint v2
                    addPoint g2


        let mutable rowOffset = 0
        let stepX, stepY = float32 front.PixelSpacingX, float32 front.PixelSpacingY
        let left = float32 front.UpperLeft.[0]
        let right = left + stepX

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

                processCube (rowOffset + column) (row, column)

                for n in 0..7 do
                    cube.Vertices.[n].X <- cube.Vertices.[n].X + stepX

            for n in 0..7 do
                cube.Vertices.[n].Y <- cube.Vertices.[n].Y + stepY

            rowOffset <- rowOffset + front.Columns
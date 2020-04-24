﻿namespace ReConstruct.Geometry

open System
open System.Numerics

open ReConstruct.Data.Dicom

module CubesGradientIterator =

    type Cube =
        {
            IsoValue: int16
            Vertices: Vector3[]
            Levels: int16[]
            Gradients: Vector3[]
        }

    let iterate (front, back, next) isoValue polygonize = 

        let zFront, zBack = float32 front.UpperLeft.[2], float32 back.UpperLeft.[2]
        let stepX, stepY = float32 front.PixelSpacingX, float32 front.PixelSpacingY

        let top, left = float32 front.UpperLeft.[1], float32 front.UpperLeft.[0]
        let bottom, right = top + stepY, left + stepX

        let lastRow = front.Rows - 2
        let lastColumn = front.Columns - 2

        let cube = 
            {
                IsoValue = isoValue
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
                Levels = Array.zeroCreate<int16> 8
                Gradients = Array.zeroCreate<Vector3> 8
             }

        let jumpRight = 1
        let jumpRightRight = jumpRight + jumpRight
        let jumpBottom = front.Columns
        let jumpUnder = 2*front.Columns
        let jumpUnderRight = jumpUnder + jumpRight
        let jumpBottomRight = jumpBottom + jumpRight
        let jumpBottomRightRight = jumpBottomRight + jumpRight

        let setValues topLeft (row, column) =
            let topRight = topLeft + jumpRight
            let bottomLeft = topLeft + jumpBottom
            let bottomRight = topLeft + jumpBottomRight

            let underBottomLeft, underBottomRight = 
                if row < lastRow then
                    topLeft + jumpUnder, topLeft + jumpUnderRight
                else
                    bottomLeft, bottomRight

            let rightBottomRight, rightTopRight = 
                if column < lastColumn then
                    topLeft + jumpBottomRightRight, topLeft + jumpRightRight
                else
                    bottomRight, topRight

            cube.Levels.[0] <- back.HField.[bottomLeft]
            cube.Levels.[1] <- back.HField.[bottomRight]
            cube.Levels.[2] <- front.HField.[bottomRight]
            cube.Levels.[3] <- front.HField.[bottomLeft]
            cube.Levels.[4] <- back.HField.[topLeft]
            cube.Levels.[5] <- back.HField.[topRight]
            cube.Levels.[6] <- front.HField.[topRight]
            cube.Levels.[7] <- front.HField.[topLeft]

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

                setValues (rowOffset + column) (row, column)
                polygonize cube

                for n in 0..7 do
                    cube.Vertices.[n].X <- cube.Vertices.[n].X + stepX

            for n in 0..7 do
                cube.Vertices.[n].Y <- cube.Vertices.[n].Y + stepY

            rowOffset <- rowOffset + front.Columns
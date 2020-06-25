namespace ReConstruct.Geometry

open System

open ReConstruct.Data.Dicom

module CubesIterator =

    let iterate (front, back) isoValue polygonize = 
        let left = float32 front.TopLeft.[0]
        let right = left + front.PixelSpacing.X

        let cube = Cube.create front back isoValue

        let setValues offset =
            let right = offset + 1
            let bottom = offset + front.Columns
            let bottomRight = bottom + 1

            cube.Values.[0] <- back.HField.[bottom]
            cube.Values.[1] <- back.HField.[bottomRight]
            cube.Values.[2] <- front.HField.[bottomRight]
            cube.Values.[3] <- front.HField.[bottom]
            cube.Values.[4] <- back.HField.[offset]
            cube.Values.[5] <- back.HField.[right]
            cube.Values.[6] <- front.HField.[right]
            cube.Values.[7] <- front.HField.[offset]


        let mutable rowOffset = 0
        for row in 0..front.Rows - 2 do
            cube.Vertices.[0].X <- left
            cube.Vertices.[1].X <- right
            cube.Vertices.[2].X <- right
            cube.Vertices.[3].X <- left
            cube.Vertices.[4].X <- left
            cube.Vertices.[5].X <- right
            cube.Vertices.[6].X <- right
            cube.Vertices.[7].X <- left

            for column in 0..front.Columns - 2 do

                setValues (rowOffset + column)
                polygonize cube

                for n in 0..7 do
                    cube.Vertices.[n].X <- cube.Vertices.[n].X + front.PixelSpacing.X

            for n in 0..7 do
                cube.Vertices.[n].Y <- cube.Vertices.[n].Y + front.PixelSpacing.Y

            rowOffset <- rowOffset + front.Columns
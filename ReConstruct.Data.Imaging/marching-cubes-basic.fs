﻿namespace ReConstruct.Data.Imaging

open System
open System.Numerics

open ReConstruct.Data.Imaging.CubesIterator
open ReConstruct.Data.Imaging.MarchingCubesTables

module MarchingCubesBasic =

    let private lerpVertex (index1: int, index2: int) (x: Cube) =
        let tolerance = 0.00001f

        let val1 = float32 x.Levels.[index1]
        let val2 = float32 x.Levels.[index2]

        let point1 = x.Vertices.[index1]
        let point2 = x.Vertices.[index2]

        if Math.Abs(x.IsoLevel - val1) < tolerance then
            point1   
        elif Math.Abs(x.IsoLevel - val2) < tolerance then
            point2
        elif Math.Abs(val1 - val2) < tolerance then
            point1
        else
            let mu = (x.IsoLevel - val1) / (val2 - val1)
            let x = point1.X + mu * (point2.X - point1.X)
            let y = point1.Y + mu * (point2.Y - point1.Y)
            let z = point1.Z + mu * (point2.Z - point1.Z)
            Vector3(x, y, z)

    let private marchCube (x: Cube, row, column) =

        let mutable cubeIndex = 0
        let intIsoLevel = int x.IsoLevel

        for i in 0..x.Levels.Length-1 do
            if (x.Levels.[i] <= intIsoLevel) then
                cubeIndex <- cubeIndex ||| (1 <<< i)

        if EdgeTable.[cubeIndex] <> 0 then
            let vertlist = Array.zeroCreate<Vector3> 12
            
            for i in 0..EdgeTraversal.Length-1 do
                if (EdgeTable.[cubeIndex] &&& (1 <<< i)) > 0 then
                    vertlist.[i] <- lerpVertex EdgeTraversal.[i] x

            let mutable index = 0
            while TriTable.[cubeIndex, index] <> -1 do
                let v0 = vertlist.[TriTable.[cubeIndex, index]]
                let v1 = vertlist.[TriTable.[cubeIndex, index + 1]]
                let v2 = vertlist.[TriTable.[cubeIndex, index + 2]]
                let normal = Vector3.Cross(v2 - v0, v1 - v0) |> Vector3.Normalize

                x.AddPoint v0
                x.AddPoint normal
                x.AddPoint v1
                x.AddPoint normal
                x.AddPoint v2
                x.AddPoint normal

                index <- index + 3

    let polygonize (front, back) isoLevel addPoint = 
        CubesIterator.iterate (front, back) isoLevel addPoint marchCube
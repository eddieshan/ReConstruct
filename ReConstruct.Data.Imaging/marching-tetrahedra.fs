namespace ReConstruct.Data.Imaging

open System
open System.Numerics

open ReConstruct.Data.Imaging.CubesIterator
open ReConstruct.Data.Imaging.MarchingTetrahedraTables

module MarchingTetrahedra =

    let lerpVertex(fValue1, fValue2, fValueDesired) =
        let tolerance = 0.00001f
        let fDelta = fValue2 - fValue1
    
        if fDelta < tolerance then 0.5f
        else (fValueDesired - fValue1)/fDelta

    let marchTetrahedron (vertices: Vector3[], values: float32[], v: Cube) =
        let mutable tetraIndex = 0
        let trianglesVertices = Array.zeroCreate<Vector3> 6

        for i in 0..3 do
            if values.[i] <= v.IsoLevel then
                tetraIndex <- tetraIndex ||| (1 <<< i)

        let edgeFlags = EdgeTable.[tetraIndex]

        if edgeFlags <> 0 then
            for i in 0..5 do
                if (edgeFlags &&& (1 <<< i) <> 0) then
                    let v0 = EdgeTraversal.[i].[0]
                    let v1 = EdgeTraversal.[i].[1]
                    let offset = lerpVertex(values.[v0], values.[v1], v.IsoLevel)
                    let inverseOffset = 1.0f - offset

                    trianglesVertices.[i].X <- inverseOffset*vertices.[v0].X + offset*vertices.[v1].X
                    trianglesVertices.[i].Y <- inverseOffset*vertices.[v0].Y + offset*vertices.[v1].Y
                    trianglesVertices.[i].Z <- inverseOffset*vertices.[v0].Z + offset*vertices.[v1].Z

            for i in 0..3..3 do
                if (TriTable.[tetraIndex].[i] >= 0) then
                    let v0 = trianglesVertices.[TriTable.[tetraIndex].[i]]
                    let v1 = trianglesVertices.[TriTable.[tetraIndex].[i + 1]]
                    let v2 = trianglesVertices.[TriTable.[tetraIndex].[i + 2]]
                    let normal = Vector3.Cross(v2 - v0, v1 - v0) |> Vector3.Normalize

                    v.AddPoint v0
                    v.AddPoint normal
                    v.AddPoint v1
                    v.AddPoint normal
                    v.AddPoint v2
                    v.AddPoint normal

    let marchCube (cube: Cube, row, column) =
            let tetraVertices = Array.zeroCreate<Vector3> 4
            let tetraValues = Array.zeroCreate<float32> 4
    
            for i in 0..5 do
                for j in 0..3 do
                    let cubeVertexIndex = TetraCubeIndices.[i].[j]
                    tetraVertices.[j].X <- cube.Vertices.[cubeVertexIndex].X
                    tetraVertices.[j].Y <- cube.Vertices.[cubeVertexIndex].Y
                    tetraVertices.[j].Z <- cube.Vertices.[cubeVertexIndex].Z
                    tetraValues.[j] <- float32 cube.Levels.[cubeVertexIndex]
                
                marchTetrahedron(tetraVertices, tetraValues, cube)

    let polygonize (front, back) isoLevel addPoint = 
        CubesIterator.iterate (front, back) isoLevel addPoint marchCube
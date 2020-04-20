namespace ReConstruct.Geometry

open System
open System.Buffers
open System.Numerics

open ReConstruct.Data.Dicom

open ReConstruct.Geometry.CubesIterator
open ReConstruct.Geometry.MarchingTetrahedraTables

module MarchingTetrahedra =

    let private bufferPool = ArrayPool<float32>.Shared
    let private capacity = 9000
    let private borrowBuffer() = bufferPool.Rent capacity

    let lerpVertex(v1, v2, isoLevel) =
        let delta = v2 - v1
        if delta = 0s then 
            0.5f
        else 
            float32(isoLevel - v1)/(float32 delta)

    let marchTetrahedron addPoint (vertices: Vector3[], values: int16[], v: Cube) =
        let mutable tetraIndex = 0
        let trianglesVertices = Array.zeroCreate<Vector3> 6

        for i in 0..3 do
            if values.[i] <= v.IsoValue then
                tetraIndex <- tetraIndex ||| (1 <<< i)

        let edgeFlags = EdgeTable.[tetraIndex]

        if edgeFlags <> 0 then
            for i in 0..5 do
                if (edgeFlags &&& (1 <<< i) <> 0) then
                    let v0 = EdgeTraversal.[i].[0]
                    let v1 = EdgeTraversal.[i].[1]
                    let offset = lerpVertex(values.[v0], values.[v1], v.IsoValue)
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

                    addPoint v0
                    addPoint normal
                    addPoint v1
                    addPoint normal
                    addPoint v2
                    addPoint normal

    let marchCube addPoint (cube: Cube) =
            let tetraVertices = Array.zeroCreate<Vector3> 4
            let tetraValues = Array.zeroCreate<int16> 4
    
            for i in 0..5 do
                for j in 0..3 do
                    let cubeVertexIndex = TetraCubeIndices.[i].[j]
                    tetraVertices.[j].X <- cube.Vertices.[cubeVertexIndex].X
                    tetraVertices.[j].Y <- cube.Vertices.[cubeVertexIndex].Y
                    tetraVertices.[j].Z <- cube.Vertices.[cubeVertexIndex].Z
                    tetraValues.[j] <- cube.Levels.[cubeVertexIndex]
                
                marchTetrahedron addPoint (tetraVertices, tetraValues, cube)

    let polygonize isoLevel (slices: ImageSlice[]) partialRender = 
        let queueJob = RenderAgent.renderQueue()

        let polygonizeSection (front, back) =
            let mutable currentBuffer, index = borrowBuffer(), 0
            let mutable bufferChain = List.empty

            let addPoint (p: System.Numerics.Vector3) = 
                if index = capacity then
                    bufferChain <- currentBuffer :: bufferChain
                    currentBuffer <- borrowBuffer()
                    index <- 0

                p.CopyTo(currentBuffer, index)
                index <- index + 3

            CubesIterator.iterate (front, back) isoLevel (marchCube addPoint)

            bufferChain <- currentBuffer :: bufferChain

            (index, capacity, bufferChain)

        let addPoints (index, capacity, bufferChain) =
            let lastBufferIndex = List.length bufferChain - 1
            let dumpBuffer i (buffer: float32[]) =
                let size = if i = 0 then index else capacity
                partialRender (size, buffer) (i = lastBufferIndex)
                bufferPool.Return buffer
            bufferChain |> List.iteri dumpBuffer

        let polygonizeJob i = async { return polygonizeSection (slices.[i - 1], slices.[i]) }

        seq { 1..slices.Length - 1 } |> Seq.iter(polygonizeJob >> (queueJob addPoints))
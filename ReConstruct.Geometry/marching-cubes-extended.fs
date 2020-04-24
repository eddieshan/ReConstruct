namespace ReConstruct.Geometry

open System
open System.Numerics
open System.Buffers

open ReConstruct.Core

open ReConstruct.Data.Dicom

open ReConstruct.Geometry.CubesGradientIterator
open ReConstruct.Geometry.MarchingCubesTables

module MarchingCubesExtended =

    let private bufferPool = ArrayPool<float32>.Shared
    let private capacity = 9000
    let private borrowBuffer() = bufferPool.Rent capacity

    let private marchCube addPoint (cube: Cube) =

        let mutable cubeIndex = 0uy

        for i in 0..cube.Levels.Length-1 do
            if (cube.Levels.[i] <= cube.IsoValue) then
                cubeIndex <- cubeIndex ||| (1uy <<< i)

        let cubeIndexAsInt = int cubeIndex

        if EdgeTable.[cubeIndexAsInt] <> 0 then
            let vertices = Array.zeroCreate<Vector3> 12
            let gradients = Array.zeroCreate<Vector3> 12
            
            for i in 0..EdgeTraversal.Length-1 do
                if (EdgeTable.[cubeIndexAsInt] &&& (1 <<< i)) > 0 then
                    let index1, index2 = int EdgeTraversal.[i].[0], int EdgeTraversal.[i].[1]
                    let v1, v2 = cube.Levels.[index1], cube.Levels.[index2]
                    let delta = v2 - v1
                    let mu =
                        if delta = 0s then
                            0.5f
                        else
                            float32(cube.IsoValue - v1) / (float32 delta)
                    vertices.[i] <- cube.Vertices.[index1] + mu*(cube.Vertices.[index2] - cube.Vertices.[index1])
                    //gradients.[i] <- Vector3.Lerp(cube.Gradients.[index1], cube.Gradients.[index2], mu) |> Vector3.Normalize
                    gradients.[i] <- Vector3.Lerp(cube.Gradients.[index1], cube.Gradients.[index2], mu)

            let triangles = TriTable2.[cubeIndexAsInt]

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

    let polygonize isoLevel (slices: ImageSlice[]) partialRender = 

        let polygonizeSection (front, back, next) =
            let mutable currentBuffer, index = borrowBuffer(), 0
            let mutable bufferChain = List.empty

            let addPoint (p: Vector3) = 
                if index = capacity then
                    bufferChain <- currentBuffer :: bufferChain
                    currentBuffer <- borrowBuffer()
                    index <- 0

                p.CopyTo(currentBuffer, index)
                index <- index + 3

            CubesGradientIterator.iterate (front, back, next) isoLevel (marchCube addPoint)

            bufferChain <- currentBuffer :: bufferChain

            (index, capacity, bufferChain)

        let addPoints (index, capacity, bufferChain) =
            let lastBufferIndex = List.length bufferChain - 1
            let dumpBuffer i (buffer: float32[]) =
                let size = if i = 0 then index else capacity
                partialRender (size, buffer) (i = lastBufferIndex)
                bufferPool.Return buffer
            bufferChain |> List.iteri dumpBuffer

        let queueJob = RenderAgent.renderQueue()
        let polygonizeJob i = async { return polygonizeSection (slices.[i - 2], slices.[i - 1], slices.[i]) }
        
        seq { 2..slices.Length - 1 } |> Seq.iter(polygonizeJob >> (queueJob addPoints))
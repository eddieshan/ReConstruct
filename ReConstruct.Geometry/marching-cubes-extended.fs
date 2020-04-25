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

    let polygonize isoValue (slices: ImageSlice[]) partialRender = 

        let marchCube addPoint (cube: Cube) cubeIndex =

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

            CubesGradientIterator.iterate (front, back, next) isoValue (marchCube addPoint)

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
namespace ReConstruct.Geometry

open System
open System.Numerics
open System.Buffers

open ReConstruct.Core

open ReConstruct.Data.Dicom

module MarchingCubesExtended =

    let private bufferPool = ArrayPool<float32>.Shared
    let private capacity = 9000
    let private borrowBuffer() = bufferPool.Rent capacity

    let polygonize isoValue (slices: ImageSlice[]) partialRender = 

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

            CubesGradientIterator.iterate (front, back, next) isoValue addPoint

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
namespace ReConstruct.Geometry

open System
open System.Numerics

open ReConstruct.Core

open ReConstruct.Data.Dicom

module MarchingCubesExtended =

    let polygonize isoValue (slices: ImageSlice[]) partialRender = 

        let polygonizeSection (front, back, next) =
            let mutable currentBuffer, index = BufferPool.borrow(), 0
            let mutable bufferChain = List.empty

            let addPoint (p: Vector3) = 
                if index = BufferPool.Capacity then
                    bufferChain <- currentBuffer :: bufferChain
                    currentBuffer <- BufferPool.borrow()
                    index <- 0

                p.CopyTo(currentBuffer, index)
                index <- index + 3

            //CubesGradientIteratorSIMD.iterate (front, back, next) isoValue addPoint
            CubesGradientIterator.iterate (front, back, next) isoValue addPoint

            bufferChain <- currentBuffer :: bufferChain

            (index, BufferPool.Capacity, bufferChain)

        let addPoints (index, capacity, bufferChain) =
            let lastBufferIndex = List.length bufferChain - 1
            let dumpBuffer i (buffer: float32[]) =
                let size = if i = 0 then index else capacity
                partialRender (size, buffer) (i = lastBufferIndex)
                BufferPool.ret buffer
            bufferChain |> List.iteri dumpBuffer

        let queueJob = RenderAgent.renderQueue()
        let polygonizeJob i = async { return polygonizeSection (slices.[i - 2], slices.[i - 1], slices.[i]) }
        
        seq { 2..slices.Length - 1 } |> Seq.iter(polygonizeJob >> (queueJob addPoints))
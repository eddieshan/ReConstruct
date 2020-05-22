namespace ReConstruct.Geometry

open System

open ReConstruct.Core

open ReConstruct.Data.Dicom

module MarchingCubesExtended =

    let polygonize isoValue (slices: ImageSlice[]) partialRender = 

        let polygonizeSection (front, back, next) =
            let bufferChain = BufferChain()
            //CubesGradientIteratorSIMD.iterate (front, back, next) isoValue addPoint
            CubesGradientIterator.iterate (front, back, next) isoValue bufferChain.Add
            bufferChain

        let addPoints (bufferChain: BufferChain) = bufferChain.Dump partialRender

        let queueJob = RenderAgent.renderQueue()
        let polygonizeJob i = async { return polygonizeSection (slices.[i - 2], slices.[i - 1], slices.[i]) }
    
        seq { 2..slices.Length - 1 } |> Seq.iter(polygonizeJob >> (queueJob addPoints))
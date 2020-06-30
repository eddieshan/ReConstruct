namespace ReConstruct.Geometry

open System

open ReConstruct.Data.Dicom

module DualContouring =

    let polygonize isoValue (slices: ImageSlice[]) partialRender = 

        let volume = UniformVolume.create slices isoValue

        let polygonizeSection index =
            let bufferChain = BufferChain()
            DualContouringIterator.iterate volume index bufferChain.Add
            bufferChain

        let addPoints (bufferChain: BufferChain) = bufferChain.Dump partialRender

        let queueJob = RenderAgent.renderQueue()
        let polygonizeJob i = async { return polygonizeSection i }
    
        seq { 0..slices.Length - 3 } |> Seq.iter(polygonizeJob >> (queueJob addPoints))
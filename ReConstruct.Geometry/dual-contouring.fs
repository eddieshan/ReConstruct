namespace ReConstruct.Geometry

open System

open ReConstruct.Data.Dicom

module DualContouring =

    let polygonize isoValue (slices: ImageSlice[]) partialRender = 

        let polygonizeSection index =
            let bufferChain = BufferChain()
            DualContouringIteratorSimd.iterate slices index isoValue bufferChain.Add
            bufferChain

        let addPoints (bufferChain: BufferChain) = bufferChain.Dump partialRender

        let queueJob = RenderAgent.renderQueue()
        let polygonizeJob i = async { return polygonizeSection i }
    
        seq { 0..slices.Length - 3 } |> Seq.iter(polygonizeJob >> (queueJob addPoints))
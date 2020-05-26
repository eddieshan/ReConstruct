namespace ReConstruct.Geometry

open System

open ReConstruct.Core

open ReConstruct.Data.Dicom

module MarchingCubesExtended =

    let polygonize isoValue (slices: ImageSlice[]) partialRender = 

        let polygonizeSection index =
            let bufferChain = BufferChain()
            //CubesGradientIteratorSIMD.iterate (front, back, next) isoValue addPoint
            CubesGradientIterator.iterate slices index isoValue bufferChain.Add
            bufferChain

        let addPoints (bufferChain: BufferChain) = bufferChain.Dump partialRender

        let queueJob = RenderAgent.renderQueue()
        let polygonizeJob i = async { return polygonizeSection i }
    
        seq { 0..slices.Length - 3 } |> Seq.iter(polygonizeJob >> (queueJob addPoints))
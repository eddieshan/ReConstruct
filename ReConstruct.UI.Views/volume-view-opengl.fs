namespace ReConstruct.UI.View

open System

open ReConstruct.Core
open ReConstruct.Core.Async

open System.Buffers
open System.Diagnostics

open System.Windows
open System.Windows.Forms.Integration

open ReConstruct.Data.Imaging
open ReConstruct.Data.Dicom

open ReConstruct.Render.OpenGL

open ReConstruct.UI.View

module VolumeViewOpenGL = 

    type UnitDelegate = delegate of unit -> unit

    module private RenderAgent =
        let private parallelThrottle = Environment.ProcessorCount - 1
        let private context = System.Threading.SynchronizationContext.Current

        let renderQueue() = 
            let throttledForkJoinAgent = Async.throttlingAgent parallelThrottle context
            ThrottledJob >> throttledForkJoinAgent.Post

    let getVolumeCenter firstSlice lastSlice =
        let x = firstSlice.SliceParams.UpperLeft.[0] + (firstSlice.SliceParams.PixelSpacing.X * (double firstSlice.SliceParams.Dimensions.Columns) / 2.0)
        let y = firstSlice.SliceParams.UpperLeft.[1] + (firstSlice.SliceParams.PixelSpacing.Y * (double firstSlice.SliceParams.Dimensions.Rows) / 2.0)
        let z = firstSlice.SliceParams.UpperLeft.[2] + (Math.Abs(lastSlice.SliceParams.UpperLeft.[2] - firstSlice.SliceParams.UpperLeft.[2]) / 2.0)
        (x, y, z)

    let mesh isoLevel (slices: CatSlice[]) partialRender = 
        let clock = Stopwatch.StartNew()
        let bufferPool = ArrayPool<float32>.Shared
        let queueJob = RenderAgent.renderQueue()

        let capacity = 900
        let borrowBuffer() = bufferPool.Rent capacity

        let polygonize (front, back) =
            let mutable currentBuffer, index = borrowBuffer(), 0
            let mutable bufferChain = List.empty

            let addPoint (p: System.Numerics.Vector3) = 
                if index = capacity then
                    bufferChain <- currentBuffer :: bufferChain
                    currentBuffer <- borrowBuffer()
                    index <- 0

                p.CopyTo(currentBuffer, index)
                index <- index + 3

            MarchingCubesBasic.polygonize (front, back) isoLevel addPoint
            //MarchingTetrahedra.polygonize (front, back) isoLevel addPoint
            
            bufferChain <- currentBuffer :: bufferChain

            (index, capacity, bufferChain)

        let addPoints (index, capacity, bufferChain) =
            let lastBufferIndex = List.length bufferChain - 1
            let dumpBuffer i (buffer: float32[]) =
                let size = if i = 0 then index else capacity
                partialRender (size, buffer) (i = lastBufferIndex)
                bufferPool.Return buffer
            bufferChain |> List.iteri dumpBuffer
            clock.Elapsed.TotalSeconds |> sprintf "%fs" |> Events.Status.Trigger

        let polygonizeJob i = (async { return polygonize (slices.[i - 1], slices.[i]) }, addPoints)

        seq { 1..slices.Length - 1 } |> Seq.iter(polygonizeJob >> queueJob)

    // Build Volume View from sequence of Slices. 
    let New isoLevel slices = 
        let firstSlice, lastSlice = slices |> Array.head, slices |> Array.last

        // Volume center is the centroid of the paralelogram defined between the first and last slice.
        let centroid = getVolumeCenter firstSlice lastSlice
        slices |> Array.iter(fun slice -> slice.SliceParams.AdjustToCenter centroid)
        let estimatedModelSize = Math.Abs(lastSlice.SliceParams.UpperLeft.[2] - firstSlice.SliceParams.UpperLeft.[2]) |> float32

        let progressiveMesh = mesh isoLevel slices

        RenderView.buildScene (estimatedModelSize, progressiveMesh)
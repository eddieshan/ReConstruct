namespace ReConstruct.UI.View

open System
open System.Diagnostics

open ReConstruct.Data.Imaging
open ReConstruct.Data.Dicom

open ReConstruct.Render.OpenGL

module VolumeViewOpenGL = 

    let getVolumeCenter firstSlice lastSlice =
        let x = firstSlice.SliceParams.UpperLeft.[0] + (firstSlice.SliceParams.PixelSpacing.X * (double firstSlice.SliceParams.Dimensions.Columns) / 2.0)
        let y = firstSlice.SliceParams.UpperLeft.[1] + (firstSlice.SliceParams.PixelSpacing.Y * (double firstSlice.SliceParams.Dimensions.Rows) / 2.0)
        let z = firstSlice.SliceParams.UpperLeft.[2] + (Math.Abs(lastSlice.SliceParams.UpperLeft.[2] - firstSlice.SliceParams.UpperLeft.[2]) / 2.0)
        (x, y, z)

    // Build Volume View from sequence of Slices. 
    let New isoLevel slices = 
        let firstSlice, lastSlice = slices |> Array.head, slices |> Array.last

        // Volume center is the centroid of the paralelogram defined between the first and last slice.
        let centroid = getVolumeCenter firstSlice lastSlice
        slices |> Array.iter(fun slice -> slice.SliceParams.AdjustToCenter centroid)
        let estimatedModelSize = Math.Abs(lastSlice.SliceParams.UpperLeft.[2] - firstSlice.SliceParams.UpperLeft.[2]) |> float32

        //let progressiveMesh = MarchingCubesBasic.polygonize isoLevel slices
        let calculateMesh partialRender =
            let clock = Stopwatch.StartNew()
            MarchingCubesZ.polygonize isoLevel slices partialRender
            clock.Elapsed.TotalSeconds |> sprintf "%fs" |> Events.Status.Trigger

        RenderView.buildScene (estimatedModelSize, calculateMesh)
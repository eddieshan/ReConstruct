namespace ReConstruct.UI.View

open System
open System.Diagnostics

open ReConstruct.Geometry
open ReConstruct.Data.Dicom

open ReConstruct.Render.OpenGL

module VolumeViewOpenGL = 

    let getVolumeCenter firstSlice lastSlice =
        let x = firstSlice.Layout.UpperLeft.[0] + (firstSlice.Layout.PixelSpacing.X * (double firstSlice.Layout.Dimensions.Columns) / 2.0)
        let y = firstSlice.Layout.UpperLeft.[1] + (firstSlice.Layout.PixelSpacing.Y * (double firstSlice.Layout.Dimensions.Rows) / 2.0)
        let z = firstSlice.Layout.UpperLeft.[2] + (Math.Abs(lastSlice.Layout.UpperLeft.[2] - firstSlice.Layout.UpperLeft.[2]) / 2.0)
        (x, y, z)

    // Build Volume View from sequence of Slices. 
    let New isoLevel slices = 
        let firstSlice, lastSlice = slices |> Array.head, slices |> Array.last

        // Volume center is the centroid of the paralelogram defined between the first and last slice.
        let centroid = getVolumeCenter firstSlice lastSlice
        slices |> Array.iter(fun slice -> slice.Layout.AdjustToCenter centroid)
        let estimatedModelSize = Math.Abs(lastSlice.Layout.UpperLeft.[2] - firstSlice.Layout.UpperLeft.[2]) |> float32

        //let progressiveMesh = MarchingCubesBasic.polygonize isoLevel slices
        let calculateMesh partialRender =
            let clock = Stopwatch.StartNew()
            MarchingCubesZ.polygonize isoLevel slices partialRender
            sprintf "%.2fs | %i triangles" clock.Elapsed.TotalSeconds (RenderView.totalTriangles()) |> Events.Status.Trigger

        RenderView.buildScene (estimatedModelSize, calculateMesh)
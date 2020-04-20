namespace ReConstruct.UI.View

open System
open System.Diagnostics

open ReConstruct.Geometry
open ReConstruct.Data.Dicom

open ReConstruct.Render.OpenGL

module VolumeViewOpenGL = 

    let getVolumeCenter firstSlice lastSlice =
        let x = firstSlice.UpperLeft.[0] + (firstSlice.PixelSpacingX * (double firstSlice.Columns) / 2.0)
        let y = firstSlice.UpperLeft.[1] + (firstSlice.PixelSpacingY * (double firstSlice.Rows) / 2.0)
        let z = firstSlice.UpperLeft.[2] + (Math.Abs(lastSlice.UpperLeft.[2] - firstSlice.UpperLeft.[2]) / 2.0)
        (x, y, z)

    // Build Volume View from sequence of Slices. 
    let New isoLevel slices = 
        let firstSlice, lastSlice = slices |> Array.head, slices |> Array.last

        // Volume center is the centroid of the paralelogram defined between the first and last slice.
        let centroid = getVolumeCenter firstSlice lastSlice
        slices |> Array.iter(fun slice -> slice.AdjustToCenter centroid)
        let estimatedModelSize = Math.Abs(lastSlice.UpperLeft.[2] - firstSlice.UpperLeft.[2]) |> float32

        let calculateMesh partialRender =
            MarchingCubesBasic.polygonize isoLevel slices partialRender
        
        let clock = Stopwatch.StartNew()
        let onUpdate numPoints =
            sprintf "%.2fs | %i triangles" clock.Elapsed.TotalSeconds numPoints |> Events.Status.Trigger
        RenderView.buildScene (estimatedModelSize, calculateMesh, onUpdate)
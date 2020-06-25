namespace ReConstruct.UI.View

open System
open System.Diagnostics

open ReConstruct.Geometry
open ReConstruct.Data.Dicom

open ReConstruct.Render.OpenGL

module VolumeViewOpenGL = 

    let getVolumeCenter firstSlice lastSlice =
        let x = firstSlice.TopLeft.[0] + ((float firstSlice.PixelSpacing.X) * (float firstSlice.Columns) / 2.0)
        let y = firstSlice.TopLeft.[1] + ((float firstSlice.PixelSpacing.Y) * (float firstSlice.Rows) / 2.0)
        let z = firstSlice.TopLeft.[2] + (Math.Abs(lastSlice.TopLeft.[2] - firstSlice.TopLeft.[2]) / 2.0)
        (x, y, z)

    // Build Volume View from sequence of Slices. 
    let New isoLevel slices = 
        let firstSlice, lastSlice = slices |> Array.head, slices |> Array.last

        // Volume center is the centroid of the paralelogram defined between the first and last slice.
        let centroid = getVolumeCenter firstSlice lastSlice
        slices |> Array.iter(ImageSlice.adjustToCenter centroid)
        let estimatedModelSize = Math.Abs(lastSlice.TopLeft.[2] - firstSlice.TopLeft.[2]) |> float32

        let calculateMesh partialRender =
            DualContouring.polygonize isoLevel slices partialRender
        
        let clock = Stopwatch.StartNew()
        let onUpdate numPoints =
            sprintf "%.2fs | %i triangles" clock.Elapsed.TotalSeconds (numPoints/3) |> Events.Status.Trigger
        RenderView.buildScene (estimatedModelSize, calculateMesh, onUpdate)
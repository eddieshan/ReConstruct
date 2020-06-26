namespace ReConstruct.UI.View

open System
open System.Numerics
open System.Diagnostics

open ReConstruct.Geometry
open ReConstruct.Data.Dicom

open ReConstruct.Render.OpenGL

module VolumeViewOpenGL = 

    let getVolumeCenter firstSlice lastSlice =
        let offset = Vector3(firstSlice.PixelSpacing.X * (float32 firstSlice.Columns) / 2.0f,
                            firstSlice.PixelSpacing.Y * (float32 firstSlice.Rows) / 2.0f,
                            Math.Abs(lastSlice.TopLeft.Z - firstSlice.TopLeft.Z) / 2.0f)
        firstSlice.TopLeft + offset

    // Build Volume View from sequence of Slices. 
    let New isoLevel slices = 
        let firstSlice, lastSlice = slices |> Array.head, slices |> Array.last

        // Volume center is the centroid of the paralelogram defined between the first and last slice.
        let centroid = getVolumeCenter firstSlice lastSlice
        let centeredSlices = slices |> Array.map(ImageSlice.adjustToCenter centroid)
        let estimatedModelSize = Math.Abs(lastSlice.TopLeft.Z - firstSlice.TopLeft.Z) |> float32

        let calculateMesh partialRender =
            DualContouring.polygonize isoLevel centeredSlices partialRender
        
        let clock = Stopwatch.StartNew()
        let onUpdate numPoints =
            sprintf "%.2fs | %i triangles" clock.Elapsed.TotalSeconds (numPoints/3) |> Events.Status.Trigger
        RenderView.buildScene (estimatedModelSize, calculateMesh, onUpdate)
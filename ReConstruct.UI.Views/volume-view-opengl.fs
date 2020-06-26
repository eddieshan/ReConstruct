namespace ReConstruct.UI.View

open System
open System.Numerics
open System.Diagnostics

open ReConstruct.Geometry
open ReConstruct.Data.Dicom

open ReConstruct.Render.OpenGL

module VolumeViewOpenGL = 

    let getVolumeCenter slice depth =
        let offset = Vector3(slice.PixelSpacing.X*(float32 slice.Columns), slice.PixelSpacing.Y*(float32 slice.Rows), depth)      
        slice.TopLeft + offset*Vector3(0.5f)

    // Build Volume View from sequence of Slices. 
    let New isoLevel slices = 
        let firstSlice, lastSlice = slices |> Array.head, slices |> Array.last

        // Volume center is the centroid of the paralelogram defined between the first and last slice.
        let depth = Math.Abs(lastSlice.TopLeft.Z - firstSlice.TopLeft.Z)
        let centroid = getVolumeCenter firstSlice depth        
        let centeredSlices = slices |> Array.map(ImageSlice.adjustToCenter centroid)        

        let calculateMesh partialRender =
            MarchingCubesExtended.polygonize isoLevel centeredSlices partialRender
        
        let clock = Stopwatch.StartNew()
        let onUpdate numPoints =
            sprintf "%.2fs | %i triangles" clock.Elapsed.TotalSeconds (numPoints/3) |> Events.Status.Trigger
        RenderView.buildScene (depth, calculateMesh, onUpdate)
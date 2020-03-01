namespace ReConstruct.UI.View

open System
open System.Diagnostics
open System.Windows
open System.Windows.Controls
open System.Windows.Media
open System.Windows.Media.Media3D

open ReConstruct.Core
open ReConstruct.Core.Async

open ReConstruct.Data.Dicom
open ReConstruct.Data.Imaging.MarchingCubes

module private RenderAgent =
    let private parallelThrottle = Environment.ProcessorCount - 1
    let private context = System.Threading.SynchronizationContext.Current
    let private throttledForkJoinAgent = Async.throttlingAgent parallelThrottle context
    let enqueueJob = ThrottledJob >> throttledForkJoinAgent.Post

// An event based status updater that detaches itself when render is done.
module Render =

    let updater() =

        let updateRenderStatus = new Event<int*int*float>()
        let mutable polygonizedCount = 1
        let mutable finished = false
        let mutable totalTriangles = 0

        let handleUpdate (slicesCount, numberOfTriangles, time) =
            polygonizedCount <- polygonizedCount + 1
            totalTriangles <- totalTriangles + numberOfTriangles
            let percentage = (float polygonizedCount)*100.0/(float slicesCount)
            finished <- polygonizedCount = slicesCount
            match finished with
            | true  -> Events.Status.Trigger(sprintf "%fs | %i triangles" time totalTriangles)
            | false -> Events.Status.Trigger(sprintf "Completed %i of %i : %.0f%% | %i triangles" polygonizedCount slicesCount percentage totalTriangles)

        let start() =
            Events.Status.Trigger("Rendering volume, please wait ...")
            updateRenderStatus.Publish.Subscribe handleUpdate

        let subscription = start()
        
        let updateOrStop (count, numberOfTriangles, time) = 
            if finished then
                subscription.Dispose()
            else
                updateRenderStatus.Trigger (count, numberOfTriangles, time)

        updateOrStop


// Slice series containing Hounsfield buffers are projected into section volumes using the marching cubes algorithm.
// The scene is set up based on the total volume size and position.
module VolumeView = 

    let renderLock = Object()

    let getVolumeCenter firstSlice lastSlice =
        let x = firstSlice.SliceParams.UpperLeft.[0] + (firstSlice.SliceParams.PixelSpacing.X * (double firstSlice.SliceParams.Dimensions.Columns) / 2.0)
        let y = firstSlice.SliceParams.UpperLeft.[1] + (firstSlice.SliceParams.PixelSpacing.Y * (double firstSlice.SliceParams.Dimensions.Rows) / 2.0)
        let z = firstSlice.SliceParams.UpperLeft.[2] + ((lastSlice.SliceParams.UpperLeft.[2] - firstSlice.SliceParams.UpperLeft.[2]) / 2.0)
        new Point3D(x, y, z)

    let buildScene estimatedSize =

        let distanceFactor, farPlaneFactor, fieldOfView = 0.90, 2.0, 70.0
        //let mutable cameraZ, zNear, zFar = estimatedSize*distanceFactor, 0.1, 1000.0
        let mutable cameraZ, zNear, zFar = estimatedSize*distanceFactor, 0.1, estimatedSize*farPlaneFactor

        let position = Point3D(0.0, 0.0, - cameraZ)
        let camera = PerspectiveCamera
                        (
                            UpDirection = Vector3D(0.0,-1.0,0.0),
                            FieldOfView = fieldOfView,
                            NearPlaneDistance = zNear,
                            Position = position,
                            LookDirection = Point3D(0.0, 0.0, 0.0) - position
                         )

        let viewPortLight = DirectionalLight(Color = Colors.White, Direction = camera.LookDirection)
        let lightModel = ModelVisual3D(Content = viewPortLight)
        let a3DGroup = Model3DGroup()
        let m3DModel = ModelVisual3D(Content = a3DGroup)
        let viewPort = Viewport3D(ClipToBounds = true, Camera = camera)
        seq { lightModel; m3DModel; } |> Seq.iter viewPort.Children.Add

        let addPoints (points: Point3DCollection) = 
            let mesh = new MeshGeometry3D(Positions = points)
            let geometryModel = GeometryModel3D(mesh, DiffuseMaterial(SolidColorBrush(Colors.LightGoldenrodYellow)))
            geometryModel.Transform <- Transform3DGroup()
            a3DGroup.Children.Add(geometryModel)

        let referenceCenter = 7.5        
        let mutable rotX, rotY, rotZ = 0.0f, 0.0f, 0.0f
        let mutable cameraShift = 0.0
        let center = new Point3D(referenceCenter, referenceCenter, referenceCenter)
        let axisX, axisY, axisZ = new Vector3D(1.0, 0.0, 0.0), new Vector3D(0.0, 1.0, 0.0), new Vector3D(0.0, 0.0, 1.0)

        let rotateTransform axisAngle = RotateTransform3D(axisAngle, center)

        let render() =

            let transforms = new Transform3DGroup()

            seq {
                (axisX, float rotX)
                (axisY, float rotY)
                (axisZ, float rotZ)
            } |> Seq.iter (AxisAngleRotation3D >> rotateTransform >> transforms.Children.Add)

            m3DModel.Transform <- transforms
            viewPort.Camera.Transform <- new TranslateTransform3D(0.0, 0.0, cameraShift)

        let moveCamera zoomFactor = cameraShift <- cameraShift + zoomFactor*estimatedSize
        let rotationFactor = 60.0f

        let rotate (axis, delta) =
            match axis with
            | X -> rotX <- rotX + delta*rotationFactor
            | Y -> rotY <- rotY + delta*rotationFactor
            | Z -> rotZ <- rotZ + delta*rotationFactor

        let update transform t =
            transform t
            sprintf "%f distance | X %fdeg | Y %fdeg | Z %fdeg" cameraZ rotX rotY rotZ |> Events.Status.Trigger
            render()

        DatasetMainView.Camera.OnCameraMoved.Publish |> Event.add (float >> update moveCamera)
        DatasetMainView.Camera.OnRotation.Publish |> Event.add (update rotate)
  
        (viewPort :> UIElement, addPoints)

    let mesh isoLevel (slices: CatSlice[]) addPoints updateOrStop = 
        
        let clock = Stopwatch()
        clock.Start()
        let capacity = 10000

        let polygonize (front, back) =
            let points = Point3DCollection capacity
            polygonize (front, back) isoLevel (fun p -> lock renderLock (fun _ -> points.Add p))
            points.Freeze()
            points

        let addPointsWithStatus (points: Point3DCollection) =
            addPoints points
            updateOrStop (slices.Length, points.Count, clock.Elapsed.TotalSeconds)

        // Parallel throttling. Assume,
        // - n CPU cores,
        // - 1 CPU core for the UI thread,
        // - n - 1 CPU cores for building the mesh.
        // Fork batches of n - 1 parallel threads to build a section of the mesh.
        // Each thread updates the UI when finished.
        // Parallel threads have to be throttled to avoid thread contention.
        // That is, consuming from the thread pool a lot of threads that cannot be started.
        //let parallelThrottle = Environment.ProcessorCount - 1
        //let context = System.Threading.SynchronizationContext.Current
        let polygonizeJob i = (async { return polygonize (slices.[i - 1], slices.[i]) }, addPointsWithStatus)

        seq { 1..slices.Length - 1 } |> Seq.iter(polygonizeJob >> RenderAgent.enqueueJob)


    // Build Volume View from sequence of CT Slices. 
    let New isoLevel slices =

        let updateOrStop = Render.updater()
        
        let firstSlice = slices |> Array.head
        let lastSlice = slices |> Array.last
        
        let centerPoint = getVolumeCenter firstSlice lastSlice
        slices |> Array.iter(fun slice -> slice.SliceParams.AdjustToCenter(centerPoint))
            
        // Calculate the centroid of the volumen.
        let estimatedModelSize = lastSlice.SliceParams.UpperLeft.[2] - firstSlice.SliceParams.UpperLeft.[2]
        let viewPort, addPoints = buildScene estimatedModelSize
        
        mesh isoLevel slices addPoints updateOrStop

        viewPort
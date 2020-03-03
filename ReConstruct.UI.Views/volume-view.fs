namespace ReConstruct.UI.View

open System
open System.Buffers
open System.Diagnostics
open System.Windows
open System.Windows.Controls
open System.Windows.Media
open System.Windows.Media.Media3D

open ReConstruct.Core
open ReConstruct.Core.Async

open ReConstruct.Data.Dicom
open ReConstruct.Data.Imaging.MarchingCubes


// Slice series containing Hounsfield buffers are projected into section volumes using the marching cubes algorithm.
// The scene is set up based on the total volume size and position.
module VolumeView = 

    module private RenderAgent =
        let private parallelThrottle = Environment.ProcessorCount - 1
        let private context = System.Threading.SynchronizationContext.Current
        let private throttledForkJoinAgent = Async.throttlingAgent parallelThrottle context
    
        let enqueueJob (job, continuation) = 
            (job, continuation) |> ThrottledJob |> throttledForkJoinAgent.Post

    let getVolumeCenter firstSlice lastSlice =
        let x = firstSlice.SliceParams.UpperLeft.[0] + (firstSlice.SliceParams.PixelSpacing.X * (double firstSlice.SliceParams.Dimensions.Columns) / 2.0)
        let y = firstSlice.SliceParams.UpperLeft.[1] + (firstSlice.SliceParams.PixelSpacing.Y * (double firstSlice.SliceParams.Dimensions.Rows) / 2.0)
        let z = firstSlice.SliceParams.UpperLeft.[2] + ((lastSlice.SliceParams.UpperLeft.[2] - firstSlice.SliceParams.UpperLeft.[2]) / 2.0)
        new Point3D(x, y, z)

    let buildScene estimatedSize =

        let distanceFactor, farPlaneFactor, fieldOfView = 0.90, 2.0, 70.0
        //let mutable cameraZ, zNear, zFar = estimatedSize*distanceFactor, 0.1, 1000.0
        let mutable cameraZ, zNear, zFar = estimatedSize*distanceFactor, 0.1, estimatedSize*farPlaneFactor

        let lightColor, tissueColor = Colors.White, Color.FromRgb(255uy, 173uy, 96uy)

        let position = Point3D(0.0, 0.0, - cameraZ)
        let camera = PerspectiveCamera
                        (
                            UpDirection = Vector3D(0.0,-1.0,0.0),
                            FieldOfView = fieldOfView,
                            NearPlaneDistance = zNear,
                            Position = position,
                            LookDirection = Point3D(0.0, 0.0, 0.0) - position
                         )

        let viewPortLight = DirectionalLight(Color = lightColor, Direction = camera.LookDirection)
        let lightModel = ModelVisual3D(Content = viewPortLight)
        let a3DGroup = Model3DGroup()
        let m3DModel = ModelVisual3D(Content = a3DGroup)
        let viewPort = Viewport3D(ClipToBounds = true, Camera = camera)
        seq { lightModel; m3DModel; } |> Seq.iter viewPort.Children.Add

        let buildBlock (points: Point3DCollection) = 
            let mesh = new MeshGeometry3D(Positions = points)
            let geometryModel = GeometryModel3D(mesh, DiffuseMaterial(SolidColorBrush(tissueColor)))
            geometryModel.Transform <- Transform3DGroup()
            geometryModel.Freeze()
            geometryModel

        //let addPoints (bufferChain: Point3DCollection list) = 
        //    a3DGroup.Dispatcher.BeginInvoke(Action(fun _ -> bufferChain |> List.iter(fun points -> points |> buildBlock |> a3DGroup.Children.Add))) |> ignore
        //    //bufferChain |> List.iter(fun points -> points |> buildBlock |> a3DGroup.Children.Add)

        let addPoints (points: Point3DCollection) = 
            let mesh = new MeshGeometry3D(Positions = points)
            let geometryModel = GeometryModel3D(mesh, DiffuseMaterial(SolidColorBrush(Colors.LightGoldenrodYellow)))
            geometryModel.Transform <- Transform3DGroup()
            a3DGroup.Children.Add(geometryModel)

            //geometryModel.Freeze()
            //a3DGroup.Children.Add(geometryModel)
            //a3DGroup.Dispatcher.InvokeAsync(Action(fun _ -> a3DGroup.Children.Add(geometryModel))) |> ignore

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

    let pool = ArrayPool<Point3D>.Shared

    let mesh isoLevel (slices: CatSlice[]) addPoints updateOrStop = 
        
        let clock = Stopwatch()
        clock.Start()

        let capacity = 30
        let borrowBuffer() = pool.Rent capacity

        let polygonize (front, back) =
            let mutable currentBuffer, index = borrowBuffer(), 0
            let mutable bufferChain = List.empty

            let addPoint p = 
                if index = capacity then
                    bufferChain <- currentBuffer :: bufferChain
                    currentBuffer <- borrowBuffer()
                    index <- 0

                currentBuffer.[index] <- p
                index <- index + 1

            polygonize (front, back) isoLevel addPoint

            let total = index + (bufferChain.Length * capacity)

            let points = new Point3DCollection(total)

            let dumpBuffer count (buffer: Point3D[]) = 
                for i in count - 1..-1..0 do 
                    points.Add buffer.[i]
                pool.Return buffer

            bufferChain |> List.iter(dumpBuffer capacity)
            dumpBuffer index currentBuffer
            pool.Return currentBuffer

            points.Freeze()
            points

        let update (points: Point3DCollection) =
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
        let polygonizeJob i = (async { return polygonize (slices.[i - 1], slices.[i]) }, update)

        seq { 1..slices.Length - 1 } |> Seq.iter(polygonizeJob >> RenderAgent.enqueueJob)


    // Build Volume View from sequence of CT Slices. 
    let New isoLevel slices =

        let updateOrStop = Render.updater()
        
        let firstSlice = slices |> Array.head
        let lastSlice = slices |> Array.last
        
        let centerPoint = getVolumeCenter firstSlice lastSlice
        slices |> Array.iter(fun slice -> slice.SliceParams.AdjustToCenter(centerPoint.X, centerPoint.Y, centerPoint.Z))
            
        // Calculate the centroid of the volumen.
        let estimatedModelSize = lastSlice.SliceParams.UpperLeft.[2] - firstSlice.SliceParams.UpperLeft.[2]
        let viewPort, addPoints = buildScene estimatedModelSize
        
        mesh isoLevel slices addPoints updateOrStop

        viewPort
namespace ReConstruct.UI.View

open System
open System.Collections.Generic

open ReConstruct.Core
open ReConstruct.Core.Async

open System.Buffers
open System.Diagnostics

open System.Windows
open System.Windows.Forms
open System.Windows.Forms.Integration

open OpenTK
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL

open ReConstruct.Core.Types

open ReConstruct.Data.Imaging
open ReConstruct.Data.Dicom

open ReConstruct.UI.View

module VolumeViewOpenGL = 

    module private RenderAgent =
        let private parallelThrottle = Environment.ProcessorCount - 1
        let private context = System.Threading.SynchronizationContext.Current
        let private throttledForkJoinAgent = Async.throttlingAgent parallelThrottle context
        let enqueueJob = ThrottledJob >> throttledForkJoinAgent.Post

    let private TRIANGLE_POINTS = 3
    let private TRIANGLE_VALUES = 3*TRIANGLE_POINTS

    let private maxTriangles = 3000000
    let private bufferSize = TRIANGLE_VALUES*maxTriangles
    let vertexBufferStep = 6 * sizeof<float32>
    let normalsOffset = 3 * sizeof<float32>

    let glContainer (estimatedSize, progressiveMesh) (width, height) =

        let container = new GLControl(GraphicsMode.Default)
        container.Width <- width
        container.Height <- height
        container.Dock <- DockStyle.Fill

        container.MakeCurrent()

        GL.ClearColor(Color4.Black)
        GL.Viewport(0, 0, int width, int height)
        GL.DepthMask(true)
        GL.Enable(EnableCap.DepthTest)
        GL.CullFace(CullFaceMode.Front)
        GL.DepthFunc(DepthFunction.Less)

        let vertexArrayObject = GL.GenVertexArray()
        GL.BindVertexArray(vertexArrayObject)

        let shader = Shader.New ("Shaders/shader.vert", "Shaders/lighting.frag")
        let transformMatrixId = GL.GetUniformLocation(shader.Handle, "MVP")

        let vertexBufferObject = GL.GenBuffer()

        let vertexBuffer = bufferSize |> VertexBuffer.New

        GL.BindBuffer(BufferTarget.ArrayBuffer, vertexBufferObject)
        GL.BufferData(BufferTarget.ArrayBuffer, arraySize vertexBuffer.Vertices, vertexBuffer.Vertices, BufferUsageHint.StaticDraw)

        GL.BindBuffer(BufferTarget.ArrayBuffer, vertexBufferObject)

        GL.EnableVertexAttribArray(0)
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, vertexBufferStep, 0)

        GL.EnableVertexAttribArray(1)
        GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, vertexBufferStep, normalsOffset)

        let mutable cameraZ, zNear, zFar = estimatedSize*0.90f, 0.125f, estimatedSize*3.0f
        //let mutable cameraZ, zNear, zFar = estimatedSize*1.75f, 0.1f, 1000.0f
        let cameraPosition() = Vector3(0.0f, 0.0f, cameraZ)

        let perspectiveProjection() =
            let fovy = 70.0f
            let projectionMatrix = Matrix4.CreatePerspectiveFieldOfView(fovy |> MathHelper.DegreesToRadians, container.AspectRatio, zNear, zFar)
            let viewMatrix = Matrix4.LookAt(cameraPosition(), Vector3.Zero, Vector3.UnitY)
            viewMatrix * projectionMatrix

        let mutable rotX, rotY, rotZ = 0.0f, 0.0f, 0.0f

        let moveCamera zoomFactor = cameraZ <- cameraZ + zoomFactor*estimatedSize

        let rotate (axis, delta) =
            match axis with
            | X -> rotX <- rotX + delta
            | Y -> rotY <- rotY + delta
            | Z -> rotZ <- rotZ + delta
            
        let modelViewProjection() =            
            Matrix4.CreateRotationX(rotX) * Matrix4.CreateRotationY(rotY) * Matrix4.CreateRotationZ(rotZ) * perspectiveProjection()

        //let lightPos = Vector3(0.0f, 1.0f, 0.0f)
        //let lightPos = cameraPosition()
        //let objectColor = Vector3(1.0f, 0.5f, 0.31f)
        let objectColor = Vector3(Color4.WhiteSmoke.R, Color4.WhiteSmoke.G, Color4.WhiteSmoke.B)
        let lightColor = Vector3(1.0f, 1.0f, 1.0f)

        let render() =
            GL.Clear(ClearBufferMask.ColorBufferBit ||| ClearBufferMask.DepthBufferBit)
            shader.Use()

            let camera = cameraPosition()

            shader.SetMatrix4("model", Matrix4.Identity)
            shader.SetVector3("objectColor", objectColor)
            shader.SetVector3("lightColor", lightColor)
            shader.SetVector3("lightPos", camera)
            shader.SetVector3("viewPos", camera)

            let mvp = modelViewProjection()
            GL.UniformMatrix4(transformMatrixId, false, ref mvp)

            GL.DrawArrays(PrimitiveType.Triangles, 0, bufferSize / 6)
            container.SwapBuffers()

        let update transform t =
            transform t
            sprintf "%f distance | X %fdeg | Y %fdeg | Z %fdeg" cameraZ rotX rotY rotZ |> Events.Status.Trigger
            render()

        let cleanUp() =
            GL.DeleteBuffers(1, ref vertexBufferObject)
            GL.DeleteProgram(shader.Handle)
            GL.DeleteVertexArrays(1, ref vertexArrayObject)

        let bufferLock = Object()
        let mutable currentOfset = 0

        let partialRender (buffer: float32[]) =

            let bufferSize = arraySize buffer

            GL.BindBuffer(BufferTarget.ArrayBuffer, vertexBufferObject)
            GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr currentOfset, bufferSize, buffer)

            lock bufferLock (fun _ -> currentOfset <- currentOfset + bufferSize)

            //let e0 = GL.GetError()

            render()

        let mutable firstRender = true

        let progressiveRender() =
            if firstRender then
                currentOfset <- 0
                progressiveMesh partialRender
                firstRender <- false
            else
                render()
        
        container.Paint |> Event.add(fun _ -> progressiveRender())
        DatasetMainView.Camera.OnCameraMoved.Publish |> Event.add (update moveCamera)
        DatasetMainView.Camera.OnRotation.Publish |> Event.add (update rotate)
        container.HandleDestroyed |> Event.add (fun _ -> cleanUp())
        
        container

    let onLoadHostWindow (host: WindowsFormsHost) model e =
        let parent = host.Parent :?> FrameworkElement
        host.Width <- parent.ActualWidth
        host.Height <- parent.ActualHeight
        host.Child <- (host.Width |> int, host.Height |> int) |> glContainer model

    let buildScene model =
        let winformsHost = new WindowsFormsHost(HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch)
        winformsHost.Loaded |> Event.add (onLoadHostWindow winformsHost model)
        (winformsHost :> UIElement, None)

    let getVolumeCenter firstSlice lastSlice =
        let x = firstSlice.SliceParams.UpperLeft.[0] + (firstSlice.SliceParams.PixelSpacing.X * (double firstSlice.SliceParams.Dimensions.Columns) / 2.0)
        let y = firstSlice.SliceParams.UpperLeft.[1] + (firstSlice.SliceParams.PixelSpacing.Y * (double firstSlice.SliceParams.Dimensions.Rows) / 2.0)
        let z = firstSlice.SliceParams.UpperLeft.[2] + ((lastSlice.SliceParams.UpperLeft.[2] - firstSlice.SliceParams.UpperLeft.[2]) / 2.0)
        new Vector3d(x, y, z)

    let pool = ArrayPool<float32>.Shared

    let mesh isoLevel (slices: CatSlice[]) partialRender = 
        let clock = Stopwatch.StartNew()

        let bufferSubdata _ =
            GL.GetError() |> sprintf "Render completed in %fs | %A" clock.Elapsed.TotalSeconds |> Events.Status.Trigger

        let capacity = 900
        let borrowBuffer() = pool.Rent capacity

        /// TODO: bufferChain implementation based on single linked list is atrocious, must be replaced.
        let polygonize (front, back) =
            let mutable currentBuffer, index = borrowBuffer(), 0
            let mutable bufferChain = List.empty

            let addCoordinate v =
                currentBuffer.[index] <- (float32 v)
                index <- index + 1

            let addPoint (p: Vector3d) = 
                if index = capacity then
                    bufferChain <- currentBuffer |> List.singleton |> List.append bufferChain
                    currentBuffer <- borrowBuffer()
                    index <- 0
                addCoordinate p.X
                addCoordinate p.Y
                addCoordinate p.Z

            MarchingCubesBasic.polygonize (front, back) isoLevel addPoint

            let total = index + (bufferChain.Length * capacity)

            let points = new List<float32>(total)

            let dumpBuffer count (buffer: float32[]) = 
                for i in 0..1..count-1 do
                    buffer.[i] |> points.Add
                pool.Return buffer

            bufferChain |> List.iter(dumpBuffer capacity)
            dumpBuffer index currentBuffer

            points.ToArray()

        let addPoints points =
            partialRender points
            clock.Elapsed.TotalSeconds |> sprintf "%fs" |> Events.Status.Trigger

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
        let polygonizeJob i = (async { return polygonize (slices.[i - 1], slices.[i]) }, addPoints)

        seq { 1..slices.Length - 1 } |> Seq.iter(polygonizeJob >> RenderAgent.enqueueJob)

    // Build Volume View from sequence of Slices. 
    let New isoLevel slices = 
        let firstSlice, lastSlice = slices |> Array.head, slices |> Array.last
        let estimatedModelSize = (lastSlice.SliceParams.UpperLeft.[2] - firstSlice.SliceParams.UpperLeft.[2]) |> float32

        // Volume center is the centroid of the paralelogram defined between the first and last slice.
        let centroid = getVolumeCenter firstSlice lastSlice
        slices |> Array.iter(fun slice -> slice.SliceParams.AdjustToCenter(centroid.X, centroid.Y, centroid.Z))

        let progressiveMesh = mesh isoLevel slices

        buildScene (estimatedModelSize, progressiveMesh)
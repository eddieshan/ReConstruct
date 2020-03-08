﻿namespace ReConstruct.UI.View

open System

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

        let renderQueue() = 
            let throttledForkJoinAgent = Async.throttlingAgent parallelThrottle context
            ThrottledJob >> throttledForkJoinAgent.Post

    let private TRIANGLE_POINTS = 3
    let private TRIANGLE_VALUES = 3*TRIANGLE_POINTS

    let private maxTriangles = 3000000
    let private bufferSize = TRIANGLE_VALUES*maxTriangles
    let vertexBufferStep = 6 * sizeof<float32>
    let normalsOffset = 3 * sizeof<float32>

    let private pool = ArrayPool<float32>.Shared

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

        //let mutable cameraZ, zNear, zFar = estimatedSize*0.8f, 0.125f, estimatedSize*4.0f
        //let perspectiveProjection() =
        //    let fovy = 45.0f
        //    Matrix4.CreatePerspectiveFieldOfView(fovy |> MathHelper.DegreesToRadians, container.AspectRatio, zNear, zFar)

        let mutable cameraZ, zNear, zFar = estimatedSize*1.20f, -estimatedSize*10.0f, estimatedSize*10.0f
        let orthographicProjection =
            Matrix4.CreateOrthographic((float32 width)/2.0f, (float32 height)/2.0f, zNear, zFar)

        let cameraPosition() = Vector3(0.0f, 0.0f, -cameraZ)
        let viewProjection() = Matrix4.LookAt(cameraPosition(), Vector3.Zero, Vector3.UnitY)

        let mutable rotX, rotY, rotZ = 0.0f, 0.0f, 0.0f

        let moveCamera zoomFactor = cameraZ <- cameraZ + zoomFactor*estimatedSize

        let rotate (axis, delta) =
            match axis with
            | X -> rotX <- rotX + delta
            | Y -> rotY <- rotY + delta
            | Z -> rotZ <- rotZ + delta
            
        let modelProjection() = 
            Matrix4.CreateRotationX(rotX) * Matrix4.CreateRotationY(rotY) * Matrix4.CreateRotationZ(rotZ)

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

            let mvp = modelProjection() * viewProjection() * orthographicProjection
            //let mvp = modelProjection() * viewProjection() * perspectiveProjection()
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

        let partialRender (index: int, capacity: int, bufferChain: float32[] list) =
            bufferChain |> List.iteri(fun i buffer ->
                let size = if i = 0 then index else capacity
                let bufferSize = size * sizeof<float32>
                GL.BindBuffer(BufferTarget.ArrayBuffer, vertexBufferObject)
                GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr currentOfset, bufferSize, buffer)
                lock bufferLock (fun _ -> currentOfset <- currentOfset + bufferSize)
                pool.Return buffer
            )
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
        let z = firstSlice.SliceParams.UpperLeft.[2] + (Math.Abs(lastSlice.SliceParams.UpperLeft.[2] - firstSlice.SliceParams.UpperLeft.[2]) / 2.0)
        new Vector3d(x, y, z)

    let mesh isoLevel (slices: CatSlice[]) partialRender = 
        let clock = Stopwatch.StartNew()

        let queueJob = RenderAgent.renderQueue()

        let bufferSubdata _ =
            GL.GetError() |> sprintf "Render completed in %fs | %A" clock.Elapsed.TotalSeconds |> Events.Status.Trigger

        let capacity = 900
        let borrowBuffer() = pool.Rent capacity

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
            
            bufferChain <- currentBuffer :: bufferChain

            (index, capacity, bufferChain)

        let renderLock = new Object()
        let addPoints points =
            lock renderLock (fun _ -> partialRender points)
            clock.Elapsed.TotalSeconds |> sprintf "%fs" |> Events.Status.Trigger

        let polygonizeJob i = (async { return polygonize (slices.[i - 1], slices.[i]) }, addPoints)

        seq { 1..slices.Length - 1 } |> Seq.iter(polygonizeJob >> queueJob)

    // Build Volume View from sequence of Slices. 
    let New isoLevel slices = 
        let firstSlice, lastSlice = slices |> Array.head, slices |> Array.last

        // Volume center is the centroid of the paralelogram defined between the first and last slice.
        let centroid = getVolumeCenter firstSlice lastSlice
        slices |> Array.iter(fun slice -> slice.SliceParams.AdjustToCenter(centroid.X, centroid.Y, centroid.Z))

        let estimatedModelSize = Math.Abs(lastSlice.SliceParams.UpperLeft.[2] - firstSlice.SliceParams.UpperLeft.[2]) |> float32

        let maxZ = (slices |> Array.maxBy(fun slice -> slice.SliceParams.UpperLeft.[2])).SliceParams.UpperLeft

        let progressiveMesh = mesh isoLevel slices

        buildScene (estimatedModelSize, progressiveMesh)
namespace ReConstruct.Render.OpenGL

open System
open System.Windows
open System.Windows.Forms
open System.Windows.Forms.Integration

open OpenTK
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL

open ReConstruct.Core
open ReConstruct.Render

module RenderView = 

    let private TRIANGLE_POINTS = 3
    let private TRIANGLE_VALUES = 3*TRIANGLE_POINTS

    let private maxTriangles = 3000000
    let private bufferSize = TRIANGLE_VALUES*maxTriangles
    let private vertexBufferStep = 6 * sizeof<float32>
    let private normalsOffset = 3 * sizeof<float32>

    let mutable private currentBufferSize = 0

    let totalTriangles() = currentBufferSize / 3

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
        shader.Use()

        let transformMatrixId = GL.GetUniformLocation(shader.Handle, "MVP")

        let vertexBufferObject = GL.GenBuffer()

        let totalSize = bufferSize * sizeof<float32>

        GL.EnableVertexAttribArray(0)
        GL.EnableVertexAttribArray(1)

        GL.BindBuffer(BufferTarget.ArrayBuffer, vertexBufferObject)
        
        Unsafe.UseAndDispose totalSize (fun buffer -> GL.BufferData(BufferTarget.ArrayBuffer, totalSize, buffer, BufferUsageHint.StaticDraw))

        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, vertexBufferStep, 0)        
        GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, vertexBufferStep, normalsOffset)

        Lighting.configure shader

        //let mutable cameraZ, zNear, zFar = estimatedSize*0.8f, 0.125f, estimatedSize*4.0f
        //let perspectiveProjection() =
        //    let fovy = 45.0f
        //    Matrix4.CreatePerspectiveFieldOfView(fovy |> MathHelper.DegreesToRadians, container.AspectRatio, zNear, zFar)

        let mutable cameraZ, zNear, zFar = estimatedSize, -estimatedSize*2.0f, estimatedSize*2.0f
        let orthographicProjection =
            Matrix4.CreateOrthographic((float32 width)/2.0f, (float32 height)/2.0f, zNear, zFar)

        let cameraPosition() = Vector3(0.0f, 0.0f, cameraZ)
        let viewProjection() = Matrix4.LookAt(cameraPosition(), Vector3.Zero, Vector3.UnitY)

        let modelTransform = ModelTransform.create()

        let moveCamera zoomFactor = cameraZ <- cameraZ + zoomFactor*estimatedSize

        let mutable currentOfset, subBufferSize = 0, 0
        currentBufferSize <- 0

        let maxSubBufferSize = 120000

        let render() =
            GL.Clear(ClearBufferMask.ColorBufferBit ||| ClearBufferMask.DepthBufferBit)

            let camera = cameraPosition()
            let modelProjection = modelTransform |> Projection.transformMatrix

            shader.SetMatrix4("model", Matrix4.Identity)
            shader.SetVector3("viewPos", camera)

            let mvp = modelProjection * viewProjection() * orthographicProjection
            //let mvp = modelProjection * viewProjection() * perspectiveProjection()
            GL.UniformMatrix4(transformMatrixId, false, ref mvp)

            GL.DrawArrays(PrimitiveType.Triangles, 0, currentBufferSize / 6)
            container.SwapBuffers()

        let handle error =
            if error <> ErrorCode.NoError then
                error |> sprintf "Error sub buffering data | %O" |> Exception |> raise

        let partialRender (size: int, buffer: float32[]) isLast =

            currentBufferSize <- currentBufferSize + size
            subBufferSize <- subBufferSize + size

            let bufferSize = size * sizeof<float32>
            GL.BindBuffer(BufferTarget.ArrayBuffer, vertexBufferObject)
            GL.GetError() |> handle

            GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr currentOfset, bufferSize, buffer)
            currentOfset <- currentOfset + bufferSize
    
            GL.GetError() |> handle

            if subBufferSize > maxSubBufferSize || isLast then    
                subBufferSize <- 0
                render()

        let mutable firstRender = true

        let progressiveRender() =
            if firstRender then
                currentOfset <- 0
                progressiveMesh partialRender
                firstRender <- false
            else
                render()

        let update transform t =
            transform t
            modelTransform |> Events.VolumeTransformed.Trigger
            render()

        let cleanUp() =
            GL.DeleteBuffers(1, ref vertexBufferObject)
            GL.DeleteProgram(shader.Handle)
            GL.DeleteVertexArrays(1, ref vertexArrayObject)
        
        container.Paint |> Event.add(fun _ -> progressiveRender())
        Events.OnCameraMoved.Publish |> Event.add (update moveCamera)
        Events.OnRotation.Publish |> Event.add (update modelTransform.Rotate)
        Events.OnScale.Publish |> Event.add (update modelTransform.Rescale)
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
        (winformsHost :> UIElement |> Some, None)
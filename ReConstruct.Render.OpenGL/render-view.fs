namespace ReConstruct.Render.OpenGL

open System
open System.Windows
open System.Windows.Forms
open System.Windows.Forms.Integration

open OpenTK
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL

open ReConstruct.Core.Types
open ReConstruct.Render

module RenderView = 

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

        //let mutable cameraZ, zNear, zFar = estimatedSize*0.8f, 0.125f, estimatedSize*4.0f
        //let perspectiveProjection() =
        //    let fovy = 45.0f
        //    Matrix4.CreatePerspectiveFieldOfView(fovy |> MathHelper.DegreesToRadians, container.AspectRatio, zNear, zFar)

        let mutable cameraZ, zNear, zFar = estimatedSize, -estimatedSize*2.0f, estimatedSize*2.0f
        let orthographicProjection =
            Matrix4.CreateOrthographic((float32 width)/2.0f, (float32 height)/2.0f, zNear, zFar)

        let cameraPosition() = Vector3(0.0f, 0.0f, cameraZ)
        let viewProjection() = Matrix4.LookAt(cameraPosition(), Vector3.Zero, Vector3.UnitY)

        let half = cameraZ/3.0f

        let pointLightPositions = [|
                Vector3(0.0f, 0.0f, half);
                Vector3(0.0f, half, 0.0f);
                Vector3(0.0f, -half, 0.0f);
                Vector3(half, 0.0f, 0.0f);
            |]

        let mutable rotX, rotY, rotZ, scale = 0.0f, 0.0f, 0.0f, 1.0f

        let resize scaleFactor = scale <- scale + scaleFactor

        let rotate (axis, delta) =
            match axis with
            | X -> rotX <- rotX + delta
            | Y -> rotY <- rotY + delta
            | Z -> rotZ <- rotZ + delta

        let moveCamera zoomFactor = cameraZ <- cameraZ + zoomFactor*estimatedSize
            
        let modelProjection() = 
            Matrix4.CreateScale(scale) * Matrix4.CreateRotationX(rotX) * Matrix4.CreateRotationY(rotY) * Matrix4.CreateRotationZ(rotZ)

        let setupLighting() =
            shader.SetFloat32("material.shininess", 32.0f)

            shader.SetVector3("dirLight.direction", Vector3(0.0f, -1.0f, 0.0f))
            shader.SetVector3("dirLight.color", Vector3(1.0f, 1.0f, 1.0f))
            shader.SetVector3("dirLight.ambient", Vector3(0.2f, 0.2f, 0.2f))
            shader.SetVector3("dirLight.diffuse", Vector3(0.5f, 0.5f, 0.5f))
            shader.SetVector3("dirLight.specular", Vector3(0.7f, 0.7f, 0.7f))

            for i in 0..pointLightPositions.Length-1 do
                shader.SetVector3(sprintf "pointLights[%i].position" i, pointLightPositions.[i])
                shader.SetVector3(sprintf "pointLights[%i].color" i, Vector3(1.0f, 1.0f, 1.0f))
                shader.SetVector3(sprintf "pointLights[%i].ambient" i, Vector3(0.2f, 0.3f, 0.2f))
                shader.SetVector3(sprintf "pointLights[%i].diffuse" i, Vector3(0.8f, 0.8f, 0.8f))
                shader.SetVector3(sprintf "pointLights[%i].specular" i, Vector3(1.0f, 1.0f, 1.0f))
                shader.SetFloat32(sprintf "pointLights[%i].constant" i, 1.0f)
                shader.SetFloat32(sprintf "pointLights[%i].linear" i, 0.09f)
                shader.SetFloat32(sprintf "pointLights[%i].quadratic" i, 0.032f)

        let render() =
            GL.Clear(ClearBufferMask.ColorBufferBit ||| ClearBufferMask.DepthBufferBit)
            shader.Use()

            let camera = cameraPosition()
            let modelProjection = modelProjection()

            shader.SetMatrix4("model", Matrix4.Identity)
            shader.SetVector3("viewPos", camera)

            let mvp = modelProjection * viewProjection() * orthographicProjection
            //let mvp = modelProjection * viewProjection() * perspectiveProjection()
            GL.UniformMatrix4(transformMatrixId, false, ref mvp)

            setupLighting()

            GL.DrawArrays(PrimitiveType.Triangles, 0, bufferSize / 6)
            container.SwapBuffers()

        let update transform t =
            transform t
            sprintf "%f distance | X %fdeg | Y %fdeg | Z %fdeg" cameraZ rotX rotY rotZ |> Events.RenderStatus.Trigger
            render()

        let cleanUp() =
            GL.DeleteBuffers(1, ref vertexBufferObject)
            GL.DeleteProgram(shader.Handle)
            GL.DeleteVertexArrays(1, ref vertexArrayObject)

        let mutable currentOfset, subBufferSize = 0, 0
        let maxSubBufferSize = 120000

        let handle error =
            if error <> ErrorCode.NoError then
                error |> sprintf "Error sub buffering data | %O" |> Exception |> raise

        let partialRender (size: int, buffer: float32[]) isLast =

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
        
        container.Paint |> Event.add(fun _ -> progressiveRender())
        Events.OnCameraMoved.Publish |> Event.add (update moveCamera)
        Events.OnRotation.Publish |> Event.add (update rotate)
        Events.OnScale.Publish |> Event.add (update resize)
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
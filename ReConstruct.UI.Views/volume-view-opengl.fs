﻿namespace ReConstruct.UI.View

open System
open System.Collections.Generic

type VertexBuffer = 
    {
        Vertices: float32[]
        Add: float32 -> unit
        AddVertices: IList<float32> -> unit
        //Count: unit -> int
        Size: unit -> int
        ElementSize: unit -> int
        Reset: unit -> unit
    }

module VertexBuffer = 
    
    let New size =
        let mutable count = 0
        let buffer = Array.zeroCreate size
        
        let add v = 
            buffer.[count] <- v
            count <- count + 1

        let addVertices (vertices: IList<float32>) = 
            vertices.CopyTo(buffer, count)
            count <- count + vertices.Count

        {
            Vertices = buffer
            Add = add
            AddVertices = addVertices
            //Count = fun() -> count
            Size = fun() -> count*sizeof<float32>
            ElementSize = fun() -> sizeof<float32> 
            Reset = fun() -> count <- 0
        }

module VolumeViewOpenGL = 
    open System.Diagnostics
    open System.Threading.Tasks

    open System.Windows
    open System.Windows.Forms
    open System.Windows.Forms.Integration
    open System.Windows.Media.Media3D

    open OpenTK
    open OpenTK.Graphics
    open OpenTK.Graphics.OpenGL

    open ReConstruct.Core
    open ReConstruct.Core.Types
    open ReConstruct.Core.Async

    open ReConstruct.Data.Imaging.MarchingCubes
    open ReConstruct.Data.Dicom

    open ReConstruct.UI.View

    let private TRIANGLE_POINTS = 3
    let private TRIANGLE_VALUES = 3*TRIANGLE_POINTS

    let private maxTriangles = 3000000
    let private bufferSize = TRIANGLE_VALUES*maxTriangles
    let private vertexBuffer = bufferSize |> VertexBuffer.New 
    let private normalsBuffer = bufferSize |> VertexBuffer.New 

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
        GL.BindBuffer(BufferTarget.ArrayBuffer, vertexBufferObject)
        GL.BufferData(BufferTarget.ArrayBuffer, arraySize vertexBuffer.Vertices, vertexBuffer.Vertices, BufferUsageHint.StaticDraw)

        let normalsBufferObject = GL.GenBuffer()
        GL.BindBuffer(BufferTarget.ArrayBuffer, normalsBufferObject)
        GL.BufferData(BufferTarget.ArrayBuffer, arraySize normalsBuffer.Vertices, normalsBuffer.Vertices, BufferUsageHint.StaticDraw)

        GL.EnableVertexAttribArray(0)
        GL.BindBuffer(BufferTarget.ArrayBuffer, vertexBufferObject)
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * vertexBuffer.ElementSize(), 0)

        GL.EnableVertexAttribArray(1)
        GL.BindBuffer(BufferTarget.ArrayBuffer, normalsBufferObject)
        GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 3 * normalsBuffer.ElementSize(), 0)

        //let mutable cameraZ, zNear, zFar = estimatedSize*0.90f, 0.125f, estimatedSize*3.0f
        let mutable cameraZ, zNear, zFar = estimatedSize*1.75f, 0.1f, 1000.0f
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
        let lightPos = cameraPosition()
        //let objectColor = Vector3(1.0f, 0.5f, 0.31f)
        let objectColor = Vector3(Color4.WhiteSmoke.R, Color4.WhiteSmoke.G, Color4.WhiteSmoke.B)
        let lightColor = Vector3(1.0f, 1.0f, 1.0f)

        let render() =
            GL.Clear(ClearBufferMask.ColorBufferBit ||| ClearBufferMask.DepthBufferBit)
            shader.Use()

            shader.SetMatrix4("model", Matrix4.Identity)
            shader.SetVector3("objectColor", objectColor)
            shader.SetVector3("lightColor", lightColor)
            shader.SetVector3("lightPos", lightPos)
            shader.SetVector3("viewPos", cameraPosition())

            let mvp = modelViewProjection()
            GL.UniformMatrix4(transformMatrixId, false, ref mvp)

            GL.DrawArrays(PrimitiveType.Triangles, 0, vertexBuffer.Vertices.Length / 3)
            container.SwapBuffers()

        let update transform t =
            transform t
            sprintf "%f distance | X %fdeg | Y %fdeg | Z %fdeg" cameraZ rotX rotY rotZ |> Events.Status.Trigger
            render()

        let cleanUp() =
            GL.DeleteBuffers(1, ref vertexBufferObject)
            GL.DeleteBuffers(1, ref normalsBufferObject)
            GL.DeleteProgram(shader.Handle)
            GL.DeleteVertexArrays(1, ref vertexArrayObject)

        let bufferLock = Object()
        let mutable currentOfset = 0

        let partialRender (partialVertexBuffer: float32[]) (partialNormalsBuffer: float32[]) =

            let partialVertexSize, partialNormalsSize = arraySize partialVertexBuffer, arraySize partialNormalsBuffer

            lock bufferLock (fun _ -> currentOfset <- currentOfset + partialVertexSize)

            GL.BindBuffer(BufferTarget.ArrayBuffer, vertexBufferObject)
            GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr currentOfset, partialVertexSize, partialVertexBuffer)

            GL.BindBuffer(BufferTarget.ArrayBuffer, normalsBufferObject)
            GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr currentOfset, partialNormalsSize, partialNormalsBuffer)

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
        new Point3D(x, y, z)

    // Calculate normals. 
    // *** TODO ***
    // For the moment each vertex is assigned the triangle normal.
    // Temporary, till an efficient algorithm to calculate real vertex normals is figured out.
    // Triangle normals results in poor lighting render.
    let getNormal (points: IList<Point3D>) index =
        let vertex i = Vector3(float32 points.[i].X, float32 points.[i].Y, float32 points.[i].Z)
        let vertices = seq { index..index + 2 } |> Seq.map vertex |> Seq.toArray
        //let normal = Vector3.Cross(vertices.[1] - vertices.[0], vertices.[2] - vertices.[0])
        let normal = Vector3.Cross(vertices.[2] - vertices.[0], vertices.[1] - vertices.[0])
        normal.Normalize()
        seq {
            yield! seq { normal.X; normal.Y; normal.Z; }
            yield! seq { normal.X; normal.Y; normal.Z; }
            yield! seq { normal.X; normal.Y; normal.Z; }
        }

    let mesh isoLevel (slices: CatSlice[]) partialRender = 
        let capacity = 10000
        let clock = Stopwatch.StartNew()

        let bufferSubdata _ =
            GL.GetError() |> sprintf "Render completed in %fs | %A" clock.Elapsed.TotalSeconds |> Events.Status.Trigger

        let polygonize (front, back) =
            let points = capacity |> List

            polygonize (front, back) isoLevel (fun p -> points.Add p)

            points

        let addPoints (points: IList<Point3D>) =
            let partialVertices = points |> Seq.collect(fun p -> seq { p.X; p.Y; p.Z; } |> Seq.map float32) |> Seq.toArray
            let partialNormals = seq { 0..3..points.Count - 3 } |> Seq.collect(fun i -> getNormal points i) |> Seq.toArray
            partialRender partialVertices partialNormals
            clock.Elapsed.TotalSeconds |> sprintf "%fs" |> Events.Status.Trigger

        //let chunks = seq { 1..slices.Length - 1 }
        //             |> Seq.toArray
        //             |> Array.Parallel.map (fun i -> polygonize (slices.[i - 1], slices.[i]))        
        //let totalSize = chunks |> Seq.sumBy(fun c -> c.Count)
        //chunks |> Array.iter addPoints
        //bufferSubdata()

        let current = TaskScheduler.FromCurrentSynchronizationContext()
        let updateUI = Action<Task<List<Point3D>>>(fun p -> p.Result |> addPoints)
        let forkAndJoinUI (task: Task<List<Point3D>>) =
            let completion = task.ContinueWith(updateUI, current)
            task.Start()
            completion

        let parallelThrottle = Environment.ProcessorCount

        let refresh (t: Task) = t.ContinueWith(Action<Task>(bufferSubdata), current)

        seq { 1..slices.Length - 1 } 
            //|> Seq.map(fun i -> Func<List<Point3D>>(fun _ -> polygonize (slices.[i - 1], slices.[i])) |> Task<List<Point3D>>)
            |> Seq.map(fun i -> (fun _ -> polygonize (slices.[i - 1], slices.[i])) |> func |> task)
            |> Seq.chunkBySize parallelThrottle
            |> Seq.iter(Seq.map forkAndJoinUI >> Task.WhenAll >> refresh >> ignore)

    // Build Volume View from sequence of Slices. 
    let New isoLevel slices = 
        let firstSlice, lastSlice = slices |> Array.head, slices |> Array.last
        let estimatedModelSize = (lastSlice.SliceParams.UpperLeft.[2] - firstSlice.SliceParams.UpperLeft.[2]) |> float32

        // Volume center is the centroid of the paralelogram defined between the first and last slice.
        let centroid = getVolumeCenter firstSlice lastSlice
        slices |> Array.iter(fun slice -> slice.SliceParams.AdjustToCenter(centroid))

        //let viewPort = buildScene (0, TestData.vertices, TestData.colors)
        let progressiveMesh = mesh isoLevel slices

        buildScene (estimatedModelSize, progressiveMesh)
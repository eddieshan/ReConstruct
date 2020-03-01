namespace ReConstruct.UI.View

open System
open System.IO
open System.Text
open System.Collections.Generic
open OpenTK
open OpenTK.Graphics.OpenGL4

type Shader =
    {
        Handle: int
        Use: unit -> unit
        GetUniformLocation: string -> int
        GetAttribLocation: string -> int
        SetInt: string*int -> unit
        SetFloat: string*float -> unit
        SetMatrix4: string*Matrix4 -> unit
        SetVector3: string *Vector3 -> unit
    }

module Shader =

    let LoadSource (path: string) =
        use sr = new StreamReader(path, Encoding.UTF8)
        sr.ReadToEnd()

    let Compile (shader: int) =
        // Try to compile the shader
        GL.CompileShader(shader)
    
        // Check for compilation errors
        //GL.GetShader(shader, ShaderParameter.CompileStatus, out var code)
        let code = GL.GetShader(shader, ShaderParameter.CompileStatus)
        if code <> (int All.True) then
            // We can use `GL.GetShaderInfoLog(shader)` to get information about the error.
            shader |> GL.GetShaderInfoLog |> sprintf "Shader compilation failed: %s" |> Exception|> raise

    let Link (program: int) =
        // We link the program
        GL.LinkProgram(program)
    
        // Check for linking errors
        let code = GL.GetProgram(program, GetProgramParameterName.LinkStatus)
        if code <> (int All.True) then
            // We can use `GL.GetProgramInfoLog(program)` to get information about the error.
            "Error occurred whilst linking Program" |> Exception |> raise


    let New (vertPath, fragPath) =

        let vertexSource = LoadSource(vertPath)
        let vertexShader = GL.CreateShader(ShaderType.VertexShader)
        GL.ShaderSource(vertexShader, vertexSource)
        Compile(vertexShader)
           
        let fragSource = LoadSource(fragPath)
        let fragmentShader = GL.CreateShader(ShaderType.FragmentShader)
        GL.ShaderSource(fragmentShader, fragSource)
        Compile(fragmentShader)

        let Handle = GL.CreateProgram()

        GL.AttachShader(Handle, vertexShader)
        GL.AttachShader(Handle, fragmentShader)

        Link(Handle)
           
        let numberOfUniforms = GL.GetProgram(Handle, GetProgramParameterName.ActiveUniforms)
           
        let uniformLocations = new Dictionary<string, int>()

        seq { 0 .. numberOfUniforms - 1}
        |> Seq.iter(fun i -> 
                        let key, _, _ = GL.GetActiveUniform(Handle, i)
                        let location = GL.GetUniformLocation(Handle, key)   
                        uniformLocations.Add(key, location)
                    )

        let apply f (name, data) = 
            GL.UseProgram(Handle)
            let location = uniformLocations.[name]
            f (location, data)

        GL.DetachShader(Handle, vertexShader)
        GL.DetachShader(Handle, fragmentShader)
        GL.DeleteShader(fragmentShader)
        GL.DeleteShader(vertexShader)
        
        {
            Handle = Handle
            Use = fun _ -> GL.UseProgram(Handle)
            GetAttribLocation =  fun attribName -> GL.GetAttribLocation(Handle, attribName)
            GetUniformLocation = fun key -> uniformLocations.[key]
            SetInt = apply (fun (location, data) -> GL.Uniform1(location, data))                        
            SetFloat = apply (fun (location, data) -> GL.Uniform1(location, data))
            SetMatrix4 = apply (fun (location, data) -> GL.UniformMatrix4(location, false, ref data))
            SetVector3 = apply (fun (location, data) -> GL.Uniform3(location, data))                                
        }
namespace ReConstruct.Render.OpenGL

open OpenTK

open ReConstruct.Render

module internal Projection =
    let transformMatrix modelTransform =
        let mutable (rotX, rotY, rotZ), scale = modelTransform.Rotation(), modelTransform.Scale()
        Matrix4.CreateScale(scale) * Matrix4.CreateRotationX(rotX) * Matrix4.CreateRotationY(rotY) * Matrix4.CreateRotationZ(rotZ)        

module internal Lighting =
    let Color = Vector3(1.0f, 1.0f, 1.0f)
    let Ambient = Vector3(0.05f, 0.05f, 0.05f)
    let Diffuse = Vector3(0.5f, 0.5f, 0.5f)
    let Specular = Vector3(0.4f, 0.4f, 0.4f)

    let dirLightPositions = [|
            Vector3(0.0f, 1.0f, 0.0f);
            Vector3(0.0f, -1.0f, 0.0f);
        |]

    let configure shader =

        shader.SetFloat32("material.shininess", 32.0f)

        for i in 0..dirLightPositions.Length-1 do
            shader.SetVector3(sprintf "dirLights[%i].direction" i, dirLightPositions.[i])
            shader.SetVector3(sprintf "dirLights[%i].color" i, Color)
            shader.SetVector3(sprintf "dirLights[%i].ambient" i, Ambient)
            shader.SetVector3(sprintf "dirLights[%i].diffuse" i, Diffuse)
            shader.SetVector3(sprintf "dirLights[%i].specular" i, Specular)
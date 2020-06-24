namespace ReConstruct.Render.OpenGL

open OpenTK

open ReConstruct.Render

module internal Projection =
    let transformMatrix modelTransform =
        let mutable (rotX, rotY, rotZ), scale = modelTransform.Rotation(), modelTransform.Scale()
        Matrix4.CreateScale(scale) * Matrix4.CreateRotationX(rotX) * Matrix4.CreateRotationY(rotY) * Matrix4.CreateRotationZ(rotZ)

module Lighting =
    let private toV3(v: System.Numerics.Vector3) = Vector3(v.X, v.Y, v.Z)

    let dirLightPositions = [|
            Vector3(0.0f, 1.0f, 0.0f);
            Vector3(0.0f, -1.0f, 0.0f);
        |]

    let configure shader =

        shader.SetFloat32("material.shininess", Scene.getReflectivity())

        for i in 0..dirLightPositions.Length-1 do
            shader.SetVector3(sprintf "dirLights[%i].direction" i, dirLightPositions.[i])

            // Color not used for now but it will be when implementing surfaces layers. Commented out for now. 
            // With some graphics cards the unused color uniform will be removed in compilation and access to the uniform throw an exception.
            //shader.SetVector3(sprintf "dirLights[%i].color" i, Scene.getColor() |> toV3)
            shader.SetVector3(sprintf "dirLights[%i].ambient" i, Scene.getAmbient() |> toV3)
            shader.SetVector3(sprintf "dirLights[%i].diffuse" i, Scene.getDiffuse() |> toV3)
            shader.SetVector3(sprintf "dirLights[%i].specular" i, Scene.getSpecular() |> toV3)
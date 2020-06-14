namespace ReConstruct.Render

open System.Numerics

module Scene =
    let mutable private _reflectivity = 0.8f
    let mutable private _color = Vector3(1.0f, 1.0f, 1.0f)
    let mutable private _ambient = Vector3(0.05f, 0.05f, 0.05f)
    let mutable private _diffuse = Vector3(0.7f, 0.7f, 0.7f)
    let mutable private _specular = Vector3(0.1f, 0.1f, 0.1f)

    let private updateScene f =
        f()
        true |> Events.OnSceneUpdate.Trigger

    let getColor() = _color
    let setColor v = updateScene(fun () -> _color <- v)

    let getReflectivity() = _reflectivity
    let setReflectivity v = updateScene(fun () -> _reflectivity <- v)

    let getAmbient() = _ambient
    let setAmbient v = updateScene(fun () -> _ambient <- v)
    
    let getDiffuse() = _diffuse
    let setDiffuse v = updateScene(fun () -> _diffuse <- v)

    let getSpecular() = _specular
    let setSpecular v = updateScene(fun () -> _specular <- v)
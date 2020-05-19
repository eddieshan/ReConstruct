namespace ReConstruct.Render

open System.Numerics

module Scene =
    let mutable private _reflectivity = 0.8f

    let mutable private _ambient = Vector3(0.05f, 0.05f, 0.05f)
    let mutable private _diffuse = Vector3(0.5f, 0.5f, 0.5f)
    let mutable private _specular = Vector3(0.4f, 0.4f, 0.4f)

    let getReflectivity() = _reflectivity
    let setReflectivity v = _reflectivity <- v
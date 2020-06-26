namespace ReConstruct.Core

open System
open System.Numerics

module Vector2 =
    let inline fromDoubles (v: float[]) = Vector2(float32 v.[0], float32 v.[1])

module Vector3 =
    let inline fromDoubles (v: float[]) = Vector3(float32 v.[0], float32 v.[1], float32 v.[2])
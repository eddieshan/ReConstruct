namespace ReConstruct.Geometry

open System
open System.Numerics

open ReConstruct.Data.Dicom

module Gradient =

    let d1 = 2.0 |> Math.Sqrt |> float32
    let d2 = 3.0 |> Math.Sqrt |> float32
    let inline avg (v0, v1, v2) = ((float32 v0) + (float32 v1)/d1 + (float32 v2)/d2)/18.0f

type Gradient(slices: ImageSlice[]) =

    // Approximate gradient with a smooth kernel based on averaging weighted partial derivatives.
    // Weights are the inverse of the distance to the point.
    member this.setValue (index, pos, g: byref<Vector3>) = 
        let numRows = slices.[index].Columns
        let prev, current, next = slices.[index - 1].HField, slices.[index].HField, slices.[index + 1].HField
        let right, left = pos + 1, pos - 1
        let under, above = pos + numRows, pos - numRows
        let aRight, aLeft = above + 1, above - 1
        let uRight, uLeft = under + 1, under - 1

        g.X <- 
            Gradient.avg(current.[right] - current.[pos] - current.[left] + current.[pos],
                current.[aRight] - current.[pos] - current.[aLeft] + current.[pos] + 
                current.[uRight] - current.[pos] - current.[uLeft] + current.[pos] + 
                prev.[right] - current.[pos] - prev.[left] + current.[pos] +
                next.[right] - current.[pos] - next.[left] + current.[pos],
                prev.[aRight] - current.[pos] - prev.[aLeft] + current.[pos] + 
                prev.[uRight] - current.[pos] - prev.[uLeft] + current.[pos] + 
                next.[aRight] - current.[pos] - next.[aLeft] + current.[pos] + 
                next.[uRight] - current.[pos] - next.[uLeft] + current.[pos])
        g.Y <- 
            Gradient.avg(current.[under] - current.[pos] - current.[above] + current.[pos],
                current.[uRight] - current.[pos] - current.[aRight] + current.[pos] +
                current.[uLeft] - current.[pos] - current.[aLeft] + current.[pos] +
                prev.[under] - current.[pos] - prev.[above] + current.[pos] +
                next.[under] - current.[pos] - next.[above] + current.[pos],
                prev.[uRight] - current.[pos] - prev.[aRight] + current.[pos] +
                prev.[uLeft] - current.[pos] - prev.[aLeft] + current.[pos] +
                next.[uRight] - current.[pos] - next.[aRight] + current.[pos] +
                next.[uLeft] - current.[pos] - next.[aLeft] + current.[pos])
        g.Z <- 
            Gradient.avg(next.[pos] - current.[pos] - prev.[pos] + current.[pos],
                next.[right] - current.[pos] - prev.[right] + current.[pos] +
                next.[left] - current.[pos] - prev.[left] + current.[pos] +
                next.[under] - current.[pos] - prev.[under] + current.[pos] +
                next.[above] - current.[pos] - prev.[above] + current.[pos],
                next.[aLeft] - current.[pos] - prev.[aLeft] + current.[pos] +
                next.[uLeft] - current.[pos] - prev.[uLeft] + current.[pos] +
                next.[aRight] - current.[pos] - prev.[aRight] + current.[pos] +
                next.[uRight] - current.[pos] - prev.[uRight] + current.[pos])


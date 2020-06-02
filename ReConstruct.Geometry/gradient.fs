namespace ReConstruct.Geometry

open System
open System.Numerics

open ReConstruct.Data.Dicom

module private Gradient =

    let d1 = 2.0 |> Math.Sqrt |> float32
    let d2 = 3.0 |> Math.Sqrt |> float32
    let inline avg (v0, v1, v2) = ((float32 v0) + (float32 v1)/d1 + (float32 v2)/d2)/18.0f

// Approximate gradient with a smooth kernel based on averaging weighted partial derivatives taking neighbour points.
// Weights are the inverse of the distance from the gradient point to its neighbours.
// Distances have 3 possible values,
// 1       -> Neighbour adjacent on the same axis.
// Sqrt(2) -> Neighbour on the opposite side of a coplanar diagonal.
// Sqrt(3) -> Neighbour on the opposite side of a non coplanar diagonal.
type Gradient(slices: ImageSlice[]) =

    member this.setValue (index, pos, g: byref<Vector3>) = 
        let numRows = slices.[index].Columns
        let prev, current, next = slices.[index - 1].HField, slices.[index].HField, slices.[index + 1].HField
        let right, left = pos + 1, pos - 1
        let under, above = pos + numRows, pos - numRows
        let aRight, aLeft = above + 1, above - 1
        let uRight, uLeft = under + 1, under - 1

        g.X <- 
            Gradient.avg(current.[right] - current.[left],
                current.[aRight] - current.[aLeft] + 
                current.[uRight] - current.[uLeft] + 
                prev.[right] - prev.[left] +
                next.[right] - next.[left],
                prev.[aRight] - prev.[aLeft] + 
                prev.[uRight] - prev.[uLeft] + 
                next.[aRight] - next.[aLeft] + 
                next.[uRight] - next.[uLeft])
        g.Y <- 
            Gradient.avg(current.[under] - current.[above],
                current.[uRight] - current.[aRight] +
                current.[uLeft] - current.[aLeft] +
                prev.[under] - prev.[above] +
                next.[under] - next.[above],
                prev.[uRight] - prev.[aRight] +
                prev.[uLeft] - prev.[aLeft] +
                next.[uRight] - next.[aRight] +
                next.[uLeft] - next.[aLeft])
        g.Z <- 
            Gradient.avg(next.[pos] - prev.[pos],
                next.[right] - prev.[right] +
                next.[left] - prev.[left] +
                next.[under] - prev.[under] +
                next.[above] - prev.[above],
                next.[aLeft] - prev.[aLeft] +
                next.[uLeft] - prev.[uLeft] +
                next.[aRight] - prev.[aRight] +
                next.[uRight] - prev.[uRight])


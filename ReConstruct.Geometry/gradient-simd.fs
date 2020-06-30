namespace ReConstruct.Geometry

open System
open System.Numerics

open ReConstruct.Data.Dicom

// Approximate gradient with a smooth kernel based on averaging weighted partial derivatives taking neighbour points.
// Weights are the inverse of the distance from the gradient point to its neighbours.
// Distances have 3 possible values,
// 1       -> Neighbour adjacent on the same axis.
// Sqrt(2) -> Neighbour on the opposite side of a coplanar diagonal.
// Sqrt(3) -> Neighbour on the opposite side of a non coplanar diagonal.
type GradientSimd(slices: ImageSlice[]) =
    let maxes = Vector(slices.[0].HField.Length - 1)
    let mins = Vector(0)
    let numRows = slices.[0].Columns
    let offsetsMin = Vector([| 1; numRows; -numRows + 1; numRows + 1; 0; 0; 0; 0; |])
    let offsetsMax = Vector([| -1; -numRows; -numRows - 1; numRows - 1; 0; 0; 0; 0; |])

    member this.get (index, pos: int) = 
        let prevIndex = Math.Max(0, index - 1)

        let prev, current, next = slices.[prevIndex].HField, slices.[index].HField, slices.[index + 1].HField

        let inline foldV4 (v: Vector4) = v.X + v.Y + v.Z + v.W
        let inline v4 (x, y, z, w) = Vector4(float32 x, float32 y, float32 z, float32 w)

        let positions = Vector(pos)
        let high = Vector.Max(mins, positions + offsetsMax)
        let low = Vector.Min(maxes, positions + offsetsMin)
        let right, under, aRight, uRight = low.[0], low.[1], low.[2], low.[3]
        let left, above, aLeft, uLeft = high.[0], high.[1], high.[2], high.[3]

        let x0 = float32 (current.[right] - current.[left])
        let x1 = (v4(current.[aRight], current.[uRight], prev.[right], next.[right]) - 
                  v4(current.[aLeft], current.[uLeft], prev.[left], next.[left])) |> foldV4
        let x2 = (v4(prev.[aRight], prev.[uRight], next.[aRight], next.[uRight]) -
                  v4(prev.[aLeft], prev.[uLeft], next.[aLeft], next.[uLeft])) |> foldV4

        let y0 = float32(current.[under] - current.[above])
        let y1 = (v4(current.[uRight], current.[uLeft], prev.[under],  next.[under]) -
                  v4(current.[aRight], current.[aLeft], prev.[above], next.[above])) |> foldV4
        let y2 = (v4(prev.[uRight], prev.[uLeft], next.[uRight], next.[uLeft]) -
                  v4(prev.[aRight], prev.[aLeft], next.[aRight], next.[aLeft])) |> foldV4

        let z0 = float32(next.[pos] - prev.[pos])
        let z1 = (v4(next.[right], next.[left], next.[under], next.[above]) - 
                  v4(prev.[right], prev.[left], prev.[under], prev.[above])) |> foldV4
        let z2 = (v4(next.[aLeft], next.[uLeft], next.[aRight], next.[uRight]) -
                  v4(prev.[aLeft], prev.[uLeft], prev.[aRight], prev.[uRight])) |> foldV4

        let v0 = Vector3(x0, y0, z0)
        let v1 = Vector3(x1, y1, z1)
        let v2 = Vector3(x2, y2, z2)

        (v0 + v1/DistanceSmoothKernel.d1 + v2/DistanceSmoothKernel.d2)/18.0f
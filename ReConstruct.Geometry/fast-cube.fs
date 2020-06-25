namespace ReConstruct.Geometry

open System
open System.Numerics

open ReConstruct.Data.Dicom

type FastCube(isoValue, front, back) =

    let zFront, zBack = float32 front.UpperLeft.[2], float32 back.UpperLeft.[2]
    let jumpX, jumpY = front.PixelSpacing.X, front.PixelSpacing.Y
    let top = float32 front.UpperLeft.[1]
    let bottom = top + jumpY

    let left = float32 front.UpperLeft.[0]
    let right = left + jumpX

    let stepX = Vector(Array.create 4 jumpX)
    let stepY = Vector(Array.create 4 jumpY)

    let x = [| left; right; right; left; left; right; right; left; |]
    let y = [| bottom; bottom; bottom; bottom; top; top; top; top; |]
    let z = [| zBack; zBack; zFront; zFront; zBack; zBack; zFront; zFront; |]

    let values = Array.zeroCreate<int16> 8
    let gradients = Array.zeroCreate<Vector3> 8

    let vA = Array.zeroCreate<float32> 4
    let vB = Array.zeroCreate<float32> 4

    member x.IsoValue = isoValue
    member x.Values = values
    member x.Gradients = gradients

    member this.X = x
    member this.SX = stepX

    member this.Y = y
    member this.SY = stepY

    member this.Z = z

    member this.Left = left
    member this.Right = right

    member this.StepX() = 
        (Vector(this.X) + this.SX).CopyTo(this.X)
        (Vector(this.X, 4) + this.SX).CopyTo(this.X, 4)

    member this.StepY() = 
        (Vector(this.Y) + this.SY).CopyTo(this.Y)
        (Vector(this.Y, 4) + this.SY).CopyTo(this.Y, 4)

    member this.ResetX() = 
        this.X.[0] <- this.Left
        this.X.[1] <- this.Right
        this.X.[2] <- this.Right
        this.X.[3] <- this.Left
        this.X.[4] <- this.Left
        this.X.[5] <- this.Right
        this.X.[6] <- this.Right
        this.X.[7] <- this.Left

    member this.Vertex(i: int) = Vector3(this.X.[i], this.Y.[i], this.Z.[i])

    member this.Lerp a b (mu: float32) = 
        vA.[0] <- this.X.[a]
        vA.[1] <- this.Y.[a]
        vA.[2] <- this.Z.[a]
        vB.[0] <- this.X.[b]
        vB.[1] <- this.Y.[b]
        vB.[2] <- this.Z.[b]
        
        Vector(vA) + Vector.Multiply(mu, Vector(vA) - Vector(vB))

    member x.GetIndex() =
        let mutable cubeIndex = 0uy
        for i in 0..x.Values.Length-1 do
            if (x.Values.[i] <= x.IsoValue) then
                cubeIndex <- cubeIndex ||| (1uy <<< i)
        cubeIndex |> int
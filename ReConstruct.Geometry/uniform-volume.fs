namespace ReConstruct.Geometry

open System
open System.Numerics

open ReConstruct.Data.Dicom

type UniformVolume =
    {
        IsoValue: int16
        Slices: ImageSlice[]
        Step: Vector3        
        VertexOffsets: Vector3[]
        CubeMap: int[][]
    }

module UniformVolume =
    let create (slices: ImageSlice[]) isoValue =
        let stepX, stepY = slices.[0].PixelSpacing.X, slices.[0].PixelSpacing.Y
        let stepZ = slices.[1].TopLeft.Z - slices.[0].TopLeft.Z
        let jumpColumn, jumpRow = 1, slices.[0].Columns
        let jumpDiagonal = jumpColumn + jumpRow

        let cubeMap = [|
            [| 1; jumpRow; |]
            [| 1; jumpDiagonal; |]
            [| 0; jumpDiagonal; |]
            [| 0; jumpRow; |]
            [| 1; 0; |]
            [| 1; jumpColumn; |]
            [| 0; jumpColumn; |]
            [| 0; 0; |]
        |]

        let vertexOffsets = [|
            Vector3(0.0f, stepY, stepZ)
            Vector3(stepX, stepY, stepZ)
            Vector3(stepX, stepY, 0.0f)
            Vector3(0.0f, stepY, 0.0f)
            Vector3(0.0f, 0.0f, stepZ)
            Vector3(stepX, 0.0f, stepZ)
            Vector3(stepX, 0.0f, 0.0f)
            Vector3(0.0f, 0.0f, 0.0f)
        |]

        {
            IsoValue = isoValue
            Slices = slices
            Step = Vector3(stepX, stepY, stepZ)
            VertexOffsets = vertexOffsets
            CubeMap = cubeMap            
        }
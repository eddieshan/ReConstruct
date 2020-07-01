namespace ReConstruct.Geometry

open System
open System.Numerics

open ReConstruct.Data.Dicom

open ReConstruct.Geometry.CommonIndices
open ReConstruct.Geometry.MarchingCubesTables
open ReConstruct.Geometry.DualContouringTables

module DualContouringIteratorSimd =

    let cubeMap = [|
        [| 1; 2; |]
        [| 1; 3; |]
        [| 0; 3; |]
        [| 0; 2; |]
        [| 1; 0; |]
        [| 1; 1; |]
        [| 0; 1; |]
        [| 0; 0; |]
    |]

    let iterate (volume: UniformVolume) frontIndex addPoint = 
        let jumpColumn, jumpRow = 1, volume.Slices.[frontIndex].Columns
        let jumpDiagonal = jumpColumn + jumpRow
        let gradient = GradientSimd(volume.Slices)

        let backIndex, nextIndex = frontIndex + 1, frontIndex + 2

        let isoValues = Vector(volume.IsoValue)

        let inline getIndex (values: int16[]) =
            let mutable cubeIndex = 0s
            
            // This will yield 1 for greather or equal and 0 otherwise, all 8 cubes values are checked in one instruction.
            let gtOrEq = -Vector.GreaterThanOrEqual(isoValues, Vector(values))

            // Bit masks can be applied sequentially with no branching, the comparison is stored numerically in gtOrEq.
            for i in 0..7 do
                cubeIndex <- cubeIndex ||| (gtOrEq.[i] <<< i)
            cubeIndex |> int
       
        let findInnerVertex (frontTopLeft: Vector3) (corners: Vector<int>) (values: int16[]) (section: int[]) = 
            let mutable bestFitVertex = Vector3.Zero
            let mutable bestFitGradient = Vector3.Zero

            let cubeIndex = getIndex values

            let contributions = EdgeContributions.[cubeIndex]

            for n in contributions do
                let indexA, indexB = int EdgeTraversal.[n].[0], int EdgeTraversal.[n].[1]
                let v1, v2 = values.[indexA], values.[indexB]
                let delta = v2 - v1

                let mu =
                    if delta = 0s then
                        0.5f
                    else
                        float32(volume.IsoValue - v1) / (float32 delta)

                let gradientA = gradient.get (section.[cubeMap.[indexA].[0]], corners.[cubeMap.[indexA].[1]])
                let gradientB = gradient.get (section.[cubeMap.[indexB].[0]], corners.[cubeMap.[indexB].[1]])

                bestFitVertex <- bestFitVertex + Vector3.Lerp(volume.VertexOffsets.[indexA], volume.VertexOffsets.[indexB], mu)
                bestFitGradient <- bestFitGradient + Vector3.Lerp(gradientA, gradientB, mu)

            if contributions.Length > 0 then
                bestFitVertex <- frontTopLeft + (bestFitVertex /(float32 contributions.Length))

            {
                CubeIndex = cubeIndex
                Position = bestFitVertex
                Gradient = bestFitGradient
            }
        
        let left = volume.Slices.[frontIndex].TopLeft.X

        let lastRow, lastColumn = volume.Slices.[frontIndex].Rows - 2, volume.Slices.[frontIndex].Columns - 2
        let nRows, nColumns = lastRow + 1, lastColumn + 1
        
        let columnJump = Vector<int>.One

        let sections = [| [| frontIndex; backIndex; |]; [| backIndex; nextIndex; |]; |]

        let createDualCellsRow _ = Array.zeroCreate<DualCell> nColumns
        let innerVertices = Array.init sections.Length (fun _ -> Array.init nRows createDualCellsRow)

        let mutable frontTopLeft = Vector3.Zero

        let values = Array.zeroCreate<int16> 16

        let baseCorners = [| 0; jumpColumn; jumpRow; jumpDiagonal; 0; 0; 0; 0; |]

        for n in 0..sections.Length-1 do
            let section = sections.[n]
            let mutable corners = Vector(baseCorners)

            frontTopLeft.Y <- volume.Slices.[section.[0]].TopLeft.Y
            frontTopLeft.Z <- volume.Slices.[section.[0]].TopLeft.Z

            for row in 0..lastRow do
                frontTopLeft.X <- left

                for column in 0..lastColumn do

                    let front, back = volume.Slices.[section.[0]].HField, volume.Slices.[section.[1]].HField
                    values.[0] <- back.[corners.[2]]
                    values.[1] <- back.[corners.[3]]
                    values.[2] <- front.[corners.[3]]
                    values.[3] <- front.[corners.[2]]
                    values.[4] <- back.[corners.[0]]
                    values.[5] <- back.[corners.[1]]
                    values.[6] <- front.[corners.[1]]
                    values.[7] <- front.[corners.[0]]

                    innerVertices.[n].[row].[column] <- findInnerVertex frontTopLeft corners values section

                    corners <- corners + columnJump

                    frontTopLeft.X <- frontTopLeft.X + volume.Step.X

                frontTopLeft.Y <- frontTopLeft.Y + volume.Step.Y

                corners <- corners + columnJump

        for row in 0..lastRow do
            for column in 0..lastColumn do
                let edgeIndex = innerVertices.[0].[row].[column].CubeIndex
                let contributingQuads = QuadContributions.[edgeIndex]
                for n in contributingQuads do
                    for triangle in QuadsTraversal.[n] do
                        let innerVertex = innerVertices.[triangle.[0]].[row + triangle.[1]].[column + triangle.[2]]
                        innerVertex.Position |> addPoint
                        innerVertex.Gradient |> addPoint
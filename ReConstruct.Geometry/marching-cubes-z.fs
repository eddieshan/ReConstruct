namespace ReConstruct.Geometry

open System
open System.Buffers
open System.Numerics

open ReConstruct.Data.Dicom

open ReConstruct.Geometry.MarchingCubesTables

module MarchingCubesZ =

    let private bufferPool = ArrayPool<float32>.Shared
    let private capacity = 9000
    let private borrowBuffer() = bufferPool.Rent capacity

    let v3(v4: Vector4) = Vector3(v4.X, v4.Y, v4.Z)

    let lerp(p1: Vector4, p2: Vector4, value: float32) =
        if Math.Abs(p1.W - p2.W) > 0.00001f then
            v3(p1) + (v3(p2) - v3(p1))/(p2.W - p1.W)*(value - p1.W)
        else 
            v3(p1)

    let MarchingCubes(cubesX: int, cubesY: int, cubesZ: int, 
                      sizeFactor: Vector3, isoValue: float32, points: int[], addVertex,
                      upperLeft: Vector3, step: Vector3) =

        let factor = Vector3(1.0f/(2.0f*sizeFactor.X), 1.0f/(2.0f*sizeFactor.Y), 1.0f/(2.0f*sizeFactor.Z))

        let lerpNormal(p1: Vector4, p2: Vector4, value: float32) = lerp(p1, p2, value)*factor

        let jumpDown = cubesZ + 1
        let jumpRight = (cubesY + 1) * jumpDown
        let jumpRight2 = 2*jumpRight
        let jumpDown2 = 2*jumpDown
        let jumpRightAndBack = jumpRight + 1
        let jumpRightAndDown = jumpRight + jumpDown
        let jumpDownAndBack = jumpDown + 1
        let jumpRightAndDownAndBack =  jumpRightAndDown + 1

        let lastX, lastY, lastZ = cubesX - 1, cubesY - 1, cubesZ - 1

        let cube = Array.zeroCreate<Vector4> 8
        let vertices = Array.zeroCreate<Vector3> 12
        let normalVertices = Array.zeroCreate<Vector4> 8
        let normals = Array.zeroCreate<Vector3> 12

        let valueAt n = 
            if n < points.Length then
                float32 points.[n]
            else
                0.0f

        let adjacents = [|
            [| 0; 1; -jumpRight;                0; 4; -jumpDown;                 0; 3; -1;                       |]
            [| 1; 0; jumpRight2;                0; 5; jumpRight - jumpDown;      0; 2; jumpRight - 1;            |]
            [| 1; 3; jumpRight2 + 1;            0; 6; jumpRight - cubesZ;       1; 2; jumpRight + 2;            |]
            [| 0; 2; -jumpRight + 1;            0; 7; -cubesZ;                  1; 0; 2;                        |]
            [| 0; 5; -jumpRight + jumpDown;     1; 0; jumpDown2;                 0; 7; cubesZ;                  |]
            [| 1; 4; jumpRight2 + jumpDown;     1; 1; jumpRight + jumpDown2;     0; 6; jumpRight + cubesZ;      |]
            [| 1; 7; jumpRight2 + jumpDown + 1; 1; 2; jumpRight + jumpDown2 + 1; 1; 5; jumpRight + jumpDown + 2; |]
            [| 0; 6; -jumpRight + jumpDown + 1; 1; 3; jumpDown2 + 1;             1; 4; jumpDown + 2;             |]
        |]

        let mutable index = 0

        let normalAt n =
            let x =
                if adjacents.[n].[0] = 0 then
                    valueAt(index + adjacents.[n].[2]) - cube.[adjacents.[n].[1]].W
                else
                    cube.[adjacents.[n].[1]].W - valueAt(index + adjacents.[n].[2])
            let y =
                if adjacents.[n].[3] = 0 then
                    valueAt(index + adjacents.[n].[5]) - cube.[adjacents.[n].[4]].W
                else
                    cube.[adjacents.[n].[4]].W - valueAt(index + adjacents.[n].[5])
            let z =
                if adjacents.[n].[6] = 0 then
                    valueAt(index + adjacents.[n].[8]) - cube.[adjacents.[n].[7]].W
                else
                    cube.[adjacents.[n].[7]].W - valueAt(index + adjacents.[n].[8])
            Vector4(x, y, z, cube.[n].W)

        let normalOrUnit edgeIndex test = 
            if test then normalAt edgeIndex
            else Vector4(1.0f, 1.0f, 1.0f, 1.0f)

        let mutable left, right = upperLeft.X, upperLeft.X + step.X
        let mutable offsetX, offsetY = 0, 0
        let mutable cubeIndex, edgeIndex, normalIndex = 0, 0, 0

        let normalsLookup = [|
            [| 0; 1; 0; 0; 0; |]
            [| 1; 2; lastX; 0; 0; |]

            [| 1; 2; lastX; 0; 0; |]
            [| 2; 4; lastX; 0; 0; |]

            [| 2; 4; lastX; 0; 0; |]
            [| 3; 8; 0; 0; lastZ; |]

            [| 3; 8; 0; 0; lastZ; |]
            [| 0; 1; 0; 0; 0; |]

            [| 4; 16; 0; lastY; 0; |]
            [| 5; 32; lastX; lastY; 0; |]

            [| 5; 32; lastX; lastY; 0; |]
            [| 6; 64; lastX; lastY; lastZ; |]

            [| 6; 64; lastX; lastY; lastZ; |]
            [| 7; 128; 0; lastY; lastZ; |]

            [| 7; 128; 0; lastY; lastZ; |]
            [| 4; 16; 0; lastY; 0; |]

            [| 0; 1; 0; 0; 0; |]
            [| 4; 16; 0; lastY; 0; |]

            [| 1; 2; lastX; 0; 0; |]
            [| 5; 32; lastX; lastY; 0; |]

            [| 2; 4; lastX; 0; 0; |]
            [| 6; 64; lastX; lastY; lastZ; |]

            [| 3; 8; 0; 0; lastZ; |]
            [| 7; 128; 0; lastY; lastZ; |]
        |]

        for i in 0..lastX do
            offsetX <- offsetX + jumpRight
            left <- left + step.X
            right <- right + step.X

            let mutable top, bottom = upperLeft.Y, upperLeft.Y + step.Y
            
            offsetY <- 0
            for j in 0..lastY do
                offsetY <- offsetY + jumpDown
                top <- top + step.Y
                bottom <- top + step.Y

                let mutable front, back = upperLeft.Z, upperLeft.Z + step.Z

                let offsetXY = offsetX + offsetY

                for k in 0..lastZ do
                    index <- offsetXY + k
                    front <- front + step.Z
                    back <- back + step.Z

                    cube.[0] <- Vector4(left, top, front, valueAt(index))
                    cube.[1] <- Vector4(right, top, front, valueAt(index + jumpRight))
                    cube.[2] <- Vector4(right, top, back, valueAt(index + jumpRightAndBack))
                    cube.[3] <- Vector4(left, top, back, valueAt(index + 1))
                    cube.[4] <- Vector4(left, bottom, front, valueAt(index + jumpDown))
                    cube.[5] <- Vector4(right, bottom, front, valueAt(index + jumpRightAndDown))
                    cube.[6] <- Vector4(right, bottom, back, valueAt(index + jumpRightAndDownAndBack))
                    cube.[7] <- Vector4(left, bottom, back, valueAt(index + jumpDownAndBack))

                    cubeIndex <- 0

                    for n in 0..7 do
                        if cube.[n].W <= isoValue then
                            cubeIndex <- cubeIndex ||| (1 <<< n)

                    let traverseNormal n =
                        if (normalIndex &&& normalsLookup.[n].[1] = 0) then
                            normalVertices.[normalsLookup.[n].[0]] <- normalOrUnit normalsLookup.[n].[0] (i <> normalsLookup.[n].[2] && j <> normalsLookup.[n].[3] && k <> normalsLookup.[n].[4])
                            normalIndex <- normalIndex ||| normalsLookup.[n].[1]

                    let calculateVertex n = 
                        let p0 = (n <<< 1)
                        let p1 = p0 + 1
                        traverseNormal p0
                        traverseNormal p1
                        vertices.[n] <- lerp(cube.[normalsLookup.[p0].[0]], cube.[normalsLookup.[p1].[0]], isoValue)
                        normals.[n] <- lerpNormal(normalVertices.[normalsLookup.[p0].[0]], normalVertices.[normalsLookup.[p1].[0]], isoValue)
                
                    if EdgeTable.[cubeIndex] <> 0 then
                        normalIndex <- 0
                        edgeIndex <- EdgeTable.[cubeIndex]

                        for n in 0..11 do
                            if (edgeIndex &&& (1 <<< n)) <> 0 then
                                calculateVertex n
                
                        let mutable n = 0
                        while TriTable.[cubeIndex, n] <> -1 do
                            let index = [| TriTable.[cubeIndex, n+2]; TriTable.[cubeIndex, n+1]; TriTable.[cubeIndex, n] |]
                            for h in 0..2 do
                                addVertex vertices.[index.[h]]
                                addVertex normals.[index.[h]]
                            n <- n + 3

    let polygonize isoLevel (slices: CatSlice[]) partialRender = 

        let start = slices.[0].SliceParams
        
        let columns, rows = start.Dimensions.Columns, start.Dimensions.Rows
        let points = Array.zeroCreate<int> (rows*columns*slices.Length)
        let mutable index = 0

        let stepZ = slices |> Seq.pairwise 
                           |> Seq.map(fun (f, b) -> Math.Abs(f.SliceParams.UpperLeft.[2] - b.SliceParams.UpperLeft.[2])) 
                           |> Seq.distinct
                           |> Seq.exactlyOne
        
        let step = Vector3(float32 start.PixelSpacing.X, float32 start.PixelSpacing.Y, float32 stepZ)
        for i in 0..columns - 1 do
            for j in 0..rows - 1 do
                for k in 0..slices.Length - 1 do
                    points.[index] <- slices.[k].HounsfieldBuffer.[j, i]
                    index <- index + 1
        
        let sizeZ = Math.Abs(slices.[slices.Length - 1].SliceParams.UpperLeft.[2] - slices.[0].SliceParams.UpperLeft.[2]) |> float32
        let sizeFactor = Vector3((float32 columns)*step.X, (float32 rows)*step.Y, sizeZ)
        let upperLeft = Vector3(float32 start.UpperLeft.[0], float32 start.UpperLeft.[1], float32 start.UpperLeft.[2])

        let mutable currentBuffer, index = borrowBuffer(), 0

        let addPoint (p: Vector3) = 
            if index = capacity then
                partialRender (index, currentBuffer) false
                index <- 0

            p.CopyTo(currentBuffer, index)
            index <- index + 3

        MarchingCubes(columns - 1, rows - 1, slices.Length - 1, 
                     sizeFactor, isoLevel, points, addPoint,
                     upperLeft,
                     step)

        partialRender (index, currentBuffer) true
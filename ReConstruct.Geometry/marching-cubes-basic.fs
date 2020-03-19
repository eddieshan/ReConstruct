namespace ReConstruct.Geometry

open System
open System.Numerics
open System.Buffers

open ReConstruct.Core

open ReConstruct.Data.Dicom

open ReConstruct.Geometry.CubesIterator
open ReConstruct.Geometry.MarchingCubesTables

module private RenderAgent =
    let private parallelThrottle = Environment.ProcessorCount - 1
    let private context = System.Threading.SynchronizationContext.Current

    let renderQueue() = 
        let throttledForkJoinAgent = Async.throttlingAgent parallelThrottle context
        Async.ThrottledJob >> throttledForkJoinAgent.Post

module MarchingCubesBasic =

    let private bufferPool = ArrayPool<float32>.Shared
    let private capacity = 9000
    let private borrowBuffer() = bufferPool.Rent capacity

    let private lerpVertex (index1: int, index2: int) (x: Cube) =
        let tolerance = 0.00001f

        let val1 = float32 x.Levels.[index1]
        let val2 = float32 x.Levels.[index2]

        let point1 = x.Vertices.[index1]
        let point2 = x.Vertices.[index2]

        if Math.Abs(x.IsoValue - val1) < tolerance then
            point1   
        elif Math.Abs(x.IsoValue - val2) < tolerance then
            point2
        elif Math.Abs(val1 - val2) < tolerance then
            point1
        else
            let mu = (x.IsoValue - val1) / (val2 - val1)
            let x = point1.X + mu * (point2.X - point1.X)
            let y = point1.Y + mu * (point2.Y - point1.Y)
            let z = point1.Z + mu * (point2.Z - point1.Z)
            Vector3(x, y, z)

    let private marchCube (cube: Cube) =

        let mutable cubeIndex = 0
        let intIsoLevel = int cube.IsoValue

        for i in 0..cube.Levels.Length-1 do
            if (cube.Levels.[i] <= intIsoLevel) then
                cubeIndex <- cubeIndex ||| (1 <<< i)

        if EdgeTable.[cubeIndex] <> 0 then
            let vertlist = Array.zeroCreate<Vector3> 12
            
            for i in 0..EdgeTraversal.Length-1 do
                if (EdgeTable.[cubeIndex] &&& (1 <<< i)) > 0 then
                    vertlist.[i] <- lerpVertex EdgeTraversal.[i] cube

            let mutable index = 0
            while TriTable.[cubeIndex, index] <> -1 do
                let v0 = vertlist.[TriTable.[cubeIndex, index]]
                let v1 = vertlist.[TriTable.[cubeIndex, index + 1]]
                let v2 = vertlist.[TriTable.[cubeIndex, index + 2]]
                let normal = Vector3.Cross(v2 - v0, v1 - v0) |> Vector3.Normalize

                cube.AddPoint v0
                cube.AddPoint normal
                cube.AddPoint v1
                cube.AddPoint normal
                cube.AddPoint v2
                cube.AddPoint normal

                index <- index + 3

    let polygonize isoLevel (slices: CatSlice[]) partialRender = 
        let queueJob = RenderAgent.renderQueue()

        let polygonizeSection (front, back) =
            let mutable currentBuffer, index = borrowBuffer(), 0
            let mutable bufferChain = List.empty

            let addPoint (p: System.Numerics.Vector3) = 
                if index = capacity then
                    bufferChain <- currentBuffer :: bufferChain
                    currentBuffer <- borrowBuffer()
                    index <- 0

                p.CopyTo(currentBuffer, index)
                index <- index + 3

            CubesIterator.iterate (front, back) isoLevel addPoint marchCube

            bufferChain <- currentBuffer :: bufferChain

            (index, capacity, bufferChain)

        let addPoints (index, capacity, bufferChain) =
            let lastBufferIndex = List.length bufferChain - 1
            let dumpBuffer i (buffer: float32[]) =
                let size = if i = 0 then index else capacity
                partialRender (size, buffer) (i = lastBufferIndex)
                bufferPool.Return buffer
            bufferChain |> List.iteri dumpBuffer

        let polygonizeJob i = (async { return polygonizeSection (slices.[i - 1], slices.[i]) }, addPoints)

        seq { 1..slices.Length - 1 } |> Seq.iter(polygonizeJob >> queueJob)
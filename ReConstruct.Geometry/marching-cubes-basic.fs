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
        fun processResult job -> (job, processResult) |> Async.ThrottledJob |> throttledForkJoinAgent.Post

module MarchingCubesBasic =

    let private bufferPool = ArrayPool<float32>.Shared
    let private capacity = 9000
    let private borrowBuffer() = bufferPool.Rent capacity

    let private marchCube addPoint (cube: Cube) =

        let mutable cubeIndex = 0

        for i in 0..cube.Levels.Length-1 do
            if (cube.Levels.[i] <= cube.IsoValue) then
                cubeIndex <- cubeIndex ||| (1 <<< i)

        if EdgeTable.[cubeIndex] <> 0 then
            let vertlist = Array.zeroCreate<Vector3> 12
            
            for i in 0..EdgeTraversal.Length-1 do
                if (EdgeTable.[cubeIndex] &&& (1 <<< i)) > 0 then
                    let index1, index2 = EdgeTraversal.[i].[0], EdgeTraversal.[i].[1]
                    let v1, v2 = cube.Levels.[index1], cube.Levels.[index2]
                    let delta = v2 - v1
                    let mu =
                        if delta = 0 then
                            0.5f
                        else
                            float32(cube.IsoValue - v1) / (float32 delta)
                    vertlist.[i] <- cube.Vertices.[index1] + mu*(cube.Vertices.[index2] - cube.Vertices.[index1])

            let index = ref 0
            let triangles = TriTable.[cubeIndex]
            while triangles.[!index] <> -1 do
                let v0 = vertlist.[triangles.[!index]]
                incr index
                let v1 = vertlist.[triangles.[!index]]
                incr index
                let v2 = vertlist.[triangles.[!index]]
                incr index

                let normal = Vector3.Cross(v2 - v0, v1 - v0) |> Vector3.Normalize

                addPoint v0
                addPoint normal
                addPoint v1
                addPoint normal
                addPoint v2
                addPoint normal

    let polygonize isoLevel (slices: ImageSlice[]) partialRender = 

        let polygonizeSection (front, back) =
            let mutable currentBuffer, index = borrowBuffer(), 0
            let mutable bufferChain = List.empty

            let addPoint (p: Vector3) = 
                if index = capacity then
                    bufferChain <- currentBuffer :: bufferChain
                    currentBuffer <- borrowBuffer()
                    index <- 0

                p.CopyTo(currentBuffer, index)
                index <- index + 3

            CubesIterator.iterate (front, back) isoLevel (marchCube addPoint)

            bufferChain <- currentBuffer :: bufferChain

            (index, capacity, bufferChain)

        let addPoints (index, capacity, bufferChain) =
            let lastBufferIndex = List.length bufferChain - 1
            let dumpBuffer i (buffer: float32[]) =
                let size = if i = 0 then index else capacity
                partialRender (size, buffer) (i = lastBufferIndex)
                bufferPool.Return buffer
            bufferChain |> List.iteri dumpBuffer

        let queueJob = RenderAgent.renderQueue()
        let polygonizeJob i = async { return polygonizeSection (slices.[i - 1], slices.[i]) }
        seq { 1..slices.Length - 1 } |> Seq.iter(polygonizeJob >> (queueJob addPoints))

        /// NOT TO BE REMOVED. Single-thread version for testing.
        //let polygonizeJob i = polygonizeSection (slices.[i - 1], slices.[i]) |> addPoints
        //seq { 1..slices.Length - 1 } |> Seq.iter(polygonizeJob)
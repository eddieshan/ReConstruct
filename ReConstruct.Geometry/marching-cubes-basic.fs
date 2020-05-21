namespace ReConstruct.Geometry

open System
open System.Numerics

open ReConstruct.Core

open ReConstruct.Data.Dicom

open ReConstruct.Geometry.MarchingCubesTables

module private RenderAgent =
    let private parallelThrottle = Environment.ProcessorCount - 1
    let private context = System.Threading.SynchronizationContext.Current

    let renderQueue() = 
        let throttledForkJoinAgent = Async.throttlingAgent parallelThrottle context
        fun processResult job -> (job, processResult) |> Async.ThrottledJob |> throttledForkJoinAgent.Post

module MarchingCubesBasic =

    let private marchCube addPoint (cube: Cube) =

        let cubeIndexAsInt = cube.GetIndex()

        if EdgeTable.[cubeIndexAsInt] <> 0 then
            let vertlist = Array.zeroCreate<Vector3> 12
            
            for i in 0..EdgeTraversal.Length-1 do
                if (EdgeTable.[cubeIndexAsInt] &&& (1 <<< i)) > 0 then
                    let index1, index2 = int EdgeTraversal.[i].[0], int EdgeTraversal.[i].[1]
                    let v1, v2 = cube.Values.[index1], cube.Values.[index2]
                    let delta = v2 - v1
                    let mu =
                        if delta = 0s then
                            0.5f
                        else
                            float32(cube.IsoValue - v1) / (float32 delta)

                    vertlist.[i] <- Vector3.Lerp(cube.Vertices.[index1], cube.Vertices.[index2], mu)

            let triangles = TriTable2.[cubeIndexAsInt]

            for triangle in triangles do
                let v0 = vertlist.[triangle.[0]]
                let v1 = vertlist.[triangle.[1]]
                let v2 = vertlist.[triangle.[2]]

                let normal = Vector3.Cross(v2 - v0, v1 - v0) |> Vector3.Normalize

                addPoint v0
                addPoint normal
                addPoint v1
                addPoint normal
                addPoint v2
                addPoint normal

    let polygonize isoLevel (slices: ImageSlice[]) partialRender = 

        let polygonizeSection (front, back) =
            let mutable currentBuffer, index = BufferPool.borrow(), 0
            let mutable bufferChain = List.empty

            let addPoint (p: Vector3) = 
                if index = BufferPool.Capacity then
                    bufferChain <- currentBuffer :: bufferChain
                    currentBuffer <- BufferPool.borrow()
                    index <- 0

                p.CopyTo(currentBuffer, index)
                index <- index + 3

            CubesIterator.iterate (front, back) isoLevel (marchCube addPoint)

            bufferChain <- currentBuffer :: bufferChain

            (index, BufferPool.Capacity, bufferChain)

        let addPoints (index, capacity, bufferChain) =
            let lastBufferIndex = List.length bufferChain - 1
            let dumpBuffer i (buffer: float32[]) =
                let size = if i = 0 then index else capacity
                partialRender (size, buffer) (i = lastBufferIndex)
                BufferPool.ret buffer
            bufferChain |> List.iteri dumpBuffer

        let queueJob = RenderAgent.renderQueue()
        let polygonizeJob i = async { return polygonizeSection (slices.[i - 1], slices.[i]) }
        
        seq { 1..slices.Length - 1 } |> Seq.iter(polygonizeJob >> (queueJob addPoints))

        /// NOT TO BE REMOVED. Single-thread version for testing.
        //let polygonizeJob i = polygonizeSection (slices.[i - 1], slices.[i]) |> addPoints
        //seq { 1..slices.Length - 1 } |> Seq.iter(polygonizeJob)
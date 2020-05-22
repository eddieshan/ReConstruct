namespace ReConstruct.Geometry

open System
open System.Buffers
open System.Numerics

open ReConstruct.Core

module private BufferPool =
    let private bufferPool = ArrayPool<float32>.Shared
    let Capacity = 9000
    let borrow() = bufferPool.Rent Capacity
    let ret buffer = bufferPool.Return buffer

module private RenderAgent =
    let private parallelThrottle = Environment.ProcessorCount - 1
    let private context = System.Threading.SynchronizationContext.Current

    let renderQueue() = 
        let throttledForkJoinAgent = Async.throttlingAgent parallelThrottle context
        fun processResult job -> (job, processResult) |> Async.ThrottledJob |> throttledForkJoinAgent.Post

type private BufferChain() =
    let mutable currentBuffer = BufferPool.borrow()
    let mutable index = 0
    let mutable bufferChain = List.empty

    member this.Add(p: Vector3) =
        if index = BufferPool.Capacity then
            bufferChain <- currentBuffer :: bufferChain
            currentBuffer <- BufferPool.borrow()
            index <- 0

        p.CopyTo(currentBuffer, index)
        index <- index + 3

    member this.Dump f =
        bufferChain <- currentBuffer :: bufferChain
        let lastBufferIndex = List.length bufferChain - 1
        let dumpBuffer i (buffer: float32[]) =
            let size = if i = 0 then index else BufferPool.Capacity
            f (size, buffer) (i = lastBufferIndex)
            BufferPool.ret buffer
        bufferChain |> List.iteri dumpBuffer
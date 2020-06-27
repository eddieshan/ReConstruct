namespace ReConstruct.Core

open System
open System.Threading.Tasks

module Async =

    let GreedyThrottle = Environment.ProcessorCount - 1

    let task<'T> f =
        Task<'T>(f)

    let parallelThrottledByProcessor tasks = Async.Parallel(tasks, GreedyThrottle)

    // Run operation, typically on a new thread and then its continuation on the current thread.
    let doBusyAsync continueWith operation =
        async {
            let! result = operation
            result |> continueWith
        }
        |> Async.StartImmediate

    // Run an operation on the the thread pool.
    let onThreadPool operation =
        async {
            let context = System.Threading.SynchronizationContext.Current
            do! Async.SwitchToThreadPool()

            let! result = operation

            do! Async.SwitchToContext context

            return result
        }

    type ThrottlingMessage<'T> = 
    | ThrottledJob of Async<'T>*('T -> unit)
    | Completed

    // A parallel throttling agent that forks a jobs and joins a context with a continuation.
    // Meant for UI parallel fork/join jobs.
    let throttlingAgent limit context = MailboxProcessor.Start(fun inbox -> async {
      let queue = System.Collections.Generic.Queue<_*_>()
      let running = ref 0
      while true do
        let! msg = inbox.Receive()
        match msg with
        | Completed -> decr running
        | ThrottledJob (job, continuation) -> queue.Enqueue(job, continuation)

        while running.Value < limit && queue.Count > 0 do
          let work, continuation = queue.Dequeue()
          incr running
          do! 
            async { 
                let! result = work
                inbox.Post(Completed)
                do! Async.SwitchToContext context
                result |> continuation
            }
            |> Async.StartChild
            |> Async.Ignore 
    })
namespace ReConstruct.Core

open System

module Time =

    open System.Diagnostics

    let clock f =
        let watch = new Stopwatch()
        watch.Start()
        let v = f()
        watch.Stop()
        (v, watch.Elapsed)
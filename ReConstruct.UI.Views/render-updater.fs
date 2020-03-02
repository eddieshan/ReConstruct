namespace ReConstruct.UI.View

open System

// An event based status updater that detaches itself when render is done.
module Render =

    let updater() =

        let updateRenderStatus = new Event<int*int*float>()
        let mutable polygonizedCount = 1
        let mutable finished = false
        let mutable totalTriangles = 0

        let handleUpdate (slicesCount, numberOfTriangles, time) =
            polygonizedCount <- polygonizedCount + 1
            totalTriangles <- totalTriangles + numberOfTriangles
            let percentage = (float polygonizedCount)*100.0/(float slicesCount)
            finished <- polygonizedCount = slicesCount
            match finished with
            | true  -> Events.Status.Trigger(sprintf "%fs | %i triangles" time totalTriangles)
            | false -> Events.Status.Trigger(sprintf "Completed %i of %i : %.0f%% | %i triangles" polygonizedCount slicesCount percentage totalTriangles)

        let start() =
            Events.Status.Trigger("Rendering volume, please wait ...")
            updateRenderStatus.Publish.Subscribe handleUpdate

        let subscription = start()
        
        let updateOrStop (count, numberOfTriangles, time) = 
            if finished then
                subscription.Dispose()
            else
                updateRenderStatus.Trigger (count, numberOfTriangles, time)

        updateOrStop
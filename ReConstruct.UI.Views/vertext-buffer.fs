namespace ReConstruct.UI.View

open System
open System.Collections.Generic

type VertexBuffer = 
    {
        Vertices: float32[]
        Add: float32 -> unit
        AddVertices: IList<float32> -> unit
        //Count: unit -> int
        Size: unit -> int
        ElementSize: unit -> int
        Reset: unit -> unit
    }

module VertexBuffer = 
    
    let New size =
        let mutable count = 0
        let buffer = Array.zeroCreate size
        
        let add v = 
            buffer.[count] <- v
            count <- count + 1

        let addVertices (vertices: IList<float32>) = 
            vertices.CopyTo(buffer, count)
            count <- count + vertices.Count

        {
            Vertices = buffer
            Add = add
            AddVertices = addVertices
            //Count = fun() -> count
            Size = fun() -> count*sizeof<float32>
            ElementSize = fun() -> sizeof<float32> 
            Reset = fun() -> count <- 0
        }
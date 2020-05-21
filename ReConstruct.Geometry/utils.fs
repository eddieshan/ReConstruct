namespace ReConstruct.Geometry

open System
open System.Buffers

module BufferPool =
    let private bufferPool = ArrayPool<float32>.Shared
    let Capacity = 9000
    let borrow() = bufferPool.Rent Capacity
    let ret buffer = bufferPool.Return buffer
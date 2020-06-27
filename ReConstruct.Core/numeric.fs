namespace ReConstruct.Core

open System
open System.Globalization

module Numeric =

    [<Literal>]
    let INT16_SIZE = 2

    [<Literal>] 
    let INT32_SIZE = 4

    [<Literal>] 
    let INT64_SIZE = 8

    [<Literal>] 
    let FLOAT32_SIZE = 4

    [<Literal>] 
    let DOUBLE64_SIZE = 8
   
    let inline even v = ((v >>> 1) <<< 1) = v

    let inline cap max = 
        function 
        | v when v > max -> max
        | v -> v
    
    let inline parseDouble s = Convert.ToDouble(s, CultureInfo.InvariantCulture)

    let inline toUInt16 buffer = BitConverter.ToUInt16(buffer, 0)
    let inline toInt16 buffer = BitConverter.ToInt16(buffer, 0)
    let inline toUInt32 buffer = BitConverter.ToUInt32(buffer, 0)
    let inline toInt32 buffer = BitConverter.ToInt32(buffer, 0)
    let inline toFloat buffer = BitConverter.ToSingle(buffer, 0)
    let inline toDouble buffer = BitConverter.ToDouble(buffer, 0)

module Byte =
    let MinAsInt, MaxAsInt = 0, 255
    let Min, Max = 0uy, 255uy
    let inline clamp v = Math.Min(MaxAsInt, Math.Max(v, MinAsInt)) |> Convert.ToByte
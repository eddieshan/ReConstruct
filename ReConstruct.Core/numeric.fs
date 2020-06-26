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
    
    let parseDouble s = Convert.ToDouble(s, CultureInfo.InvariantCulture)

    let toUInt16 buffer = BitConverter.ToUInt16(buffer, 0)
    let toInt16 buffer = BitConverter.ToInt16(buffer, 0)
    let toUInt32 buffer = BitConverter.ToUInt32(buffer, 0)
    let toInt32 buffer = BitConverter.ToInt32(buffer, 0)
    let toFloat buffer = BitConverter.ToSingle(buffer, 0)
    let toDouble buffer = BitConverter.ToDouble(buffer, 0)

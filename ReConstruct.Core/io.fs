namespace ReConstruct.Core

open System
open System.IO

module IO =
    let inline directoryFiles ext options path = 
        Directory.GetFiles(path, ext, options) 

    let inline memoryStream (buffer: byte[]) = MemoryStream(buffer)

    let inline binaryReader stream = BinaryReader(stream)
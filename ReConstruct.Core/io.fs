namespace ReConstruct.Core

open System
open System.IO

module IO =
    let directoryFiles ext options path = 
        Directory.GetFiles(path, ext, options) 

    let memoryStream (buffer: byte[]) = MemoryStream(buffer)

    let binaryReader stream = BinaryReader(stream)
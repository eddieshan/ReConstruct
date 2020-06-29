namespace ReConstruct.Data.Dicom

open System
open System.IO
open System.Text

open ReConstruct.Core

// Wrapper around a binary reader to read different types of values.
// Supports little or big endian encoding.
// Highly composable.
type DicomReader(reader: BinaryReader) =
    interface IDisposable with            
        member this.Dispose() = reader.Dispose()

    member x.readHex length =
        length |> reader.ReadBytes |> Array.fold(fun s t -> s + t.ToString("X2") + " ") String.Empty

    member x.readString length =
        let buffer = reader.ReadBytes(length)
        Encoding.Default
                .GetString(buffer)
                .Trim()
                .Replace(Environment.NewLine, " ")
                .Replace(Convert.ToChar(0x00).ToString(), "")

    member x.readBytes n = reader.ReadBytes n

    member x.endianReader =
        function
        | EndianEncoding.BigEndian -> reader.ReadBytes >> Array.rev
        | _ -> reader.ReadBytes

    member x.readAndRewind length = 
        let position = reader.BaseStream.Position
        let bytes = length |> reader.ReadBytes
        reader.BaseStream.Position <- position
        bytes

    member x.rewind i = reader.BaseStream.Position <- reader.BaseStream.Position - i

    member x.forward i = reader.BaseStream.Position <- reader.BaseStream.Position + i

    member x.goTo i = reader.BaseStream.Position <- i

    member x.position() = reader.BaseStream.Position

    member x.length() = reader.BaseStream.Length
namespace ReConstruct.Services

open System
open System.IO
open System.Text

open ReConstruct.Data.Dicom.BaseTypes

module internal Utils =

     // Wrapper around a binary reader to read different types of values.
     // Supports little or big endian encoding.
     // Highly composable.
    type DicomReader =
        {
            readString: int -> string;
            readBytes: int -> byte[];
            readAndRewind: int -> byte[];
            readHex: int -> string;
            rewind: int64 -> unit;
            forward: int64 -> unit;
            goTo: int64 -> unit;
            position: unit -> int64;
            length: unit -> int64;
            endianReader: EndianEncoding -> (int -> byte[]);
            Dispose: unit -> unit;
        }
        // A bit awkward implementation of IDisposable, forced by the syntax of record types.
        interface IDisposable with            
            member this.Dispose() = this.Dispose()
        static member New (reader: BinaryReader) =
            let readHex length =
                length |> reader.ReadBytes |> Array.fold(fun s t -> s + t.ToString("X2") + " ") String.Empty

            let readString length =
                let buffer = reader.ReadBytes(length)
                Encoding.Default
                        .GetString(buffer)
                        .Trim()
                        .Replace(Environment.NewLine, " ")
                        .Replace(Convert.ToChar(0x00).ToString(), "")

            let endianReader (reader: BinaryReader) =
                function
                | EndianEncoding.BigEndian -> reader.ReadBytes >> Array.rev
                | _ -> reader.ReadBytes

            let readAndRewind length = 
                let bytes = length |> reader.ReadBytes
                reader.BaseStream.Position <- reader.BaseStream.Position - (int64 length)
                bytes

            {
                readString = readString;
                readBytes = reader.ReadBytes;
                readAndRewind = readAndRewind;
                readHex = readHex;
                rewind = fun i -> reader.BaseStream.Position <- reader.BaseStream.Position - i;
                forward = fun i -> reader.BaseStream.Position <- reader.BaseStream.Position + i;
                goTo = fun i -> reader.BaseStream.Position <- i;
                position = fun _ -> reader.BaseStream.Position;
                length = fun _ -> reader.BaseStream.Length;
                endianReader = endianReader reader;
                Dispose = reader.Dispose;
            }
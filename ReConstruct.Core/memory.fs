namespace ReConstruct.Core

module Unsafe =

    open System.Runtime.InteropServices

    let UseAndDispose(size: int) f =
        let buffer =  Marshal.AllocHGlobal size
        buffer |> f
        Marshal.FreeHGlobal buffer
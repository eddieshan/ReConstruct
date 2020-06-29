namespace ReConstruct.Data.Dicom

open ReConstruct.Core

module Utils =

    let inline splitNumbers s = String.split '\\' s

    let inline parseLastDouble s = s |> splitNumbers |> Array.last |> Numeric.parseDouble       

    let inline parseDoubles (s: string) = s |> splitNumbers |> Array.map Numeric.parseDouble
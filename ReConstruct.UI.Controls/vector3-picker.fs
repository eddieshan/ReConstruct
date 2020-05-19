namespace ReConstruct.UI.Controls

open System
open System.Numerics
open System.Windows

open ReConstruct.UI.Core.UI

module Vector3Picker =

    let create onValueChanged (v: Vector3) = 
        let mutable current = [| v.X; v.Y; v.Z; |]

        let updateColor index value = 
            current.[index] <- value
            Vector3(current.[0], current.[1], current.[2]) |> onValueChanged
            
        current |> Seq.mapi(fun i c -> (0.1f, c) |> Spinner.create (updateColor i) Single.Parse :> UIElement) 
                |> childrenOf (stack "horizontal")
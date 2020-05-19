namespace ReConstruct.UI.Controls

open System
open System.Windows
open System.Windows.Media
open System.Windows.Controls

open ReConstruct.UI.Core.UI

module ColorPicker =

    let create color = 
        seq {
            let sample = Border(Background = SolidColorBrush(color), Style = style "color-sample")
            let mutable current = [| color.R; color.G; color.B |]

            let updateColor index value = 
                current.[index] <- byte value
                sample.Background <- SolidColorBrush(Color.FromRgb(current.[0], current.[1], current.[2]))
            
            yield! current |> Seq.mapi(fun i c -> (1, c) |> Spinner.create (updateColor i) Int32.Parse :> UIElement)
            yield sample :> UIElement
        } |> childrenOf (stack "horizontal")
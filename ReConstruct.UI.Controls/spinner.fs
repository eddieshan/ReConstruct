namespace ReConstruct.UI.Controls

open System
open System.Windows
open System.Windows.Controls

open ReConstruct.UI.Assets
open ReConstruct.UI.Core.UI

module Spinner =

    let spinner up down =
        seq {
            Icons.CHEVRON_UP |> button "icon-button-mini" |> onClick up
            Icons.CHEVRON_DOWN |> button "icon-button-mini" |> onClick down
        } |> childrenOf (stack "spinner")

    let inline create onValueChanged (convert: string->'T) (delta, value) =
        let textBox = TextBox(Text = value.ToString(), Style = style "spinner-text")
        textBox.TextChanged |> Event.add (fun _ -> textBox.Text |> convert |> onValueChanged)

        let update v =
            let newValue = (textBox.Text |> convert) + v
            textBox.Text <- newValue.ToString()

        seq {
            yield textBox :> UIElement
            yield (spinner (fun _ -> update delta) (fun _ -> update -delta)) :> UIElement
        } |> childrenOf (stack "horizontal")
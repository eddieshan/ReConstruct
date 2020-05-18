namespace ReConstruct.UI.Controls

open System
open System.Windows
open System.Windows.Controls

open ReConstruct.UI.Assets
open ReConstruct.UI.Core.UI

module Spinner =

    let private spinner up down =
        seq {
            Icons.CHEVRON_UP |> button "icon-button-mini" |> onClick up
            Icons.CHEVRON_DOWN |> button "icon-button-mini" |> onClick down
        } |> childrenOf (stack "spinner")

    let private updatableText value onTextChanged =
        let textBox = TextBox(Text = value.ToString())
        textBox.TextChanged |> Event.add (fun _ -> textBox.Text |> onTextChanged)
        let update delta =
            let newValue = (textBox.Text |> Convert.ToInt16) + delta
            textBox.Text <- newValue.ToString()
        (textBox, update)

    let create onTextChanged value =
        seq {
            let textBox, update = updatableText value onTextChanged
            yield textBox :> UIElement
            yield (spinner (fun _ -> update 1s) (fun _ -> update -1s)) :> UIElement
        } |> childrenOf (stack "horizontal")
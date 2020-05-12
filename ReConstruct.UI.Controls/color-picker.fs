namespace ReConstruct.UI.Controls

open System
open System.Drawing
open System.Windows
open System.Windows.Controls

open ReConstruct.UI.Assets

open ReConstruct.UI.Core.UI

module ColorPicker =

    let spinner up down =
        seq {
            Icons.CHEVRON_UP |> button "icon-button-mini" |> onClick up
            Icons.CHEVRON_DOWN |> button "icon-button-mini" |> onClick down
        } |> childrenOf (stack "spinner")

    let updatableText value =
        let textBox = TextBox(Text = value)
        let update delta =
            textBox.Text <- ((textBox.Text |> Convert.ToInt16) + delta).ToString()
        (textBox, update)

    let colorComponent caption value: UIElement seq =
        seq {
            yield TextBlock(Text = caption)
            let textBox, update = updatableText value
            yield textBox
            yield spinner (fun _ -> update 1s) (fun _ -> update -1s)
        }

    let create (color: Color) = 
        seq {
            yield! color.R.ToString() |> colorComponent "R"
            yield! color.G.ToString() |> colorComponent "G"
            yield! color.B.ToString() |> colorComponent "B"
        } |> childrenOf (stack "horizontal")
namespace ReConstruct.UI.Controls

open System
open System.Windows
open System.Windows.Media
open System.Windows.Controls

open ReConstruct.UI.Assets

open ReConstruct.UI.Core.UI

module ColorPicker =

    let private spinner up down =
        seq {
            Icons.CHEVRON_UP |> button "icon-button-mini" |> onClick up
            Icons.CHEVRON_DOWN |> button "icon-button-mini" |> onClick down
        } |> childrenOf (stack "spinner")

    let private updatableText value updateColor =
        let textBox = TextBox(Text = value.ToString())
        textBox.TextChanged |> Event.add (fun _ -> textBox.Text |> Convert.ToByte |> updateColor)
        let update delta =
            let newValue = (textBox.Text |> Convert.ToInt16) + delta |> byte
            textBox.Text <- newValue.ToString()
        (textBox, update)

    let private colorPart caption updateColor value: UIElement seq =
        seq {
            yield TextBlock(Text = caption)
            let textBox, update = updatableText value updateColor
            yield textBox
            yield spinner (fun _ -> update 1s) (fun _ -> update -1s)
        }

    let create color = 
        seq {
            let sample = Border(Background = SolidColorBrush(color), Style = style "color-sample")
            let updateColor color = sample.Background <- SolidColorBrush(color)
            yield! color.R |> colorPart "R" (fun r -> Color.FromRgb(r, color.G, color.B) |> updateColor)
            yield! color.G |> colorPart "G" (fun g -> Color.FromRgb(color.R, g, color.B) |> updateColor)
            yield! color.B |> colorPart "B" (fun b -> Color.FromRgb(color.R, color.G, b) |> updateColor)
            yield sample :> UIElement
        } |> childrenOf (stack "horizontal")
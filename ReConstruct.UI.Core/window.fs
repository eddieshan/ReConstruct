namespace ReConstruct.UI.Core

open System
open System.Windows

open ReConstruct.UI.Core.UI

module Window =
    let floatingPanel content =
        let layout = stack "vertical"
        let container = new Window(Style = style "floating-panel", Content = layout)
        let handleBar = stack "handlebar"
        handleBar.MouseLeftButtonDown |> Event.add(fun ev -> container.DragMove())

        let closePanel = textBlock "close-panel" "x"
        closePanel >- handleBar

        handleBar >- layout
        content >- layout
        container.Show()
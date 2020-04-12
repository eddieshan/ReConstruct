namespace ReConstruct.UI.Core

open System
open System.Windows
open System.Windows.Controls

open ReConstruct.UI.Core.UI

module Window =
    let floatingPanel title content =
        let layout = stack "vertical"
        let container = Window(Style = style "floating-panel", Content = layout)

        let panelTitle = textBlock "panel-caption" title
        let closePanel = "x" |> button "panel-button" |> withClick (fun _ -> container.Close())

        let handleBar = DockPanel(Style = style "panel-handle")
        handleBar.MouseLeftButtonDown |> Event.add(fun ev -> container.DragMove())
        closePanel |> dockTo handleBar Dock.Right
        panelTitle |> dockTo handleBar Dock.Left

        handleBar >- layout
        content >- layout
        container.Show()
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
        let closePanel = "x" |> button "panel-button" |> onClick (fun _ -> container.Close())

        let handleBar = "panel-handle" |> dock 
        handleBar.MouseLeftButtonDown |> Event.add(fun ev -> container.DragMove())
        closePanel |> dockTo handleBar Dock.Right
        panelTitle |> dockTo handleBar Dock.Left

        handleBar |> withBorder "panel-handle-border" >- layout
        content >- layout
        container.Show()
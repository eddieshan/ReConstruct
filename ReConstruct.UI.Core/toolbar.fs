namespace ReConstruct.UI.Core

open System
open System.Windows
open System.Windows.Controls

open ReConstruct.UI.Core.UI

module Toolbar =

    let New<'T when 'T :> UIElement> style (items: 'T seq) =
        let toolBar = border style
        let container = StackPanel()
        items |> Seq.iter(fun item -> item >- container)
        container |> onlyChildOf toolBar

        toolBar
    
    let Left<'T when 'T :> UIElement> (items: 'T seq) = items |> New "left-toolbar"

    let Right<'T when 'T :> UIElement> (items: 'T seq) = items |> New "right-toolbar"

    let top status items =
        let toolBar = border "top-toolbar"
        let container = DockPanel(HorizontalAlignment = HorizontalAlignment.Stretch, LastChildFill=true)
        items |> Seq.iter(fun item -> item |> dockTo container Dock.Left)
        status |> dockTo container Dock.Right
        container |> onlyChildOf toolBar

        toolBar

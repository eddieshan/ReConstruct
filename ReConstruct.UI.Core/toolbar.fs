namespace ReConstruct.UI.Core

open System
open System.Windows
open System.Windows.Controls

open ReConstruct.UI.Core
open ReConstruct.UI.Core.UI

module Toolbar =

    let vertical style items =
        let toolBar = border style
        items |> childrenOf (StackPanel()) |> onlyChildOf toolBar
        toolBar
    
    let left items = items |> vertical "left-toolbar"

    let right items = items |> vertical "right-toolbar"

    let top status items =
        let toolBar = border "top-toolbar"
        let container = DockPanel(HorizontalAlignment = HorizontalAlignment.Stretch)
        items |> Seq.iter(fun item -> item |> Dock.left container)
        status |> Dock.right container
        container |> onlyChildOf toolBar

        toolBar

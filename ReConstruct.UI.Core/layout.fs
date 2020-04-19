namespace ReConstruct.UI.Core

open System
open System.Windows.Controls

module Dock =

    let right (parent:DockPanel) child = 
        parent.Children.Add(child) |> ignore
        DockPanel.SetDock(child, Dock.Right)

    let left (parent:DockPanel) child = 
        parent.Children.Add(child) |> ignore
        DockPanel.SetDock(child, Dock.Left)

    let top (parent:DockPanel) child = 
        parent.Children.Add(child) |> ignore
        DockPanel.SetDock(child, Dock.Top)

    let bottom (parent:DockPanel) child = 
        parent.Children.Add(child) |> ignore
        DockPanel.SetDock(child, Dock.Bottom)
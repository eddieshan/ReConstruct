namespace ReConstruct.UI.View

open System
open System.Windows
open System.Windows.Controls

open ReConstruct.UI.Core.UI

module ContainerView = 
    let New navigationContainer =
        let progressIndicator = new Label(Style = style "progress-indicator", IsEnabled = false)
        Events.Progress.Publish |> Event.add (fun p -> progressIndicator.IsEnabled <- p)

        let root = new DockPanel()
        progressIndicator |> dockTo root Dock.Top
        navigationContainer |> dockTo root Dock.Left
        root :> UIElement
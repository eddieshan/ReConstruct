namespace ReConstruct.UI.View

open System
open System.Windows
open System.Windows.Controls

open ReConstruct.UI.Core
open ReConstruct.UI.Core.UI

module ContainerView = 
    let New navigationContainer =
        let progressIndicator = new Label(Style = style "progress-indicator", IsEnabled = false)
        Events.Progress.Publish |> Event.add (fun p -> progressIndicator.IsEnabled <- p)

        let root = new DockPanel()

        progressIndicator |> Dock.top root
        navigationContainer |> Dock.left root
        root :> UIElement
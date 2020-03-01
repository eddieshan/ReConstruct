namespace ReConstruct.UI.View

open System
open System.Windows

open ReConstruct.UI.Core.UI

module MessageView = 

    let New message =

        let container = stack "message-panel"

        message |> textBlock "message-text" >- container

        container :> UIElement


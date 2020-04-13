namespace ReConstruct.UI.View

open System
open System.Windows

open ReConstruct.UI.Core.UI

module PagerBar =

    let private Empty() = stack "pager-panel"

    let private withPages loadPage numPages =
        let bar = Empty()
        let selectedIcon = Icons.BROKEN_CIRCLE
        let unselectedIcon = Icons.CIRCLE
        let defaultPage = 0

        let buttons = [1..numPages] |> List.map(fun _ -> unselectedIcon |> iconButton)
        buttons.[defaultPage] |> withIcon selectedIcon
        let selectPage index =
            buttons |> List.iter(fun b -> b |> withIcon unselectedIcon)
            buttons.[index] |> withIcon selectedIcon
            index |> loadPage

        buttons |> List.iteri(fun index button -> button |> onClick (fun _ -> index |> selectPage) >- bar)

        defaultPage |> loadPage

        bar

    let New loadPage numPages =
        match numPages > 0 with
        | false -> Empty() :> UIElement
        | true -> numPages |> withPages loadPage :> UIElement

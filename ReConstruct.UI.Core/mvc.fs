namespace ReConstruct.UI.Core

open System
open System.Windows
open System.Windows.Controls
open System.Threading.Tasks

open ReConstruct.UI.Core.Actions

type Mvc private(loadView, route) =
    [<DefaultValue>]
    static val mutable private instance: Mvc

    member private this.route:AppAction -> (UIElement * ((unit -> unit) option)) = route
    member private this.loadView:(UIElement * ((unit -> unit) option)) -> unit = loadView

    static member configure (loadView, route) = Mvc.instance <- new Mvc(loadView, route)

    static member action action = action |> Mvc.instance.route

    static member send action = action |> Mvc.instance.route |> Mvc.instance.loadView
    
    static member partial action = action |> Mvc.instance.route

module Mvc =
    let basicView v = (v, None)
open System
open System.IO
open System.Windows
open System.Windows.Controls

open ReConstruct.UI.Core
open ReConstruct.UI.Core.UI
open ReConstruct.UI.Core.Actions
open ReConstruct.UI.View
open ReConstruct.UI


[<STAThread>]
[<EntryPoint>]
do
    // Bootstrap WPF. Load XAML resources and set up MVC.
    let app = Application()
    let executingPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)

    Styles.bundle app

    let navigationContainer = new Grid()
    
    let navigateToView (view: UIElement, onLoad) = 
        navigationContainer.Children.Clear()
        view >- navigationContainer
        onLoad |> Option.iter(fun f -> f())

    let window = navigationContainer |> ContainerView.New |> UI.window

    Mvc.configure (navigateToView, Router.handle)
    
    // Initial action is displaying available datasets.
    Open |> File |> Mvc.send
    
    app.Run(window) |> ignore
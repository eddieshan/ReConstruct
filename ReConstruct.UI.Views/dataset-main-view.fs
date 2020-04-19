namespace ReConstruct.UI.View

open System
open System.Windows
open System.Windows.Controls

open ReConstruct.Data.Dicom

open ReConstruct.UI.Core
open ReConstruct.UI.Core.UI
open ReConstruct.UI.Core.Actions

open ReConstruct.Render

module DatasetMainView =

    let New() =

        let container = DockPanel()

        container.Children.Clear()

        let contentView = DockPanel(LastChildFill = true)

        let loadPartial (view, onLoad) =
            view |> Option.iter(fun v ->
                                    Events.Status.Trigger(String.Empty)
                                    contentView.Children.Clear()
                                    v |> dockTo contentView Dock.Top)
            onLoad |> Option.iter(fun f -> f())

        let statusArea() =
            let status = String.Empty |> textBlock "status-caption"
            Events.Status.Publish |> Event.add(fun s -> status.Text <- s)
            Events.RenderStatus.Publish |> Event.add(fun s -> status.Text <- s)
            status

        let status = statusArea()
        
        let rightToolbar() = 
                
            let loadSlices dataset = dataset.Id |> LoadSlices |> Dicom |> Mvc.partial |> loadPartial
            let loadVolume dataset = 
                AppState.Level |> Option.iter(fun level -> (dataset.Id, level) |> LoadVolume |> Dicom |> Mvc.partial |> loadPartial)
            let openScalarFieldPanel dataset = dataset.Id |> OpenScalarFieldPanel |> Tool |> Mvc.send
            let loadTags dataset = dataset.Id |> LoadTags |> Dicom |> Mvc.partial |> loadPartial

            let buttons = [| 
                            (Icons.NEW |> iconButton, fun _ -> Open |> File |> Mvc.send)
                            (Icons.TAG |> iconButton, loadTags) |]

            let imagingButtons = [|
                                   (Icons.IMAGE_SERIES |> iconButton, loadSlices) 
                                   (Icons.VIEW_3D |> iconButton, loadVolume)
                                   (Icons.SCALAR_FIELD |> iconButton, openScalarFieldPanel)
                                   (Icons.SHIFTED_CIRCLE |> iconButton, fun _ -> OpenTransformPanel |> Tool |> Mvc.send) |]

            let menuButtons = imagingButtons |> Seq.append buttons |> Seq.map (fst >> disable >> asUIElement)
            let toolbar = menuButtons |> Toolbar.top status

            let loadContent dataset =
                buttons |> Seq.iter(fun (b, f) -> b |> enable |> onClick(fun _ -> dataset |> f) |> ignore)
                imagingButtons |> Seq.iter(fun (b, f) -> b |> enableIf dataset.HasPixelData |> onClick(fun _ -> dataset |> f) |> ignore)
                dataset |> loadSlices

            (toolbar, loadContent)

        let toolbar, loadToolbar = rightToolbar()
        
        let loadContent (entry, (time: TimeSpan)) =
            entry |> loadToolbar
            false |> Events.Progress.Trigger            
            Math.Round(time.TotalSeconds, 2) |> sprintf "%.2fs" |> Events.Status.Trigger

        toolbar |> dockTo container Dock.Top
        contentView |> dockTo container Dock.Bottom

        Events.Status.Trigger("Processing images, please wait ...")
        Events.Progress.Trigger(true)

        (container :> UIElement, loadContent)
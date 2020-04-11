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

        let header() =
            let header = DockPanel()
            let status = String.Empty |> textBlock "caption-text"
            status |> dockTo header Dock.Right

            let caption =  String.Empty |> textBlock "caption-text"
            caption |> dockTo header Dock.Left

            Events.Status.Publish |> Event.add(fun s -> status.Text <- s)
            Events.RenderStatus.Publish |> Event.add(fun s -> status.Text <- s)

            let loadContent entry = 
                let iodsInfo = match entry.HasPixelData with
                               | true  -> sprintf "%i slices with image data" entry.Iods.Length
                               | false -> sprintf "%i slices, no image data" entry.Iods.Length
                caption.Text <- sprintf "%s - %s" entry.SopClass iodsInfo

            (header, loadContent)
        
        let rightToolbar() = 
                
            let loadSlices dataset = dataset.Id |> LoadSlices |> Dicom |> Mvc.partial |> loadPartial
            let loadVolume dataset = (dataset.Id, Imaging.BONES_ISOVALUE) |> LoadVolume |> Dicom |> Mvc.partial |> loadPartial
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
            let toolbar = menuButtons |> Toolbar.Right

            let loadContent dataset =
                buttons |> Seq.iter(fun (b, f) -> b |> enable |> withClick(fun _ -> dataset |> f) |> ignore)
                imagingButtons |> Seq.iter(fun (b, f) -> b |> enableIf dataset.HasPixelData |> withClick(fun _ -> dataset |> f) |> ignore)
                dataset |> loadSlices

            (toolbar, loadContent)

        let toolbar, loadToolbar = rightToolbar()
        let header, loadHeader = header()
        
        let loadContent (entry, (time: TimeSpan)) =
            entry |> loadHeader
            entry |> loadToolbar
            false |> Events.Progress.Trigger
            
            Math.Round(time.TotalSeconds, 2) |> sprintf "%.2fs" |> Events.Status.Trigger

        toolbar |> dockTo container Dock.Right
        header |> dockTo container Dock.Top
        contentView |> dockTo container Dock.Top

        Events.Status.Trigger("Processing images, please wait ...")
        Events.Progress.Trigger(true)

        (container :> UIElement, loadContent)
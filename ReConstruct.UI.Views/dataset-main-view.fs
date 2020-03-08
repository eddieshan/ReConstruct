namespace ReConstruct.UI.View

open System
open System.Windows
open System.Windows.Controls

open ReConstruct.Core.String
open ReConstruct.Data.Dicom

open ReConstruct.UI.Core
open ReConstruct.UI.Core.UI
open ReConstruct.UI.Core.Actions

open ReConstruct.Data.Imaging

module DatasetMainView =

    module Camera =
        let OnRotation = new Event<Axis*float32>()
        let OnCameraMoved = new Event<float32>()
        let OnScale = new Event<float32>()

    let New() =

        let container = DockPanel()

        container.Children.Clear()

        let contentView = DockPanel(LastChildFill = true)

        let loadPartial (view, onLoad) =
            Events.Status.Trigger(String.Empty)
            contentView.Children.Clear()
            view |> dockTo contentView Dock.Top
            onLoad |> Option.iter(fun f -> f())

        let header() =
            let header = DockPanel()
            let status = String.Empty |> textBlock "caption-text"
            status |> dockTo header Dock.Right

            let caption =  String.Empty |> textBlock "caption-text"
            caption |> dockTo header Dock.Left

            Events.Status.Publish |> Event.add(fun s -> status.Text <- s)

            let loadContent entry = 
                let iodsInfo = match entry.HasPixelData with
                               | true  -> sprintf "%i slices with image data" entry.Iods.Length
                               | false -> sprintf "%i slices, no image data" entry.Iods.Length
                caption.Text <- sprintf "%s - %s" entry.SopClass iodsInfo

            (header, loadContent)

        let cameraTools() =
            let rotate v = fun _ -> v |> Camera.OnRotation.Trigger
            let moveCameraZ v = fun _ -> v |> Camera.OnCameraMoved.Trigger
            let scale v = fun _ -> v |> Camera.OnScale.Trigger

            let delta, zoomFactor, scaleFactor = 0.1f, 0.05f, 0.05f

            let addAxisControls(labelA, labelB, axis) = 
                seq {
                    yield labelA |> iconButton |> withClick (rotate (axis, delta)) :> UIElement
                    yield labelB |> iconButton |> withClick (rotate (axis, -delta)) :> UIElement
                    //yield label |> sprintf "-%s" |> iconButton |> withClick (increase (axis, -delta)) :> UIElement
                } 
            
            seq {
                yield! addAxisControls (Icons.CHEVRON_UP, Icons.CHEVRON_DOWN, Axis.X)
                yield! addAxisControls (Icons.CHEVRON_LEFT, Icons.CHEVRON_RIGHT,Axis.Y)
                yield! addAxisControls (Icons.ROTATE_LEFT, Icons.ROTATE_RIGHT, Axis.Z)
                yield Icons.CAMERA_IN |> iconButton |> withClick (moveCameraZ zoomFactor) :> UIElement
                yield Icons.CAMERA_OUT |> iconButton |> withClick (moveCameraZ -zoomFactor) :> UIElement
                yield Icons.ZOOM_IN |> iconButton |> withClick (scale scaleFactor) :> UIElement
                yield Icons.ZOOM_OUT |> iconButton |> withClick (scale -scaleFactor) :> UIElement
            }
        
        let rightToolbar() = 
                
            let loadSlices dataset = dataset.Id |> LoadSlices |> Dicom |> Mvc.partial |> loadPartial
            //let loadVolume dataset = (dataset.Id, Hounsfield.BONES_ISOVALUE) |> LoadVolume |> Dicom |> Mvc.partial |> loadPartial
            let loadVolume dataset = (dataset.Id, Hounsfield.SKIN_ISOVALUE) |> LoadVolume |> Dicom |> Mvc.partial |> loadPartial
            let loadTags dataset = dataset.Id |> LoadTags |> Dicom |> Mvc.partial |> loadPartial

            let buttons = [ (Icons.NEW |> iconButton, fun _ -> Open |> File |> Mvc.send)
                            (Icons.TAG |> iconButton, loadTags) ]

            let imagingButtons = [ (Icons.IMAGE_SERIES |> iconButton, loadSlices) 
                                   (Icons.VIEW_3D |> iconButton, loadVolume) ] 

            let menuButtons = imagingButtons |> Seq.append buttons |> Seq.map (fst >> disable >> asUIElement)
            let toolbar = cameraTools() |> Seq.append menuButtons |> Toolbar.Right
            //let toolbar = imagingButtons |> Seq.append buttons |> Seq.map (fst >> disable) |> Toolbar.Right

            let loadContent dataset =
                buttons |> Seq.iter(fun (b, f) -> b |> enable |> withClick(fun _ -> dataset |> f) |> ignore)
                imagingButtons |> Seq.iter(fun (b, f) -> b |> enableIf dataset.HasPixelData |> withClick(fun _ -> dataset |> f) |> ignore)
                //dataset |> loadTags
                dataset |> loadSlices

            (toolbar, loadContent)

        let toolbar, loadToolbar = rightToolbar()
        let header, loadHeader = header()
        
        let loadContent (entry, (time: TimeSpan)) =
            entry |> loadHeader
            entry |> loadToolbar
            false |> Events.Progress.Trigger
            
            Math.Round(time.TotalSeconds, 2) |> sprintf "%fs" |> Events.Status.Trigger

        toolbar |> dockTo container Dock.Right
        header |> dockTo container Dock.Top
        contentView |> dockTo container Dock.Top

        Events.Status.Trigger("Processing images, please wait ...")
        Events.Progress.Trigger(true)

        (container :> UIElement, loadContent)
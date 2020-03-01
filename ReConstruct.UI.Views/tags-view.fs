namespace ReConstruct.UI.View

open System
open System.IO
open System.Windows
open System.Windows.Controls
open System.Windows.Data
open System.Windows.Input

open ReConstruct.UI.Core
open ReConstruct.UI.Core.UI
open ReConstruct.UI.Core.Actions

open ReConstruct.Data.Dicom

module TagsView = 

    let New entry =

        let container = DockPanel()
        let contentView = new Grid()

        let newNode text = text |> treeViewItem "no-indent"

        let iodsList = listBox "list-box"
        
        let iodNode (index, iod: DicomInstance) = 
            let imageMark = match iod.CatSlice with
                            | Some _ -> "*" 
                            | None   -> ""
            let item = sprintf "%s %s" (Path.GetFileNameWithoutExtension(iod.FileName)) imageMark |> listBoxItem
            item.MouseUp |> Event.add(fun _ -> entry.Iods.[index] |> IodView.New |> loadContent contentView)
            item

        entry.Iods |> Array.iteri(fun index iod -> (index, iod) |> iodNode |> addItemTo iodsList)

//        container.FlowDirection <- FlowDirection.LeftToRight
//        container.LastChildFill <- true
        iodsList |> dockTo container Dock.Right
        contentView |> dockTo container Dock.Left

        container :> UIElement
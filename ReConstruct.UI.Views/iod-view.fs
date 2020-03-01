namespace ReConstruct.UI.View

open System
open System.Windows
open System.Windows.Controls

open ReConstruct.Core.String

open ReConstruct.Data.Dicom

open ReConstruct.UI.Core.UI

module IodView = 

    let private addAttribute (parent:ItemsControl) (element: DicomDataElement) =
        let tag, data = element.Tag, element.ValueField
        let name = tag |> Tags.getTagName

        if not (name.Contains("UID")) then
            let row = stack "horizontal"
            sprintf "%A %s: " tag name |> textBlock "tag-title" >- row
            data |> truncate 100 |> textBlock "caption-text" >- row
            row |> addItemTo parent

    let rec traverseTags f (node: DicomTree) =
        node.Tag |> f
        node.Children.Values |> Seq.iter(traverseTags f)

    let New (iod: DicomInstance) =
        let listbox = listBox "list-box"
        iod.DicomTree |> traverseTags (addAttribute listbox)
        listbox :> UIElement
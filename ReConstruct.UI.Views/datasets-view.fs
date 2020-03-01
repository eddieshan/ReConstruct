namespace ReConstruct.UI.View

open System
open System.Windows

open ReConstruct.UI.Core
open ReConstruct.UI.Core.UI
open ReConstruct.UI.Core.Actions

module DatasetsView = 

    let private entryRow (id, name) =
        let row = stack "horizontal"

        Icons.EDIT |> iconButton |> withClick (fun _ -> id |> DatasetEntry |> Dicom |> Mvc.send) >- row

        name |> textBlock "caption-text" >- row
        row

    let New entries =

        let contentView = stack "vertical-center"

        entries |> Seq.iter (fun entry -> entry |> entryRow >- contentView)

        contentView :> UIElement
namespace ReConstruct.UI

open System
open System.IO
open System.Windows
open System.Reflection

module Styles =

    let bundle (app: Application) =

        let executingPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)

        let newDictionary name =
            let path = Path.Combine(executingPath, name + ".xaml")
            ResourceDictionary(Source = Uri(path, UriKind.RelativeOrAbsolute))
        
        let addDictionary name = app.Resources.MergedDictionaries.Add(newDictionary name)

        seq [
            "base"
            "text"
            "text-box"
            "button"
            "check-box"
            "combo-box"
            "list-box"
            "scrollbar"
            "menu"
            "data-table"
            "pager"
            "form"
            "tree-view"
        ] |> Seq.iter addDictionary

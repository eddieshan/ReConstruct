namespace ReConstruct.UI

open System
open System.IO
open System.Windows
open System.Reflection

module Styles =

    let bundle (app: Application) =

        let mergeStyles dictionary = app.Resources.MergedDictionaries.Add dictionary
        let newDictionary xamlPath = ResourceDictionary(Source = Uri(xamlPath, UriKind.RelativeOrAbsolute))

        let executingPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
        Directory.GetFiles(executingPath, "*.xaml", SearchOption.TopDirectoryOnly) |> Seq.iter(newDictionary >> mergeStyles)
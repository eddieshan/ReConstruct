namespace RadiologyViewer.Core

open System
open System.Globalization
open System.Xml.Linq

open RadiologyViewer.Core.String

module Xml =

    let xElement name = new XElement(XName.Get(name))
    let xAttribute (name, value) = new XAttribute(XName.Get(name), value)

    let attribute name (element: XElement) = element.Attribute(XName.Get(name)).Value
    let descendants name (element: XDocument) = element.Descendants(XName.Get(name))
    let elements name (element: XElement) = element.Elements(XName.Get(name))

    let tag element = element |> attribute "Tag"
    let tagName element = element |> attribute "TagName"
    let tagData element = element |> attribute "Data" |> trim
    let dataElements element = element |> descendants "DataElement"

    let tagInfo = function
                  | Some e -> (e |> tag |> Some, e |> tagName |> Some, e |> tagData |> Some)
                  | None   -> (None, None, None)
    
    let hasTag name =
        function
        | null -> false
        | xDocument -> xDocument |> dataElements |> Seq.exists(fun e -> name = (tag e))
    
    let findTag xDocument name =
        match xDocument with
        | null -> None
        | _    -> xDocument |> dataElements |> Seq.filter(fun e -> name = (tag e)) |> Seq.tryLast

    let parseFloat (s: string) =
        let items = s.Split([| '\\' |])
        Convert.ToDouble(items.[items.Length - 1], CultureInfo.InvariantCulture)

    let stringTag xDocument = findTag xDocument >> Option.map tagData
    let tagOf xDocument f = findTag xDocument >> Option.map (tagData >> parseFloat >> f)
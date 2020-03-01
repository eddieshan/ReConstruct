namespace ReConstruct.UI.Core

open System
open System.Windows
open System.Windows.Controls

open ReConstruct.Core.String
open ReConstruct.Core.Patterns
open ReConstruct.UI.Core.UI

type DataCell<'T> =
    | IntCell of ('T -> int)
    | DecimalCell of ('T -> decimal)
    | StringCell of ('T -> string)
    | DateCell of ('T -> DateTime)
    | IconCell of ('T -> string)
    | StyleCell of ('T -> string*string)*string
    | ActionCell of ('T -> string*(unit -> unit))

type DataColumn<'T> = DataColumn of (string* int*DataCell<'T>)

type DataTable<'T> =
    {
        Root: UIElement;
        DisplayPage: 'T list -> unit;
        OnRowSelected: Event<'T>;
    }

// STACKPANEL ROWS IMPLEMENTATION.
module DataTable =

    let private columnBuilder column =
        let (DataColumn(title, width, cell)) = column

        let inline cellText (text, width, styleKey) = TextBlock(Text = text, Width = float width, Style = style styleKey)
        let typedCell style value = cellText(value, width, style) :> UIElement
        let styledCell (value, style) = cellText(value, width, style) :> UIElement
        let actionCell (icon, action) =  icon |> iconButton |> withClick(fun e -> action()) :> UIElement

        match cell with
        | IntCell(getValue)                         -> (fun r -> r |> getValue |> toString |> typedCell "numeric-cell"), cellText(title, width, "numeric-header")
        | DecimalCell(getValue)                     -> (fun r -> String.Format("{0:n2}", getValue r) |> typedCell "numeric-cell"), cellText(title, width, "numeric-header")
        | StringCell(getValue)                      -> (fun r -> r |> getValue |> typedCell "text-cell"), cellText(title, width, "text-header")
        | DateCell(getValue)                        -> (fun r -> r |> getValue |> toShortDate |> typedCell "date-cell"), cellText(title, width, "date-header")
        | IconCell(getValue)                        -> (fun r -> r |> getValue |> typedCell "icon-cell"), cellText(title, width, "icon-header")
        | StyleCell(getValueAndStyle, headerStyle)  -> (fun r -> r |> getValueAndStyle |> styledCell), cellText(title, width, headerStyle)
        | ActionCell(getIconAndAction)              -> (fun r -> r |> getIconAndAction |> actionCell), cellText(title, width, "text-header")

    let dataTable columns = 

        let columnBuilders = columns |> List.map(fun c -> columnBuilder c)

        let header = stack "data-header-row" |> branch(fun header -> columnBuilders |> List.iter(fun c -> c |> snd >- header))

        let pageContainer = StackPanel()

        let onRowSelected = Event<_>()

        let selectableRow dataRow = 
            let selectable = stack "data-row" |> branch(fun row -> columnBuilders |> List.iter(fun c -> dataRow |> (c |> fst) >- row))

            let selectRow() =
                onRowSelected.Trigger(dataRow) 
                selectable.Style <- style "data-row-selected"

            selectable.MouseLeftButtonUp |> Event.add (fun e -> selectRow())
            selectable

        let displayPage (data: 'T list) =
            pageContainer.Children.Clear()
            let rows = data |> List.map(fun dataRow -> dataRow |> selectableRow)
            let resetRows() = rows |> List.iter(fun r -> r.Style <- style "data-row")
            onRowSelected.Publish |> Event.add(fun r -> resetRows())
            rows |> List.iter(fun r -> r >- pageContainer)

        let pageBorder = border "page-container" |> withParent pageContainer

        let root = stack "data-table" |> withChild header |> withChild pageBorder

        {
            Root = root;
            DisplayPage = displayPage;
            OnRowSelected = onRowSelected;
        }


// STACKPANEL ROWS RECURSIVE IMPLEMENTATION.
//module DataTable =
//
//    let cellText (text, width, styleKey) = TextBlock(Text = text, Width = float width, Style = style styleKey)
//
//    let private header columns =
//        let styleOf column = 
//            match column.Cell with 
//                | NumericCell(_) -> "numeric-header"
//                | DateCell(_) -> "date-header" 
//                | StringCell(_) -> "text-header"
//                | IconCell(_) -> "icon-header"
//                 
//        let columnHeader column = cellText(column.Title, column.Width, styleOf column)
//        let header = StackPanel(Orientation = Orientation.Horizontal)
//        columns |> Array.iter(fun c -> columnHeader c >- header)
//        header
//
//    let private row columns dataRow = 
//        let cell column = 
//            match column.Cell with
//                | NumericCell(d) -> cellText(String.Format("{0:n2}", d dataRow), column.Width, "numeric-cell")
//                | StringCell(s) -> cellText(s dataRow, column.Width, "text-cell")
//                | DateCell(d) -> cellText((d dataRow).ToShortDateString(), column.Width, "date-cell")
//                | IconCell(s) -> cellText(s dataRow, column.Width, "icon-cell")
//        
//        let row = stack "data-row")
//        columns |> Array.iter(fun c -> cell c >- row)
//        row
//
//    let pager displayPage dataPages = 
//
//        let container = stack "pager")
//
//        let rec resetButtons index (elements: UIElementCollection) = 
//            if(index < elements.Count) then
//                let head = elements.Item(index)
//                if head :? Button then
//                    (head :?> Button).Style <- style "page-button"
//                elif head :? StackPanel  then
//                    resetButtons 0 (head :?> StackPanel).Children
//                resetButtons (index + 1) elements
//        
//        let pageButton caption data = 
//            let onPageSelected (b: Button) = 
//                resetButtons 0 container.Children
//                b.Style <- style "page-button-selected"
//                displayPage data
//            Button(Content = caption, Style = style "page-button") |> withClick(fun e -> onPageSelected (e.Source :?> Button))
//
//        let rec pageControl dataPages = 
//            match dataPages with
//                | [] -> []
//                | Page(data, caption) :: tail -> (pageButton caption data :> UIElement) :: pageControl tail
//                | Marker(caption, pages ):: tail -> 
//                                    let groupContainer = stack "page-group")
//                                    let buttonsContainer = stack  "page-group-buttons")
//                                    let selectGroup (marker: Button) = 
//                                        let resetGroup (group: StackPanel) =
//                                            (group.Children.Item(0) :?> Button).Style <- style "page-marker"
//                                            group.Children.Item(1) |> collapse
//
//                                        (groupContainer.Parent :?> StackPanel).Children |> whenChildIs (fun s -> resetGroup s)
//                                        buttonsContainer |> show
//                                        marker.Style <- style "page-marker-selected"
//
//                                    Button(Content = caption, Style = style "page-marker") |> withClick(fun e -> selectGroup (e.Source :?> Button)) >- groupContainer
//                                    pages |> pageControl |> childrenOf buttonsContainer >- groupContainer
//
//                                    buttonsContainer |> collapse
//                                    (groupContainer :> UIElement) :: pageControl tail
//
//        dataPages |> pageControl |> childrenOf container
//
//    let New dataPages columns =
//
//        let root = stack "data-table")
//
//        let pageContainer = StackPanel()
//        let pageBorder = Border(Style = style "page-container")        
//
//        let displayPage data =
//            pageContainer.Children.Clear()
//            data |> List.iter(fun dataRow -> dataRow |> row columns >- pageContainer)
//
//        columns |> header >- root
//        pageContainer |> rootChildOf pageBorder
//        pageBorder >- root
//
//        let pagerBar = dataPages |> pager displayPage
//        pagerBar >- root
//
//        pagerBar.Children |> withFirstOf(fun (b: Button) -> b.RaiseEvent(RoutedEventArgs(ButtonBase.ClickEvent)))
//
//        root


// CANVAS IMPLEMENTATION.
//module DataTable =
//
//    let textAt (canvas: Canvas) (width: int) x y text  =
//        Canvas.SetLeft(text, x)
//        Canvas.SetTop(text, y)
//        canvas.Children.Add(text) |> ignore
//        x + (float width)
//    
//    let cell column dataRow = 
//        let cellText (text, width, styleKey) = TextBlock(Text = text, Width = float width, Height = 20.0, Style = style styleKey)
//        match column.Cell with
//            | NumericCell(d) -> cellText(String.Format("{0:n2}", d dataRow), column.Width, "numeric-cell")
//            | StringCell(s) -> cellText(s dataRow, column.Width, "text-cell")
//            | DateCell(d) -> cellText((d dataRow).ToShortDateString(), column.Width, "date-cell")
//            | IconCell(s) -> cellText(s dataRow, column.Width, "icon-cell")
//
//    let rowAt dataRow columns (canvas: Canvas) y =
//        let cellAt column dataRow x y = cell column dataRow |> textAt canvas column.Width x y        
//        columns |> Array.fold(fun x c -> cellAt c dataRow x y) 0.0 |> ignore
//        y + 30.0
//
//    let New data columns =
//        let root = Canvas()        
//        data |> List.fold(fun y d -> rowAt d columns root y) 0.0 |> ignore
//        root

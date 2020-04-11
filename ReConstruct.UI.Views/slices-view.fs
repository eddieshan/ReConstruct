namespace ReConstruct.UI.View

open System
open System.Windows
open System.Windows.Controls
open System.Windows.Media
open System.Windows.Media.Imaging

open ReConstruct.Data.Dicom

open ReConstruct.UI.Core.UI

module Imaging = 
    let bitmapFrom(columns, rows, buffer: byte[]) =
        let bitmap = new WriteableBitmap(columns, rows, 96.0, 96.0, PixelFormats.Pbgra32, null)
        let rectangle = new Int32Rect(0, 0, columns, rows)
        let stride = (columns * bitmap.Format.BitsPerPixel) / 8
        bitmap.WritePixels(rectangle, buffer, stride, 0)
        bitmap.Freeze()
        bitmap :> BitmapSource

module SlicesView = 

    type private SliceCell =
        {
            Image: Image
            Title: TextBlock
        } with
        member x.Set iod =
            iod.Slice |> Option.iter(fun v -> x.Image.Source <- v |> Imaging.getBitmap |> Imaging.bitmapFrom)
            x.Title.Text <- iod.SortOrder |> sprintf "Slice %O"

    // Uses a sliding window pattern to page through all the slices in the study series.
    let New (iods: DicomInstance[]) =

        let container = DockPanel()

        let sliceContainer = stack "vertical"

        let columns = 5
        let rows = 3
        let pageSize = rows*columns
        let pageCount = iods.Length/pageSize
        let widthOffset = 80.0
        let heightOffset = 40.0

        let titleHeight = 20.0
        let caption text = TextBlock(Text = text, Style = style "caption-text", Height = titleHeight)

        // Estimate full screen size and partition space for each slice image.
        let width, height = (SystemParameters.PrimaryScreenWidth - widthOffset), (SystemParameters.PrimaryScreenHeight - heightOffset)
        let cellWidth = width/(float columns)
        let cellHeight = (height - titleHeight*(float rows))/(float rows)

        let sliceCell() = 
            {
                Image = Image(Width = cellWidth, Height = cellHeight)
                Title = caption ""
            }

        let newCell cell =
            let cellContainer = stack "vertical"
            cell.Image >- cellContainer
            cell.Title >- cellContainer
            cellContainer

        let sliceRow (cells: SliceCell[]) =
            let row = stack "horizontal"
            cells |> Array.iter(fun cell -> cell |> newCell >- row)
            row

        // Initialize columns*rows empty slice cells.
        let cells = seq { 1..pageSize } |> Seq.map(fun _ -> sliceCell()) |> Seq.toArray

        // Page index starts at zero.
        let loadPage pageNumber =
            let startIndex = pageSize*pageNumber
            let endIndex  = startIndex + pageSize - 1
            seq { startIndex..endIndex } |> Seq.iteri(fun cellIndex sliceIndex -> iods.[sliceIndex] |> cells.[cellIndex].Set)

        cells |> Array.chunkBySize columns |> Array.iter(fun cell -> cell |> sliceRow >- sliceContainer)
        sliceContainer |> dockTo container Dock.Left
        pageCount |> PagerBar.New loadPage |> dockTo container Dock.Right

        container :> UIElement
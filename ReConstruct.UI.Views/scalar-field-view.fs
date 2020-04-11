namespace ReConstruct.UI.View

open System
open System.Collections.Generic
open System.Windows.Controls
open System.Windows.Shapes
open System.Windows.Media

open ReConstruct.Data.Dicom

open ReConstruct.UI.Core.UI

module ScalarFieldView = 

    let private maxHeight = 200

    let private scrollable control = 
        let scroller = ScrollViewer(HorizontalScrollBarVisibility = ScrollBarVisibility.Visible)
        scroller.Content <- control
        scroller

    let private chartContainer height = 
        let chart = canvas "vertical-bars-container"
        chart.Height <- float height
        chart

    let private verticalBar n height =
        let x = (float n)*7.0
        let bar = Line(X1 = x, Y1 = 0.0, X2 = x, Y2 = float height, Style = style "vertical-bar")
        Canvas.SetBottom(bar, 0.0)
        Canvas.SetLeft(bar, 0.0)
        bar

    let valueOcurrences = Imaging.getValuesCount >> Array.sortByDescending snd
        
    let New (slices: ImageSlice[]) =
        let valuesCount = slices |> Array.Parallel.map valueOcurrences
        let countTable = new Dictionary<int, int> (500)

        let addCount (hValue, count) =    
            if countTable.ContainsKey hValue then
                countTable.[hValue] <- countTable.[hValue] + count
            else
                countTable.[hValue] <- count

        valuesCount |> Seq.iter(Seq.iter addCount)

        let descendingCounts = countTable |> Seq.sortByDescending(fun pair -> pair.Value) |> Seq.toArray
        let maxCount = (descendingCounts |> Seq.head).Value
        let mapHeight count = (float (count*maxHeight))/(float maxCount)

        let totalsHeight = descendingCounts |> Array.map(fun pair -> (pair.Key, mapHeight pair.Value))

        let trendHeight trend = 
            match trend with
            | None -> 0.0
            | Some (value, _) -> totalsHeight |> Seq.find(fun (v, _) -> v = value) |> snd               
        
        let slicesCountChart = chartContainer maxHeight
        valuesCount |> Seq.iteri(fun i levels -> levels |> Seq.tryHead |> trendHeight |> verticalBar i >- slicesCountChart)

        let totalCountChart = chartContainer maxHeight
        totalsHeight |> Seq.iteri(fun i (_, height) -> height |> verticalBar i >- totalCountChart)        

        let scalarFieldView = stack "slice-levels"
        slicesCountChart |> scrollable >- scalarFieldView
        totalCountChart |> scrollable >- scalarFieldView

        scalarFieldView
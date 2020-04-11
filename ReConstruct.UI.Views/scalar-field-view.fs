namespace ReConstruct.UI.View

open System
open System.Collections.Generic
open System.Windows.Controls
open System.Windows.Shapes

open ReConstruct.Data.Dicom

open ReConstruct.UI.Core.UI

module ScalarFieldView = 

    let private maxHeight = 500

    let private verticalBar n height =
        Line(Y1 = float maxHeight - height, Y2 = float maxHeight, Style = style "vertical-bar")

    let valueOcurrences slice =
        let sliceLevels = slice.GetValuesCount() |> Array.sortByDescending snd
        let trend = sliceLevels |> Seq.tryHead |> Option.defaultValue (0, 0)
        let total = sliceLevels |> Seq.sumBy snd
        (sliceLevels, trend, total)
        
    let New (slices: ImageSlice[]) =
        let valuesCount = slices |> Array.Parallel.map valueOcurrences
        let countTable = new Dictionary<int, int> (500)

        let addCount (hValue, count) =    
            if countTable.ContainsKey hValue then
                countTable.[hValue] <- countTable.[hValue] + count
            else
                countTable.[hValue] <- count

        valuesCount |> Seq.iter(fun (sliceLevels, _, _) -> sliceLevels |> Seq.iter addCount)

        let mapVBarSize total count = 
            if total = 0 then 
                0.0
            else
                (float (count*maxHeight))/(float total)
        
        let slicesCountChart = stack "vertical-bars-container"

        valuesCount |> Seq.iteri(fun i (_, (hValue, count), total) -> count |> mapVBarSize total |> verticalBar i >- slicesCountChart)

        let descendingCounts = countTable |> Seq.sortByDescending(fun pair -> pair.Value) 
        let maxCount = (descendingCounts |> Seq.head).Value
        
        let mapHBarSize count = (float (count*maxHeight))/(float maxCount)

        let totalCountChart = stack "vertical-bars-container"
        descendingCounts |> Seq.iteri(fun i pair -> pair.Value |> mapHBarSize |> verticalBar i >- totalCountChart)

        let scroller = ScrollViewer(HorizontalScrollBarVisibility = ScrollBarVisibility.Visible)

        let scalarFieldView = stack "slice-levels"
        slicesCountChart >- scalarFieldView
        scroller.Content <- totalCountChart
        scroller >- scalarFieldView

        scalarFieldView


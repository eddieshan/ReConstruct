namespace ReConstruct.UI.View

open System
open System.Collections.Generic
open System.Windows.Controls
open System.Windows.Shapes
open System.Windows.Media

open ReConstruct.Data.Dicom

open ReConstruct.UI.Core.UI

module ScalarFieldView = 

    let private maxHeight, itemWidth, barWidth = 200.0, 30.0, 10.0
    let private relativeBarOffset = (itemWidth - barWidth)/2.0

    let private scrollable control = ScrollViewer(Style = style "horizontal-scroller", Content = control)

    let private attachToCanvas (left, bottom) control =
        Canvas.SetLeft(control, left)
        Canvas.SetBottom(control, bottom)

    let private chartContainer numItems = Canvas(Style = style "levels-chart", Height = maxHeight, Width = (float numItems)*itemWidth)

    let private verticalBar root n value height =
        let offset = (float n)*itemWidth

        let label = value.ToString() |> textBlock "level-label"
        label |> attachToCanvas (offset, 0.0)
        label >- root

        let bar = Line(X1 = 0.0, Y1 = 0.0, X2 = 0.0, Y2 = float height, Style = style "level-bar", StrokeThickness = barWidth)
        bar |> attachToCanvas (offset + relativeBarOffset, 25.0)
        bar >- root
        
    let New (slices: ImageSlice[]) =
        let valuesCount = slices |> Array.Parallel.map Imaging.getValuesCount
        let countTable = new Dictionary<int, int> (500)

        let addCount (hValue, count) =    
            if countTable.ContainsKey hValue then
                countTable.[hValue] <- countTable.[hValue] + count
            else
                countTable.[hValue] <- count

        valuesCount |> Seq.iter(Seq.iter addCount)

        let sortedLevels = countTable |> Seq.sortBy(fun pair -> pair.Key) |> Seq.toArray
        let maxCount = countTable |> Seq.maxBy(fun p -> p.Value)
        let minCount = countTable |> Seq.minBy(fun p -> p.Value)
        let countRange = (maxCount.Value - minCount.Value) |> float
        let mapHeight count = (float count)*maxHeight/countRange
        
        let slicesCountChart = chartContainer valuesCount.Length
        valuesCount |> Seq.iteri(fun i levels -> 
                                    match levels |> Seq.isEmpty with
                                    | true -> verticalBar slicesCountChart i 0 0.0
                                    | false -> 
                                        let value, count = levels |> Seq.maxBy(fun (_, count) -> count)
                                        count |> mapHeight |> verticalBar slicesCountChart i value)

        let totalCountChart = chartContainer sortedLevels.Length
        sortedLevels |> Seq.iteri(fun i p -> p.Value |> mapHeight |> verticalBar totalCountChart p.Key i)

        let scalarFieldView = stack "slice-levels"        
        totalCountChart |> scrollable >- scalarFieldView
        slicesCountChart |> scrollable >- scalarFieldView

        scalarFieldView
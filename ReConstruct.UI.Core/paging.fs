namespace ReConstruct.UI.Core

open System

type DataPage<'T> =
    | Page of 'T list*string
    | Marker of string*DataPage<'T> list

module PagePartition =
    let ByYearAndMonth (getDate: 'T -> DateTime) data =
        let dataPage year byMonth =
              let forMonth (month, data) = data |> List.chunkBySize 28 |> List.map(fun d -> Page(d, (Utils.monthName month)))
              let allMonths = byMonth |> List.collect(fun g -> forMonth g)
              Marker(year.ToString(), allMonths)
                      
        data 
            |> List.groupBy(fun o -> getDate(o).Year)
            |> List.map(fun g -> (snd g |> List.groupBy(fun o -> getDate(o).Month)) |> dataPage (fst g))

    let ByYear (getYear: 'T -> int) data = data |> List.groupBy(fun o -> getYear o) |> List.map(fun g -> Page(snd g, (fst g).ToString()))
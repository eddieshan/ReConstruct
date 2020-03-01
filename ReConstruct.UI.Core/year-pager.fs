namespace ReConstruct.UI.Core

open System

open ReConstruct.UI.Core

module YearPager =
    let New (displayPage: 'T list -> unit) (getYear: 'T -> int) data =

        data 
            |> List.groupBy(fun o -> getYear o)
            |> List.map(fun g -> Page(snd g, (fst g).ToString()))
            |> TreePager.New displayPage
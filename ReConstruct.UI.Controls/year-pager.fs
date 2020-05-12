namespace ReConstruct.UI.Controls

open System

module YearPager =
    let New (displayPage: 'T list -> unit) (getYear: 'T -> int) data =

        data 
            |> List.groupBy(fun o -> getYear o)
            |> List.map(fun g -> Page(snd g, (fst g).ToString()))
            |> TreePager.New displayPage
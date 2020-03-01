namespace ReConstruct.Core

open System

module Seq =
    let justOne filter items = items |> Seq.filter filter |> Seq.exactlyOne

    let scanDuple seed addTo duples =
        let initialValue = duples |> Seq.head |> (fun (first, second) -> (first, seed first second))
        duples |> Seq.tail |> Seq.scan (fun (_, state) (first, second) -> (first, second |> addTo first state)) initialValue

module List =
    let justOne filter items = items |> List.filter filter |> List.exactlyOne
    let groupByFirst keys items = keys |> List.map(fun p -> (p, items |> List.filter(fun o -> p = fst o) |> List.map(fun o -> (snd o))))


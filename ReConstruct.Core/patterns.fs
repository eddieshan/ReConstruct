namespace ReConstruct.Core

open System

module Option =
    let defaultValue def =
        function 
        | None -> def
        | Some v -> v

    let fromValue v f =
        match v with
        | null -> None
        | _ -> v |> f

    let fromTrue f v =
        match v with
        | true -> f() |> Some
        | _ -> None

module Patterns = 
    let inline branch f v = 
        f v
        v

    let loop2Di action (rows, columns, increment) =
        let mutable index = 0
        for row in 0..rows - 1 do
            for column in 0..columns  -1 do
                action rows columns
                index <- index + increment


namespace ReConstruct.UI.Core

open System

open ReConstruct.Core.String

type Binding<'T> =
    {
        Key: string;
        Value: string;
        TryParse: string -> bool*'T;
    }

type SelectionBinding<'T> =
    {
        Key: string;
        Selected: string;
        Options: string list;
        value: int -> 'T;
    }

module Bind = 
    let private parseOptional tryParse value = 
        let asOption (isValid, parsedValue) = (isValid, Some parsedValue)
        if String.IsNullOrEmpty(value) then (true, None) else value |> tryParse |> asOption

    let asList options displayText value=
        {
            Key = "text";
            Selected = value |> displayText;
            Options = options |> List.map(fun o -> o |> displayText);            
            value = fun index -> options.Item(index);
        }

    let asInt (value: int) =
        {
            Value = value |> toString;
            Key = "numeric";
            TryParse = Int32.TryParse;
        }

    let asDecimal (value: decimal) =
        {
            Value = value |> toString;
            Key = "numeric";
            TryParse = Decimal.TryParse;
        }

    let asDecimalOption (value: decimal option) =
        {
            Value = value |> toString;
            Key = "numeric";
            TryParse = parseOptional Decimal.TryParse;
        }

    let asString (value: string) =
        {
            Value = value;
            Key = "text";
            TryParse = fun s -> (true, s);
        }

    let asDate (value: DateTime) =
        {
            Value = value |> toShortDate;
            Key = "date";
            TryParse = DateTime.TryParse;
        }

    let asDateOption (value: DateTime option) =
        {
            Value = match value with | Some date -> date |> toShortDate | None -> "";
            Key = "date";
            TryParse = parseOptional DateTime.TryParse;
        }


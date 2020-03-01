namespace ReConstruct.Core

open System

module String =
    let toString o = o.ToString()

    let padRight length (s: string) = s.PadRight(length)

    let truncate maxLength (s: string) = 
        if s.Length > maxLength then
            s.Substring(0, maxLength) + "..."
        else
            s

    let toShortDate (date: DateTime) = date.ToShortDateString()
    let format f o = String.Format(f, o)

    let trim (s: string) = s.Trim()
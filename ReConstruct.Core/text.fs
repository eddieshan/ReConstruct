namespace ReConstruct.Core

open System

module String =
    let inline toString o = o.ToString()

    let inline split (t: char) (s: string) = s.Split t

    let inline padRight length (s: string) = s.PadRight(length)

    let inline truncate maxLength (s: string) = 
        if s.Length > maxLength then
            s.Substring(0, maxLength) + "..."
        else
            s

    let inline toShortDate (date: DateTime) = date.ToShortDateString()
    let inline format f o = String.Format(f, o)

    let inline trim (s: string) = s.Trim()
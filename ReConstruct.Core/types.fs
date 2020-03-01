namespace ReConstruct.Core

open System

open FSharp.Reflection

module Types =

    let arraySize<'T>(v: 'T[]) = v.Length*sizeof<'T>

    let func<'T> f =
        Func<'T>(f)

    let action<'T> f =
        Action<'T>(f)

    let arrayElementSize<'T>(v: 'T[]) = sizeof<'T>

    type Id = Id of int with 
        override this.ToString() = match this with | Id id -> id.ToString()
        static member Parse = Int32.Parse >> Id

    type IdValue<'M> =
        {
            Id: Id;
            Value: 'M;
        }

    // Useful type to enumerate unions and converting their cases to/from string.
    // Cases for each type are implicitly memoized thanks to static generics.
    // Only applicable to enum unions, throws exception otherwise.
    type EnumUnion<'T> when 'T: equality private() =
        static member val private _cases = 
            // Reflection generated catalogue of cases.
            assert FSharpType.IsUnion(typeof<'T>)
            let makeCase case = 
                let value = FSharpValue.MakeUnion(case, [||]) :?> 'T
                (case.Name, value)
            let cases = FSharpType.GetUnionCases typeof<'T>
            cases |> Array.map makeCase |> Map.ofArray

        static member tryParse s = EnumUnion<'T>._cases.TryFind s

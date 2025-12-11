namespace Xanthos.Interop

open System

[<AutoOpen>]
module ComInterop =

    let inline marshalToManaged (value: obj) = value

    let inline toNullableDate (value: obj) =
        match value with
        | :? string as text when String.IsNullOrWhiteSpace text -> None
        | :? string as text ->
            match DateTime.TryParse text with
            | true, dt -> Some dt
            | _ -> None
        | _ -> None

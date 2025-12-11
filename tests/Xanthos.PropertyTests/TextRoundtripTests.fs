module Xanthos.PropertyTests.TextRoundtripTests

open System
open FsCheck
open FsCheck.Xunit
open Xanthos.Core

let isPrintable (c: char) =
    // Allow common ASCII and some whitespace; exclude control chars that SJIS may not round-trip
    (c >= ' ' && c <= '~') || c = '\n' || c = '\r' || c = '\t'

[<Property(MaxTest = 200)>]
let ``ShiftJIS encode/decode round-trips for printable ASCII`` (chars: char list) =
    let filtered = chars |> List.filter isPrintable |> Array.ofList
    let text = new string (filtered)
    let encoded = Text.encodeShiftJis text
    let decoded = Text.decodeShiftJis encoded
    decoded = text

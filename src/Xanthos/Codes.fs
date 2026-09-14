namespace Xanthos

/// Official JV-Data code table identifiers; widths refer to bytes, including spaces.
[<RequireQualifiedAccess>]
type CodeTable =
    | Racecourse
    | Weekday
    | Grade
    | RaceKind
    | RaceSymbol
    | RaceCondition
    | WeightRule
    | Track
    | TrackCondition
    | Weather
    | Abnormality
    | Margin
    | Breed
    | Sex
    | Coat
    | HorseSymbol
    | Affiliation
    | JockeyQualification
    | Apprentice

/// A recognized code has a label; an unrecognized code retains its exact representation.
type OfficialCode =
    internal
        { Table: CodeTable
          Raw: string
          Label: string option }

[<RequireQualifiedAccess>]
module Codes =
    let table code = code.Table
    let raw code = code.Raw
    let label code = code.Label
    let isKnown code = code.Label.IsSome

    let number =
        function
        | CodeTable.Racecourse -> 2001
        | CodeTable.Weekday -> 2002
        | CodeTable.Grade -> 2003
        | CodeTable.RaceKind -> 2005
        | CodeTable.RaceSymbol -> 2006
        | CodeTable.RaceCondition -> 2007
        | CodeTable.WeightRule -> 2008
        | CodeTable.Track -> 2009
        | CodeTable.TrackCondition -> 2010
        | CodeTable.Weather -> 2011
        | CodeTable.Abnormality -> 2101
        | CodeTable.Margin -> 2102
        | CodeTable.Breed -> 2201
        | CodeTable.Sex -> 2202
        | CodeTable.Coat -> 2203
        | CodeTable.HorseSymbol -> 2204
        | CodeTable.Affiliation -> 2301
        | CodeTable.JockeyQualification -> 2302
        | CodeTable.Apprentice -> 2303

    let width =
        function
        | CodeTable.Racecourse
        | CodeTable.RaceKind
        | CodeTable.Track
        | CodeTable.Coat
        | CodeTable.HorseSymbol -> 2
        | CodeTable.RaceSymbol
        | CodeTable.RaceCondition
        | CodeTable.Margin -> 3
        | _ -> 1

    // Generated declarations are separate from the independently extracted test oracle.
    let private definitions = OfficialCodeData.entries |> Map.ofList

    /// Parse without trimming, case folding, numeric conversion or discarding unknown values.
    let parse table (raw: string) =
        if
            isNull raw
            || raw.Length <> width table
            || raw |> Seq.exists (fun c -> c < ' ' || c > '~')
        then
            Error "A code must contain exactly the table's width in printable ASCII bytes."
        else
            let label = definitions |> Map.tryFind (number table, raw)

            Ok
                { Table = table
                  Raw = raw
                  Label = label }

namespace Xanthos.Data

open System
open Xanthos

/// Select the published odds limit rules explicitly when decoding historical data.
[<RequireQualifiedAccess>]
type OddsLimitFormat =
    | Current
    | Before20040814

[<RequireQualifiedAccess>]
type OddsValue =
    | NotRegistered
    | NoVotes
    | CancelledBeforeSale
    | CancelledAfterSale
    | Quoted of decimal
    | AtLeast of decimal

type SingleOddsEntry =
    { Combination: string
      Odds: Sourced<OddsValue>
      Popularity: Sourced<Popularity> }

type RangeOddsEntry =
    { Combination: string
      Minimum: Sourced<OddsValue>
      Maximum: Sourced<OddsValue>
      Popularity: Sourced<Popularity> }

type WinPlaceBracketOdds =
    { Header: RecordHeader
      Identity: RaceIdentity
      Announcement: Sourced<AnnouncementTime option>
      RegisteredCount: Sourced<int option>
      RunnerCount: Sourced<int option>
      WinSale: Sourced<SaleState>
      PlaceSale: Sourced<SaleState>
      BracketSale: Sourced<SaleState>
      PlacePayoutRule: Sourced<PlacePayoutRule>
      Win: SingleOddsEntry array
      Place: RangeOddsEntry array
      BracketQuinella: SingleOddsEntry array
      WinTotalUnitsHundredYen: Sourced<decimal option>
      PlaceTotalUnitsHundredYen: Sourced<decimal option>
      BracketTotalUnitsHundredYen: Sourced<decimal option>
      LimitFormat: OddsLimitFormat
      Raw: byte[] }

type SingleOdds =
    { Header: RecordHeader
      Kind: RecordKind
      Identity: RaceIdentity
      Announcement: Sourced<AnnouncementTime option>
      RegisteredCount: Sourced<int option>
      RunnerCount: Sourced<int option>
      Sale: Sourced<SaleState>
      Entries: SingleOddsEntry array
      TotalUnitsHundredYen: Sourced<decimal option>
      LimitFormat: OddsLimitFormat
      Raw: byte[] }

type WideOdds =
    { Header: RecordHeader
      Identity: RaceIdentity
      Announcement: Sourced<AnnouncementTime option>
      RegisteredCount: Sourced<int option>
      RunnerCount: Sourced<int option>
      Sale: Sourced<SaleState>
      Entries: RangeOddsEntry array
      TotalUnitsHundredYen: Sourced<decimal option>
      LimitFormat: OddsLimitFormat
      Raw: byte[] }

module internal OddsParser =
    let private announcement (reader: Reader) =
        let raw = reader.Ascii "Announcement" 28 8

        let value =
            if raw = "00000000" || raw = "        " then
                None
            else
                let part name position =
                    (reader.Int name position 2).Value
                    |> Option.defaultWith (fun () -> reader.Fail name position 2 "Missing announcement component.")

                let month = part "Announcement.Month" 28
                let day = part "Announcement.Day" 30
                let hour = part "Announcement.Hour" 32
                let minute = part "Announcement.Minute" 34

                if month < 1 || month > 12 then
                    reader.Fail "Announcement.Month" 28 2 "Month is outside 01–12."

                if day < 1 || day > DateTime.DaysInMonth(2000, month) then
                    reader.Fail "Announcement.Day" 30 2 "Invalid day for the supplied month."

                if hour > 23 then
                    reader.Fail "Announcement.Hour" 32 2 "Hour is outside 00–23."

                if minute > 59 then
                    reader.Fail "Announcement.Minute" 34 2 "Minute is outside 00–59."

                Some
                    { Month = month
                      Day = day
                      Hour = hour
                      Minute = minute }

        { Raw = raw; Value = value }

    let private odds (reader: Reader) format hasCurrentCap legacyCap name position length =
        let raw = reader.Ascii name position length

        let capped =
            match format, legacyCap with
            | OddsLimitFormat.Before20040814, Some cap -> Some cap
            | _ when hasCurrentCap -> Some(String('9', length))
            | _ -> None

        let value =
            if raw = String(' ', length) then
                OddsValue.NotRegistered
            elif raw = String('0', length) then
                OddsValue.NoVotes
            elif raw = String('-', length) then
                OddsValue.CancelledBeforeSale
            elif raw = String('*', length) then
                OddsValue.CancelledAfterSale
            else
                let number =
                    (reader.Number name position length 10M).Value
                    |> Option.defaultWith (fun () -> reader.Fail name position length "Missing odds value.")

                match format, legacyCap with
                | OddsLimitFormat.Before20040814, Some cap when
                    number > Decimal.Parse(cap, Globalization.CultureInfo.InvariantCulture) / 10M
                    ->
                    reader.Fail name position length "Odds exceed the selected historical format's limit."
                | _ -> ()

                if capped = Some raw then
                    OddsValue.AtLeast number
                else
                    OddsValue.Quoted number

        { Raw = raw; Value = value }

    let private single
        (reader: Reader)
        format
        hasCap
        legacyCap
        name
        position
        width
        count
        combinationWidth
        oddsWidth
        popularityWidth
        =
        Array.init count (fun i ->
            let start = position + i * width
            let field property = $"{name}[{i}].{property}"

            { Combination = reader.Identifier true (field "Combination") start combinationWidth
              Odds = odds reader format hasCap legacyCap (field "Odds") (start + combinationWidth) oddsWidth
              Popularity =
                BettingReader.popularity
                    reader
                    (field "Popularity")
                    (start + combinationWidth + oddsWidth)
                    popularityWidth })

    let private range
        (reader: Reader)
        format
        legacyCap
        name
        position
        width
        count
        combinationWidth
        oddsWidth
        popularityWidth
        =
        Array.init count (fun i ->
            let start = position + i * width
            let field property = $"{name}[{i}].{property}"

            { Combination = reader.Identifier true (field "Combination") start combinationWidth
              Minimum = odds reader format true legacyCap (field "Minimum") (start + combinationWidth) oddsWidth
              Maximum =
                odds reader format true legacyCap (field "Maximum") (start + combinationWidth + oddsWidth) oddsWidth
              Popularity =
                BettingReader.popularity
                    reader
                    (field "Popularity")
                    (start + combinationWidth + oddsWidth * 2)
                    popularityWidth })

    let private categories = [ "0"; "1"; "2"; "3"; "4"; "5"; "9" ]

    let parseO1 format (data: byte[]) =
        Reader.parse
            "O1"
            962
            categories
            (fun reader header ->
                { Header = header
                  Identity = reader.Identity 12
                  Announcement = announcement reader
                  RegisteredCount = reader.Int "RegisteredCount" 36 2
                  RunnerCount = reader.Int "RunnerCount" 38 2
                  WinSale = BettingReader.sale reader "WinSale" 40
                  PlaceSale = BettingReader.sale reader "PlaceSale" 41
                  BracketSale = BettingReader.sale reader "BracketSale" 42
                  PlacePayoutRule = BettingReader.placeRule reader "PlacePayoutRule" 43
                  Win = single reader format true None "Win" 44 8 28 2 4 2
                  Place = range reader format (Some "0999") "Place" 268 12 28 2 4 2
                  BracketQuinella = single reader format true (Some "09999") "BracketQuinella" 604 9 36 2 5 2
                  WinTotalUnitsHundredYen = reader.Number "WinTotalUnitsHundredYen" 928 11 1M
                  PlaceTotalUnitsHundredYen = reader.Number "PlaceTotalUnitsHundredYen" 939 11 1M
                  BracketTotalUnitsHundredYen = reader.Number "BracketTotalUnitsHundredYen" 950 11 1M
                  LimitFormat = format
                  Raw = Array.copy data })
            data

    let parseO3 format (data: byte[]) =
        Reader.parse
            "O3"
            2654
            categories
            (fun reader header ->
                { Header = header
                  Identity = reader.Identity 12
                  Announcement = announcement reader
                  RegisteredCount = reader.Int "RegisteredCount" 36 2
                  RunnerCount = reader.Int "RunnerCount" 38 2
                  Sale = BettingReader.sale reader "Sale" 40
                  Entries = range reader format None "Entries" 41 17 153 4 5 3
                  TotalUnitsHundredYen = reader.Number "TotalUnitsHundredYen" 2642 11 1M
                  LimitFormat = format
                  Raw = Array.copy data })
            data

    let private parseSingle
        id
        length
        kind
        count
        width
        combinationWidth
        oddsWidth
        popularityWidth
        format
        (data: byte[])
        =
        Reader.parse
            id
            length
            categories
            (fun reader header ->
                { Header = header
                  Kind = kind
                  Identity = reader.Identity 12
                  Announcement = announcement reader
                  RegisteredCount = reader.Int "RegisteredCount" 36 2
                  RunnerCount = reader.Int "RunnerCount" 38 2
                  Sale = BettingReader.sale reader "Sale" 40
                  Entries =
                    single
                        reader
                        format
                        (id <> "O6")
                        (if id = "O6" then None else Some "099999")
                        "Entries"
                        41
                        width
                        count
                        combinationWidth
                        oddsWidth
                        popularityWidth
                  TotalUnitsHundredYen = reader.Number "TotalUnitsHundredYen" (length - 12) 11 1M
                  LimitFormat = format
                  Raw = Array.copy data })
            data

    let parseO2 format data =
        parseSingle "O2" 2042 RecordKind.QuinellaOdds 153 13 4 6 3 format data

    let parseO4 format data =
        parseSingle "O4" 4031 RecordKind.ExactaOdds 306 13 4 6 3 format data

    let parseO5 format data =
        parseSingle "O5" 12293 RecordKind.TrioOdds 816 15 6 6 3 format data

    let parseO6 format data =
        parseSingle "O6" 83285 RecordKind.TrifectaOdds 4896 17 6 7 4 format data

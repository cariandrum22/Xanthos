namespace Xanthos.Data

open System
open Xanthos

exception internal ReadFailure of RecordParseError

/// Internal synchronous reader. Only the public boundary converts failures to Result.
type internal Reader(id: string, data: byte[], ?narrowedFields: (int * int * int) list) =
    let narrowed = defaultArg narrowedFields []

    let locate position length =
        let shift =
            narrowed
            |> List.sumBy (fun (p, current, previous) -> if p < position then current - previous else 0)

        let size =
            narrowed
            |> List.tryPick (fun (p, current, previous) ->
                if p = position && current = length then
                    Some previous
                else
                    None)
            |> Option.defaultValue length

        position - shift, size

    let get =
        function
        | Ok value -> value
        | Error error -> raise (ReadFailure error)

    member _.Fail name position length message =
        let position, length = locate position length
        let raw = RecordBytes.field id name position length data |> Result.defaultValue [||]

        raise (
            ReadFailure
                { RecordId = id
                  Field = name
                  Position = position
                  Length = length
                  Raw = raw
                  Message = message }
        )

    member _.Text name position length =
        let position, length = locate position length
        RecordBytes.text id name position length data |> get

    member _.Ascii name position length =
        let position, length = locate position length
        RecordBytes.ascii id name position length data |> get

    member this.Digits name position length =
        let raw = this.Ascii name position length

        if raw |> Seq.exists (fun c -> c < '0' || c > '9') then
            this.Fail name position length "Expected decimal identifier digits."

        raw

    member this.Identifier allowBlank name position length =
        let raw = this.Ascii name position length

        if allowBlank && raw |> Seq.forall ((=) ' ') then
            raw
        else
            this.Digits name position length

    member this.Number name position length scale =
        { Raw = this.Ascii name position length
          Value =
            let position, length = locate position length
            RecordBytes.number false scale id name position length data |> get }

    member this.Int name position length =
        let value = this.Number name position length 1M

        if value.Value |> Option.exists (fun n -> n > decimal Int32.MaxValue) then
            this.Fail name position length "Integer exceeds supported range."

        { Raw = value.Raw
          Value = value.Value |> Option.map int }

    member this.Code table name position =
        let raw = this.Ascii name position (Codes.width table)

        match Codes.parse table raw with
        | Ok code -> code
        | Error message -> this.Fail name position (Codes.width table) message

    member _.Date name position =
        let position, _ = locate position 8
        let raw, value = RecordBytes.date id name position data |> get
        { Raw = raw; Value = value }

    member this.Identity position =
        let date = this.Date "Identity.Date" position

        { Raw = this.Ascii "Identity" position 16
          Year = this.Int "Identity.Year" position 4
          MonthDay = this.Digits "Identity.MonthDay" (position + 4) 4
          Date = date.Value
          Racecourse = this.Code CodeTable.Racecourse "Identity.Racecourse" (position + 8)
          Meeting = this.Int "Identity.Meeting" (position + 10) 2
          Day = this.Int "Identity.Day" (position + 12) 2
          RaceNumber = this.Int "Identity.RaceNumber" (position + 14) 2 }

    member this.RaceName position =
        { Weekday = this.Code CodeTable.Weekday "Name.Weekday" position
          SpecialRaceNumber = this.Int "Name.SpecialRaceNumber" (position + 1) 4
          Title = this.Text "Name.Title" (position + 5) 60
          Subtitle = this.Text "Name.Subtitle" (position + 65) 60
          Parentheses = this.Text "Name.Parentheses" (position + 125) 60
          EnglishTitle = this.Text "Name.EnglishTitle" (position + 185) 120
          EnglishSubtitle = this.Text "Name.EnglishSubtitle" (position + 305) 120
          EnglishParentheses = this.Text "Name.EnglishParentheses" (position + 425) 120
          Abbreviation10 = this.Text "Name.Abbreviation10" (position + 545) 20
          Abbreviation6 = this.Text "Name.Abbreviation6" (position + 565) 12
          Abbreviation3 = this.Text "Name.Abbreviation3" (position + 577) 6
          Category = this.Ascii "Name.Category" (position + 583) 1
          Edition = this.Int "Name.Edition" (position + 584) 3 }

    member this.Conditions position =
        { RaceKind = this.Code CodeTable.RaceKind "Conditions.RaceKind" position
          RaceSymbol = this.Code CodeTable.RaceSymbol "Conditions.RaceSymbol" (position + 2)
          WeightRule = this.Code CodeTable.WeightRule "Conditions.WeightRule" (position + 5)
          AgeConditions =
            Array.init 5 (fun i ->
                this.Code CodeTable.RaceCondition ($"Conditions.AgeConditions[{i}]") (position + 6 + i * 3)) }

    member this.Announcement name position =
        let raw = this.Ascii name position 8

        let value =
            if raw = "00000000" || raw = "        " then
                None
            else
                let part suffix offset =
                    (this.Int (name + suffix) (position + offset) 2).Value
                    |> Option.defaultWith (fun () -> this.Fail name position 8 "Missing announcement component.")

                let month, day, hour, minute =
                    part ".Month" 0, part ".Day" 2, part ".Hour" 4, part ".Minute" 6

                if month < 1 || month > 12 then
                    this.Fail (name + ".Month") position 2 "Invalid month."

                if day < 1 || day > DateTime.DaysInMonth(2000, month) then
                    this.Fail (name + ".Day") (position + 2) 2 "Invalid day."

                if hour > 23 then
                    this.Fail (name + ".Hour") (position + 4) 2 "Invalid hour."

                if minute > 59 then
                    this.Fail (name + ".Minute") (position + 6) 2 "Invalid minute."

                Some
                    { Month = month
                      Day = day
                      Hour = hour
                      Minute = minute }

        { Raw = raw; Value = value }

    member this.ClockTime name position =
        let raw = this.Ascii name position 4

        let value =
            if raw = "0000" || raw = "    " then
                None
            else
                this.Digits name position 4 |> ignore
                let hour, minute = int raw[..1], int raw[2..]

                if hour > 23 || minute > 59 then
                    this.Fail name position 4 "Invalid hhmm time."

                Some(TimeOnly(hour, minute))

        { Raw = raw; Value = value }

module internal Reader =
    let parseWithWidths narrowed id currentLength categories operation (data: byte[]) =
        let length =
            currentLength
            - (narrowed |> List.sumBy (fun (_, current, previous) -> current - previous))

        let fail field position count message =
            Error
                { RecordId = id
                  Field = field
                  Position = position
                  Length = count
                  Raw = [||]
                  Message = message }

        if isNull data || data.Length <> length then
            fail "RecordLength" 1 length "Unexpected byte length for this record layout."
        else
            RecordBytes.header data
            |> Result.bind (fun header ->
                if header.RecordId <> id then
                    fail "RecordId" 1 2 "Record identifier does not match this parser."
                elif not (List.contains header.DataCategory categories) then
                    fail "DataCategory" 3 1 "Unknown category for this record type."
                else
                    RecordBytes.crlf id (length - 1) data
                    |> Result.bind (fun () ->
                        try
                            Ok(operation (Reader(id, data, narrowed)) header)
                        with
                        | ReadFailure error -> Error error
                        | ex -> fail "Record" 1 length ex.Message))

    let parse id length categories operation data =
        parseWithWidths [] id length categories operation data

namespace Xanthos

open System
open System.Globalization

/// Key formats come from the seven event declarations and request table on PDF pp.49-51.
module internal EventKeys =
    let originName =
        function
        | EventKind.Unknown origin -> origin
        | kind -> string kind

    let inferChangeKind (key: string) =
        if isNull key || key.Length < 2 then
            None
        else
            match key.Substring(0, 2) with
            | "JC" -> Some EventKind.JockeyChange
            | "WE" -> Some EventKind.Weather
            | "CC" -> Some EventKind.CourseChange
            | "AV" -> Some EventKind.Avoid
            | "TC" -> Some EventKind.TimeChange
            | _ -> None

    let parse (event: JvEvent) =
        let fail message =
            Error
                { Api = "parseEvent"
                  Code = None
                  Kind = JvErrorKind.InvalidInput
                  Outputs = Map.ofList [ "origin", originName event.Kind; "key", event.RawKey ]
                  Message = message }

        let format =
            match event.Kind with
            | EventKind.Pay -> Some("", "0B12")
            | EventKind.Weight -> Some("", "0B11")
            | EventKind.JockeyChange -> Some("JC", "0B16")
            | EventKind.Weather -> Some("WE", "0B16")
            | EventKind.CourseChange -> Some("CC", "0B16")
            | EventKind.Avoid -> Some("AV", "0B16")
            | EventKind.TimeChange -> Some("TC", "0B16")
            | EventKind.Unknown _ -> None

        match format with
        | None -> fail "Unknown notification origin; the original origin and key are retained."
        | Some(prefix, dataspec) ->
            let length = if prefix = "" then 12 else 28
            let key = event.RawKey

            if
                isNull key
                || key.Length <> length
                || not (key.StartsWith(prefix, StringComparison.Ordinal))
            then
                fail "The notification key does not match its origin."
            else
                let digits = key.Substring(prefix.Length)

                if digits |> Seq.exists (fun c -> c < '0' || c > '9') then
                    fail "Notification keys use ASCII digits."
                else
                    let parseDate (format: string) (text: string) =
                        DateTime.TryParseExact(text, format, CultureInfo.InvariantCulture, DateTimeStyles.None)

                    let dateOk, date = parseDate "yyyyMMdd" (digits.Substring(0, 8))

                    let timeOk, sentAt =
                        if prefix = "" then
                            true, None
                        else
                            let ok, value = parseDate "yyyyMMddHHmmss" (digits.Substring(12, 14))
                            ok, Some value

                    if not dateOk || not timeOk then
                        fail "The notification contains an invalid date or timestamp."
                    else
                        Ok
                            { Original = event
                              MeetingDate = date
                              CourseCode = digits.Substring(8, 2)
                              RaceNumber = digits.Substring(10, 2)
                              SentAt = sentAt
                              Request = { Dataspec = dataspec; Key = key } }

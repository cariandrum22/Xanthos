namespace Xanthos.Core

open System
open System.Globalization
open System.Text.Json

/// <summary>
/// Provides helpers for decoding and encoding JV-Link payloads into strongly typed domain records.
/// </summary>
module Serialization =

    open Errors

    type private ResultBuilder() =
        member _.Bind(m, f) = Result.bind f m
        member _.Return x = Ok x
        member _.ReturnFrom(m: Result<_, _>) = m

    let private result = ResultBuilder()

    let private specifyUtc (dt: DateTime) =
        if dt.Kind = DateTimeKind.Utc then
            dt
        else
            DateTime.SpecifyKind(dt, DateTimeKind.Utc)

    let private collectResults (items: Result<'a, XanthosError> list) =
        let rec loop acc remaining =
            match remaining with
            | [] -> Ok(List.rev acc)
            | Ok value :: tail -> loop (value :: acc) tail
            | Error err :: _ -> Error err

        loop [] items

    let private getStringProperty (name: string) (element: JsonElement) =
        let mutable property = Unchecked.defaultof<JsonElement>

        if element.TryGetProperty(name, &property) then
            if property.ValueKind = JsonValueKind.String then
                match property.GetString() with
                | null -> Error(validation $"Property '{name}' cannot be null.")
                | value when String.IsNullOrWhiteSpace value -> Error(validation $"Property '{name}' cannot be empty.")
                | value -> Ok value
            else
                Error(validation $"Property '{name}' must be a string.")
        else
            Error(validation $"Property '{name}' is required.")

    let private getOptionalStringProperty (name: string) (element: JsonElement) =
        let mutable property = Unchecked.defaultof<JsonElement>

        if element.TryGetProperty(name, &property) then
            match property.ValueKind with
            | JsonValueKind.Null -> Ok None
            | JsonValueKind.String ->
                match property.GetString() with
                | null -> Ok None
                | value when String.IsNullOrWhiteSpace value -> Ok None
                | value -> Ok(Some value)
            | _ -> Error(validation $"Property '{name}' must be a string or null.")
        else
            Ok None

    let private getOptionalIntProperty (name: string) (element: JsonElement) =
        let mutable property = Unchecked.defaultof<JsonElement>

        if element.TryGetProperty(name, &property) then
            match property.ValueKind with
            | JsonValueKind.Null -> Ok None
            | JsonValueKind.Number ->
                let mutable value = 0

                if property.TryGetInt32(&value) then
                    Ok(Some value)
                else
                    Error(validation $"Property '{name}' must be an integer.")
            | _ -> Error(validation $"Property '{name}' must be an integer.")
        else
            Ok None

    let private getOptionalDecimalProperty (name: string) (element: JsonElement) =
        let mutable property = Unchecked.defaultof<JsonElement>

        if element.TryGetProperty(name, &property) then
            match property.ValueKind with
            | JsonValueKind.Null -> Ok None
            | JsonValueKind.Number ->
                let mutable value = 0M

                if property.TryGetDecimal(&value) then
                    Ok(Some value)
                else
                    Error(validation $"Property '{name}' must be a decimal value.")
            | _ -> Error(validation $"Property '{name}' must be a decimal or null.")
        else
            Ok None

    let private getRequiredDateTimeProperty (name: string) (element: JsonElement) =
        let mutable property = Unchecked.defaultof<JsonElement>

        if element.TryGetProperty(name, &property) then
            if property.ValueKind = JsonValueKind.Null then
                Error(validation $"Property '{name}' cannot be null.")
            else
                try
                    property.GetDateTime() |> specifyUtc |> Ok
                with
                | :? InvalidOperationException
                | :? FormatException
                | :? ArgumentException -> Error(validation $"Property '{name}' must be an ISO 8601 date/time.")
        else
            Error(validation $"Property '{name}' is required.")

    let private getOptionalDateTimeProperty (name: string) (element: JsonElement) =
        let mutable property = Unchecked.defaultof<JsonElement>

        if element.TryGetProperty(name, &property) then
            match property.ValueKind with
            | JsonValueKind.Null -> Ok None
            | _ ->
                try
                    property.GetDateTime() |> specifyUtc |> Some |> Ok
                with
                | :? InvalidOperationException
                | :? FormatException
                | :? ArgumentException -> Error(validation $"Property '{name}' must be an ISO 8601 date/time.")
        else
            Ok None

    let private parseSurface (value: string) =
        let normalized = value |> Text.normalizeJvText

        match normalized.Trim().ToUpperInvariant() with
        | "TURF" -> TrackSurface.Turf
        | "DIRT" -> TrackSurface.Dirt
        | "SYNTHETIC" -> TrackSurface.Synthetic
        | _ -> TrackSurface.UnknownSurface

    let private parseCondition (value: string) =
        let normalized = value |> Text.normalizeJvText

        match normalized.Trim().ToUpperInvariant() with
        | "FAST" -> TrackCondition.Fast
        | "GOOD" -> TrackCondition.Good
        | "YIELDING" -> TrackCondition.Yielding
        | "SOFT" -> TrackCondition.Soft
        | "HEAVY" -> TrackCondition.Heavy
        | _ -> TrackCondition.UnknownCondition

    /// <summary>
    /// Parses a raw JV watch event key string into a typed WatchEvent.
    /// </summary>
    /// <param name="rawKey">The raw key string from JVWatchEvent callback.</param>
    /// <returns>A WatchEvent with parsed fields including event type, meeting date, course code, etc.</returns>
    /// <remarks>
    /// JVWatchEvent returns keys in formats like "0B12RA20240101010112..." where:
    /// <list type="bullet">
    /// <item>0B12/0B11/0B16 = dataspec prefix indicating event category</item>
    /// <item>RA/JC/WE/CC/AV/TC = record type (optional)</item>
    /// <item>yyyyMMdd = meeting date</item>
    /// <item>JJ = course/venue code</item>
    /// <item>RR = race number</item>
    /// </list>
    /// </remarks>
    let parseWatchEvent (rawKey: string) : WatchEvent =
        let normalized = Text.normalizeJvText rawKey

        let isEventCode prefix =
            normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)

        let payload =
            if isEventCode "0B" && normalized.Length > 4 then
                normalized.Substring(4)
            else
                normalized

        let recordTypeLength =
            payload |> Seq.takeWhile (fun c -> not (Char.IsDigit c)) |> Seq.length

        let recordType =
            if recordTypeLength > 0 then
                payload.Substring(0, recordTypeLength)
            else
                ""

        let digitPortion =
            payload.Substring(recordTypeLength)
            |> Seq.takeWhile Char.IsDigit
            |> Seq.toArray
            |> fun arr -> String(arr)

        let remaining =
            let consumed = recordTypeLength + digitPortion.Length

            if payload.Length > consumed then
                payload.Substring(consumed)
            else
                ""

        let meetingDate =
            if digitPortion.Length >= 8 then
                let dateSlice = digitPortion.Substring(0, 8)

                match
                    DateTime.TryParseExact(dateSlice, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None)
                with
                | true, dt -> Some dt
                | _ -> None
            else
                None

        let courseCode =
            if digitPortion.Length >= 10 then
                Some(digitPortion.Substring(8, 2))
            else
                None

        let raceNumber =
            if digitPortion.Length >= 12 then
                Some(digitPortion.Substring(10, 2))
            else
                None

        let participantId =
            if digitPortion.Length > 12 then
                let suffix = digitPortion.Substring(12)

                if String.IsNullOrWhiteSpace suffix then
                    None
                else
                    Some suffix
            else
                None

        let additionalData =
            [ recordType; remaining ]
            |> List.filter (String.IsNullOrWhiteSpace >> not)
            |> function
                | [] -> None
                | values -> Some(String.Join("", values))

        let recordTypeUpper = recordType.ToUpperInvariant()

        let eventType =
            if isEventCode "0B12" then
                WatchEventType.PayoffConfirmed
            elif isEventCode "0B11" then
                WatchEventType.HorseWeight
            elif isEventCode "0B16" then
                match recordTypeUpper with
                | "JC" -> WatchEventType.JockeyChange
                | "WE" -> WatchEventType.WeatherChange
                | "CC" -> WatchEventType.CourseChange
                | "AV" -> WatchEventType.AvoidedRace
                | "TC" -> WatchEventType.StartTimeChange
                | _ -> WatchEventType.UnknownEvent normalized
            else
                WatchEventType.UnknownEvent normalized

        { Event = eventType
          RawKey = normalized
          Timestamp = None
          MeetingDate = meetingDate
          CourseCode = courseCode
          RaceNumber = raceNumber
          RecordType =
            if String.IsNullOrWhiteSpace recordType then
                None
            else
                Some recordType
          ParticipantId = participantId
          AdditionalData = additionalData }

    let private parseRunnerOdds (element: JsonElement) =
        result {
            let! runnerIdText = getStringProperty "runnerId" element
            let runnerId = runnerIdText |> Text.normalizeJvText |> RunnerId.create

            let! runnerId = runnerId
            let! winOdds = getOptionalDecimalProperty "winOdds" element
            let! placeOdds = getOptionalDecimalProperty "placeOdds" element

            return
                { Runner = runnerId
                  WinOdds = winOdds
                  PlaceOdds = placeOdds }
        }

    let private ensureUniqueRunnerIds raceId entries =
        let duplicates =
            entries
            |> Seq.groupBy (fun e -> RunnerId.value e.Runner)
            |> Seq.choose (fun (runnerId, occurrences) -> if Seq.length occurrences > 1 then Some runnerId else None)
            |> Seq.toList

        match duplicates with
        | [] -> Ok entries
        | dupes ->
            let raceLabel = RaceId.value raceId
            let joined = String.Join(", ", dupes)
            Error(validation $"Duplicate runnerId(s) detected for race '{raceLabel}': {joined}")

    let private parseRaceOdds (element: JsonElement) =
        result {
            let! raceIdText = getStringProperty "raceId" element
            let! raceId = raceIdText |> Text.normalizeJvText |> RaceId.create
            let! timestamp = getRequiredDateTimeProperty "timestamp" element

            let mutable entriesElement = Unchecked.defaultof<JsonElement>

            if element.TryGetProperty("entries", &entriesElement) then
                if entriesElement.ValueKind = JsonValueKind.Array then
                    let parsedEntries =
                        entriesElement.EnumerateArray()
                        |> Seq.map parseRunnerOdds
                        |> Seq.toList
                        |> collectResults

                    let! entriesList = parsedEntries

                    let! uniqueEntries = ensureUniqueRunnerIds raceId entriesList

                    return
                        { Race = raceId
                          Timestamp = timestamp
                          Entries = uniqueEntries }
                else
                    return! Error(validation "Property 'entries' must be an array.")
            else
                return! Error(validation "Property 'entries' is required.")
        }

    let private parseRaceInfo (element: JsonElement) =
        result {
            let! raceIdText = getStringProperty "id" element
            let! raceId = raceIdText |> Text.normalizeJvText |> RaceId.create
            let! name = getStringProperty "name" element |> Result.map Text.normalizeJvText
            let! course = getOptionalStringProperty "course" element
            let! distance = getOptionalIntProperty "distanceMeters" element
            let! surfaceText = getStringProperty "surface" element
            let! conditionText = getStringProperty "condition" element
            let! scheduled = getOptionalDateTimeProperty "scheduledStart" element

            return
                { Id = raceId
                  Name = name
                  Course = course |> Option.map Text.normalizeJvText
                  DistanceMeters = distance
                  Surface = parseSurface surfaceText
                  Condition = parseCondition conditionText
                  ScheduledStart = scheduled }
        }

    let private parseArray
        (payload: byte[])
        (parser: JsonElement -> Result<'a, XanthosError>)
        (entityName: string)
        : Result<'a list, XanthosError> =
        if isNull payload || payload.Length = 0 then
            Ok []
        else
            try
                let json = Text.decodeShiftJis payload
                use document = JsonDocument.Parse(json)

                if document.RootElement.ValueKind <> JsonValueKind.Array then
                    Error(validation $"Expected JSON array for {entityName} payload.")
                else
                    document.RootElement.EnumerateArray()
                    |> Seq.map parser
                    |> Seq.toList
                    |> collectResults
            with :? JsonException as ex ->
                Error(validation $"Invalid JSON {entityName} payload: {ex.Message}")

    /// <summary>
    /// Decodes JV odds payloads (Shift-JIS or UTF-8) expressed as JSON into strongly typed snapshots.
    /// </summary>
    /// <param name="payload">Raw bytes returned by JV-Link.</param>
    /// <returns>A list of <see cref="RaceOdds"/> or a <see cref="XanthosError"/>.</returns>
    let parseOdds (payload: byte[]) : Result<RaceOdds list, XanthosError> = parseArray payload parseRaceOdds "odds"

    /// <summary>
    /// Decodes JV race card payloads (Shift-JIS or UTF-8) expressed as JSON into strongly typed records.
    /// </summary>
    /// <param name="payload">Raw bytes returned by JV-Link.</param>
    /// <returns>A list of <see cref="RaceInfo"/> or a <see cref="XanthosError"/>.</returns>
    let parseRaceCard (payload: byte[]) : Result<RaceInfo list, XanthosError> =
        parseArray payload parseRaceInfo "race-card"

    /// <summary>
    /// Serialises odds snapshots to JSON for persistence or caching scenarios.
    /// The output format is compatible with <see cref="parseOdds"/>.
    /// </summary>
    /// <param name="snapshots">The odds snapshots to serialise.</param>
    /// <returns>UTF-8 encoded JSON byte array or a <see cref="XanthosError"/>.</returns>
    let serialiseOdds (snapshots: RaceOdds list) : Result<byte[], XanthosError> =
        try
            use stream = new System.IO.MemoryStream()
            use writer = new System.Text.Json.Utf8JsonWriter(stream)

            writer.WriteStartArray()

            for snapshot in snapshots do
                writer.WriteStartObject()
                writer.WriteString("raceId", RaceId.value snapshot.Race)
                writer.WriteString("timestamp", snapshot.Timestamp.ToString("o"))

                writer.WritePropertyName("entries")
                writer.WriteStartArray()

                for entry in snapshot.Entries do
                    writer.WriteStartObject()
                    writer.WriteString("runnerId", RunnerId.value entry.Runner)

                    match entry.WinOdds with
                    | Some odds -> writer.WriteNumber("winOdds", odds)
                    | None -> writer.WriteNull("winOdds")

                    match entry.PlaceOdds with
                    | Some odds -> writer.WriteNumber("placeOdds", odds)
                    | None -> writer.WriteNull("placeOdds")

                    writer.WriteEndObject()

                writer.WriteEndArray()

                writer.WriteEndObject()

            writer.WriteEndArray()
            writer.Flush()

            stream.ToArray() |> Ok
        with ex ->
            Error(IOError($"Failed to serialise odds payload. {ex.Message}"))

    /// <summary>
    /// Deserialises odds snapshots from JSON. This is an alias for <see cref="parseOdds"/>.
    /// </summary>
    /// <param name="payload">UTF-8 or Shift-JIS encoded JSON byte array.</param>
    /// <returns>A list of <see cref="RaceOdds"/> or a <see cref="XanthosError"/>.</returns>
    let deserialiseOdds (payload: byte[]) : Result<RaceOdds list, XanthosError> = parseOdds payload

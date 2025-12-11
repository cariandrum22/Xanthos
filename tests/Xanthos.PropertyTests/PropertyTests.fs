module Xanthos.PropertyTests.PropertyTests

open System
open System.Text
open System.Text.Json
open System.Text.Json.Nodes
open Xunit
open FsCheck
open FsCheck.Xunit
open Xanthos.Core
open Xanthos.Core.Serialization
open Xanthos.Core.Text
open Xanthos.Interop
open Xanthos.PropertyTests.Generators
open System.Threading
open System.Threading.Tasks
open Xanthos.Runtime

let private jsonOptions =
    let opts = JsonSerializerOptions()
    opts.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase
    opts.WriteIndented <- false
    opts

let private defaultRequest =
    { Spec = "SPEC"
      FromTime = DateTime(2024, 1, 1)
      Option = 1 }

let private normalizeRaceSnapshots (snapshots: RaceOdds list) =
    let dedupe entries =
        entries
        |> List.fold
            (fun (acc, seen: Set<string>) entry ->
                let runnerKey = entry.Runner |> RunnerId.value |> Text.normalizeJvText

                if Set.contains runnerKey seen then
                    acc, seen
                else
                    entry :: acc, Set.add runnerKey seen)
            ([], Set.empty<string>)
        |> fst
        |> List.rev

    snapshots
    |> List.map (fun snapshot ->
        { snapshot with
            Entries = dedupe snapshot.Entries })

let private encodeRaceOdds (snapshots: RaceOdds list) =
    let snapshots = normalizeRaceSnapshots snapshots
    let array = JsonArray()

    for snapshot in snapshots do
        let obj = JsonObject()
        obj["raceId"] <- JsonValue.Create(RaceId.value snapshot.Race)
        obj["timestamp"] <- JsonValue.Create(snapshot.Timestamp)

        let entries = JsonArray()

        for entry in snapshot.Entries do
            let entryObj = JsonObject()
            entryObj["runnerId"] <- JsonValue.Create(RunnerId.value entry.Runner)

            entryObj["winOdds"] <-
                match entry.WinOdds with
                | Some odds -> JsonValue.Create(odds)
                | None -> null

            entryObj["placeOdds"] <-
                match entry.PlaceOdds with
                | Some odds -> JsonValue.Create(odds)
                | None -> null

            entries.Add(entryObj) |> ignore

        obj["entries"] <- entries
        array.Add(obj) |> ignore

    array.ToJsonString(jsonOptions) |> Text.encodeShiftJis

let private encodeRaceInfo (cards: RaceInfo list) =
    let array = JsonArray()

    for card in cards do
        let obj = JsonObject()
        obj["id"] <- JsonValue.Create(RaceId.value card.Id)
        obj["name"] <- JsonValue.Create(card.Name)

        obj["course"] <-
            match card.Course with
            | Some course -> JsonValue.Create(course)
            | None -> null

        obj["distanceMeters"] <-
            match card.DistanceMeters with
            | Some distance -> JsonValue.Create(distance)
            | None -> null

        obj["surface"] <- JsonValue.Create(card.Surface.ToString())
        obj["condition"] <- JsonValue.Create(card.Condition.ToString())

        obj["scheduledStart"] <-
            match card.ScheduledStart with
            | Some start -> JsonValue.Create(start)
            | None -> null

        array.Add(obj) |> ignore

    array.ToJsonString(jsonOptions) |> Text.encodeShiftJis

/// Helper to check if a string is a valid RaceId format
let private isValidRaceIdFormat (s: string) =
    if String.IsNullOrWhiteSpace s then
        false
    elif s.Length < 8 then
        false
    else
        let datePrefix = s.Substring(0, 8)

        let hasValidDate =
            if not (datePrefix |> Seq.forall Char.IsDigit) then
                false
            else
                let mutable parsed = DateTime.MinValue

                DateTime.TryParseExact(
                    datePrefix,
                    "yyyyMMdd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    &parsed
                )

        let hasValidSuffix =
            if s.Length <= 8 then
                true
            else
                s.Substring(8) |> Seq.forall Char.IsLetterOrDigit

        hasValidDate && hasValidSuffix

/// Helper to check if a string is a valid RunnerId format (exactly 10 digits)
let private isValidRunnerIdFormat (s: string) =
    if String.IsNullOrWhiteSpace s then
        false
    else
        let trimmed = s.Trim()
        trimmed.Length = 10 && trimmed |> Seq.forall Char.IsDigit

[<Property(Arbitrary = [| typeof<CustomArbitraries> |], QuietOnSuccess = true)>]
let ``RaceId value round-trips`` (NonEmptyString value) =
    let trimmed = value.Trim()
    let result = trimmed |> RaceId.create |> Result.map RaceId.value

    match result with
    | Ok actual -> actual = trimmed && isValidRaceIdFormat trimmed
    | Error _ -> not (isValidRaceIdFormat trimmed)

[<Property(Arbitrary = [| typeof<CustomArbitraries> |], QuietOnSuccess = true)>]
let ``RunnerId value round-trips`` (NonEmptyString value) =
    let trimmed = value.Trim()
    let result = trimmed |> RunnerId.create |> Result.map RunnerId.value

    match result with
    | Ok actual -> actual = trimmed && isValidRunnerIdFormat trimmed
    | Error _ -> not (isValidRunnerIdFormat trimmed)

[<Property(Arbitrary = [| typeof<CustomArbitraries> |], QuietOnSuccess = true)>]
let ``RunnerId arbitrary produces valid 10-digit values`` (runnerId: RunnerId) =
    let value = RunnerId.value runnerId

    value = value.Trim()
    && not (String.IsNullOrWhiteSpace value)
    && value.Length = 10
    && value |> Seq.forall Char.IsDigit

[<Property(Arbitrary = [| typeof<CustomArbitraries> |], QuietOnSuccess = true)>]
let ``RaceInfo generator produces canonical values`` (info: RaceInfo) =
    let nameTrimmed = info.Name.Trim()
    let raceIdValue = RaceId.value info.Id

    let courseValid =
        match info.Course with
        | Some course ->
            let trimmed = course.Trim()

            course = trimmed
            && trimmed.Length <= 32
            && not (String.IsNullOrWhiteSpace trimmed)
        | None -> true

    let distanceValid =
        match info.DistanceMeters with
        | Some meters -> meters >= 0
        | None -> true

    let scheduledValid =
        match info.ScheduledStart with
        | Some dt -> dt.Kind = DateTimeKind.Utc
        | None -> true

    info.Name = nameTrimmed
    && not (String.IsNullOrWhiteSpace nameTrimmed)
    && raceIdValue.Length <= 32
    && courseValid
    && distanceValid
    && scheduledValid

[<Property(Arbitrary = [| typeof<CustomArbitraries> |], QuietOnSuccess = true)>]
let ``RaceInfo generator renders consistent surface and condition`` (info: RaceInfo) =
    let surfaceName = info.Surface.ToString()
    let conditionName = info.Condition.ToString()

    not (String.IsNullOrWhiteSpace surfaceName)
    && not (String.IsNullOrWhiteSpace conditionName)

[<Property(Arbitrary = [| typeof<CustomArbitraries> |], QuietOnSuccess = true)>]
let ``RunnerOdds generator yields non-negative odds`` (odds: RunnerOdds) =
    let isValid = Option.forall (fun v -> v >= 0M)
    let runnerValue = RunnerId.value odds.Runner

    isValid odds.WinOdds
    && isValid odds.PlaceOdds
    && runnerValue = runnerValue.Trim()
    && not (String.IsNullOrWhiteSpace runnerValue)

[<Property(Arbitrary = [| typeof<CustomArbitraries> |], QuietOnSuccess = true)>]
let ``RaceOdds generator yields consistent snapshots`` (snapshot: RaceOdds) =
    let raceValue = RaceId.value snapshot.Race

    let entriesValid =
        snapshot.Entries
        |> List.forall (fun entry ->
            let value = RunnerId.value entry.Runner

            value = value.Trim()
            && not (String.IsNullOrWhiteSpace value)
            && Option.forall (fun v -> v >= 0M) entry.WinOdds
            && Option.forall (fun v -> v >= 0M) entry.PlaceOdds)

    let uniqueRunners =
        let ids = snapshot.Entries |> List.map (fun e -> RunnerId.value e.Runner)
        ids.Length = (ids |> List.distinct |> List.length)

    snapshot.Entries <> []
    && raceValue = raceValue.Trim()
    && entriesValid
    && uniqueRunners

// Additional strictness: Distance upper bound for generated RaceInfo values (if present)
[<Property(Arbitrary = [| typeof<CustomArbitraries> |], QuietOnSuccess = true)>]
let ``RaceInfo generator enforces reasonable distance upper bound`` (info: RaceInfo) =
    match info.DistanceMeters with
    | None -> true
    | Some d -> d >= 0 && d <= 10000

// Verify parseOdds output maintains unique runnerIds per snapshot
[<Property(Arbitrary = [| typeof<CustomArbitraries> |], QuietOnSuccess = true)>]
let ``parseOdds enforces unique runnerIds per snapshot`` (snapshots: RaceOdds list) =
    let normalized = normalizeRaceSnapshots snapshots
    let payload = encodeRaceOdds normalized

    match parseOdds payload with
    | Ok parsed ->
        parsed
        |> List.forall (fun snap ->
            let ids = snap.Entries |> List.map (fun e -> RunnerId.value e.Runner)
            ids.Length = (ids |> List.distinct |> List.length))
    | Error err -> failwithf "parseOdds failed unexpectedly: %A" err

// Verify parseRaceCard preserves distance upper bound
[<Property(Arbitrary = [| typeof<CustomArbitraries> |], QuietOnSuccess = true)>]
let ``parseRaceCard preserves distance invariants`` (cards: RaceInfo list) =
    let payload = encodeRaceInfo cards

    match parseRaceCard payload with
    | Ok parsed ->
        parsed
        |> List.forall (fun c -> c.DistanceMeters |> Option.forall (fun d -> d >= 0 && d <= 10000))
    | Error _ -> true

[<Property(Arbitrary = [| typeof<CustomArbitraries> |], QuietOnSuccess = true)>]
let ``serialiseOdds and deserialiseOdds round-trip`` (snapshots: RaceOdds list) =
    let normalized = normalizeRaceSnapshots snapshots

    match serialiseOdds normalized with
    | Ok bytes ->
        match deserialiseOdds bytes with
        | Ok parsed -> parsed = normalized
        | Error _ -> false
    | Error _ -> false

[<Property(QuietOnSuccess = true)>]
let ``parseOdds returns empty list for empty payload`` () = parseOdds Array.empty = Ok []

[<Property(Arbitrary = [| typeof<CustomArbitraries> |], QuietOnSuccess = true)>]
let ``parseOdds round-trips JSON payload`` (snapshots: RaceOdds list) =
    let normalized = normalizeRaceSnapshots snapshots
    let payload = encodeRaceOdds normalized

    match parseOdds payload with
    | Ok parsed ->
        Assert.Equal<RaceOdds list>(normalized, parsed)
        true
    | Error err ->
        let json = Encoding.UTF8.GetString(payload)
        failwithf "parseOdds failed: %A. JSON: %s" err json

[<Property(Arbitrary = [| typeof<CustomArbitraries> |], QuietOnSuccess = true)>]
let ``parseRaceCard round-trips JSON payload`` (cards: RaceInfo list) =
    let payload = encodeRaceInfo cards

    match parseRaceCard payload with
    | Ok parsed ->
        Assert.Equal<RaceInfo list>(cards, parsed)
        true
    | Error err ->
        let json = Encoding.UTF8.GetString(payload)
        failwithf "parseRaceCard failed: %A. JSON: %s" err json

[<Property(Arbitrary = [| typeof<CustomArbitraries> |], QuietOnSuccess = true)>]
let ``JvLinkStub streams payloads sequentially`` (payloads: byte[] list) =
    let client = JvLinkStub.FromPayloads payloads :> IJvLinkClient

    match client.Init "sid", client.Open defaultRequest with
    | Ok(), Ok _ ->
        let rec collect acc =
            match client.Read() with
            | Ok(Payload payload) -> collect ((payload.Data |> Array.toList) :: acc)
            | Ok FileBoundary -> collect acc
            | Ok DownloadPending -> collect acc
            | Ok EndOfStream -> List.rev acc
            | Error err -> failwithf "Unexpected stub error %A" err

        let actual = collect []
        let expected = payloads |> List.map Array.toList
        actual = expected
    | _ -> false

// Property: async realtime stream eventually yields payload after DownloadPending streak
[<Property(MaxTest = 50, QuietOnSuccess = true)>]
let ``StreamRealtimeAsync backoff yields eventual payload`` (NonNegativeInt n) =
    let streak = min n 8

    let outcomes =
        [ for _ in 1..streak -> Ok DownloadPending ]
        @ [ Ok(
                JvReadOutcome.Payload
                    { Timestamp = None
                      Data = [| 0x11uy |] }
            )
            Ok EndOfStream ]

    let stub = new JvLinkStub(outcomes) :> IJvLinkClient

    let config =
        match JvLinkConfig.create "SID" None None None with
        | Ok c -> c
        | Error e -> failwithf "%A" e

    let service = new JvLinkService(stub, config)

    let enumerator =
        service
            .StreamRealtimeAsync("REALTIME", "20240101", pollInterval = TimeSpan.FromMilliseconds 5.)
            .GetAsyncEnumerator(CancellationToken.None)

    let collected = ResizeArray<byte[]>()

    let rec loop () =
        task {
            let! hasNext = enumerator.MoveNextAsync().AsTask()

            if hasNext then
                match enumerator.Current with
                | Ok payload when payload.Data.Length > 0 ->
                    collected.Add payload.Data
                    return ()
                | _ -> return! loop ()
            else
                return ()
        }

    loop().Wait()
    enumerator.DisposeAsync().AsTask().Wait()
    collected.Count = 1 && collected.[0] = [| 0x11uy |]

// Property: async realtime stream respects cancellation during prolonged DownloadPending without throwing
[<Property(MaxTest = 30, QuietOnSuccess = true)>]
let ``StreamRealtimeAsync cancels gracefully during prolonged DownloadPending`` (PositiveInt n) =
    let streak = min n 50
    let outcomes = [ for _ in 1..streak -> Ok DownloadPending ] @ [ Ok EndOfStream ]
    let stub = new JvLinkStub(outcomes) :> IJvLinkClient

    let config =
        match JvLinkConfig.create "SID" None None None with
        | Ok c -> c
        | Error e -> failwithf "%A" e

    let service = new JvLinkService(stub, config)
    use cts = new CancellationTokenSource()
    cts.CancelAfter(TimeSpan.FromMilliseconds 25.)

    let enum =
        service
            .StreamRealtimeAsync(
                "REALTIME",
                "20240101",
                pollInterval = TimeSpan.FromMilliseconds 2.,
                cancellationToken = cts.Token
            )
            .GetAsyncEnumerator(cts.Token)

    let mutable observedErrors = 0
    let mutable iterations = 0

    let rec loop () =
        task {
            let! hasNext = enum.MoveNextAsync().AsTask()

            if hasNext then
                iterations <- iterations + 1

                match enum.Current with
                | Error _ -> observedErrors <- observedErrors + 1
                | _ -> ()

                if iterations < streak then return! loop () else return ()
            else
                return ()
        }

    let disposeEnumerator () =
        try
            enum.DisposeAsync().AsTask().Wait()
        with _ ->
            ()

    try
        loop().Wait()
        disposeEnumerator ()
        observedErrors = 0
    with
    | :? AggregateException as agg when agg.InnerExceptions |> Seq.forall (fun ex -> ex :? TaskCanceledException) ->
        disposeEnumerator ()
        true
    | :? AggregateException as agg ->
        disposeEnumerator ()

        failwithf
            "Unexpected exceptions during cancellation: %s"
            (String.Join(";", agg.InnerExceptions |> Seq.map (fun e -> e.GetType().Name)))

[<Property(Arbitrary = [| typeof<CustomArbitraries> |], QuietOnSuccess = true)>]
let ``RaceInfo generator enforces name constraints`` (info: RaceInfo) =
    let name = info.Name
    let invalidChars = [ '/'; '\\'; ':'; '*'; '?'; '"'; '<'; '>'; '|' ]

    not (String.IsNullOrWhiteSpace name)
    && name = name.Trim()
    && name.Length <= 64
    && invalidChars |> List.forall (fun ch -> not (name.Contains(ch)))

[<Property(Arbitrary = [| typeof<CustomArbitraries> |], QuietOnSuccess = true)>]
let ``Surface/Condition ToString are canonical and non-empty`` (info: RaceInfo) =
    let s = info.Surface.ToString()
    let c = info.Condition.ToString()

    not (String.IsNullOrWhiteSpace s)
    && s = s.Trim()
    && s.Length <= 32
    && not (String.IsNullOrWhiteSpace c)
    && c = c.Trim()
    && c.Length <= 32

[<Property(Arbitrary = [| typeof<CustomArbitraries> |], QuietOnSuccess = true)>]
let ``RaceId/RunnerId enforce length and format constraints`` (raceId: RaceId, runnerId: RunnerId) =
    let rv = RaceId.value raceId
    let uv = RunnerId.value runnerId

    // RaceId: 8-16 chars (date prefix + optional suffix)
    rv.Length >= 8
    && rv.Length <= 16
    && rv |> Seq.forall Char.IsLetterOrDigit
    // RunnerId: exactly 10 digits
    && uv.Length = 10
    && uv |> Seq.forall Char.IsDigit

[<Property(Arbitrary = [| typeof<CustomArbitraries> |], QuietOnSuccess = true)>]
let ``RaceOdds timestamps are UTC`` (snapshot: RaceOdds) =
    snapshot.Timestamp.Kind = DateTimeKind.Utc

[<Property(Arbitrary = [| typeof<CustomArbitraries> |], QuietOnSuccess = true)>]
let ``Surface/Condition round-trip ToString casing`` (info: RaceInfo) =
    let sText = info.Surface.ToString()
    let cText = info.Condition.ToString()

    sText = sText.Trim()
    && cText = cText.Trim()
    && not (String.IsNullOrWhiteSpace sText)
    && not (String.IsNullOrWhiteSpace cText)

[<Property(Arbitrary = [| typeof<CustomArbitraries> |], QuietOnSuccess = true)>]
let ``RunnerOdds do not have both odds None`` (odds: RunnerOdds) =
    match odds.WinOdds, odds.PlaceOdds with
    | None, None -> false
    | _ -> true

[<Property(Arbitrary = [| typeof<CustomArbitraries> |], QuietOnSuccess = true)>]
let ``RaceInfo distance respects inclusive bounds when present`` (info: RaceInfo) =
    info.DistanceMeters |> Option.forall (fun d -> d >= 0 && d <= 10000)

[<Property(Arbitrary = [| typeof<CustomArbitraries> |], QuietOnSuccess = true)>]
let ``parseRaceCard preserves name and id invariants`` (cards: RaceInfo list) =
    let payload = encodeRaceInfo cards

    match parseRaceCard payload with
    | Ok parsed ->
        (cards, parsed)
        ||> List.forall2 (fun original actual ->
            let nameOk = not (String.IsNullOrWhiteSpace actual.Name)
            let idOk = RaceId.value actual.Id = (RaceId.value original.Id).Trim()
            nameOk && idOk)
    | Error _ -> true

[<Property(Arbitrary = [| typeof<CustomArbitraries> |], QuietOnSuccess = true)>]
let ``DistanceMeters includes boundary values when present`` (info: RaceInfo) =
    match info.DistanceMeters with
    | None -> true
    | Some d -> d = 0 || d = 10000 || (d > 0 && d < 10000)

[<Property(Arbitrary = [| typeof<CustomArbitraries> |], MaxTest = 50, QuietOnSuccess = true)>]
let ``parseOdds reports details on failure (shrink-friendly)`` (snapshots: RaceOdds list) =
    let normalized = normalizeRaceSnapshots snapshots
    let payload = encodeRaceOdds normalized

    match parseOdds payload with
    | Ok parsed -> parsed.Length >= 0 // no-op, success
    | Error err ->
        let json = Encoding.UTF8.GetString(payload)
        // include bounded slice to aid shrinking/debug
        let head = if json.Length > 128 then json.Substring(0, 128) else json

        let tail =
            if json.Length > 256 then
                let suffix = json.Substring(json.Length - 128)
                $"...(tail)...{suffix}"
            else
                ""

        failwithf "parseOdds failed: %A. JSON(head): %s%s" err head tail

[<Property(Arbitrary = [| typeof<CustomArbitraries> |], MaxTest = 50, QuietOnSuccess = true)>]
let ``parseRaceCard reports details on failure (shrink-friendly)`` (cards: RaceInfo list) =
    let payload = encodeRaceInfo cards

    match parseRaceCard payload with
    | Ok parsed -> parsed.Length >= 0 // success
    | Error err ->
        let json = Encoding.UTF8.GetString(payload)
        let preview = if json.Length > 128 then json.Substring(0, 128) else json
        failwithf "parseRaceCard failed: %A. JSON(head): %s" err preview

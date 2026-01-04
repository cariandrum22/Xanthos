module Xanthos.Cli.Execution

open System
open System.IO
open System.Threading
open System.Threading.Tasks
open Xanthos.Core
open Xanthos.Core.Errors
open Xanthos.Core.Text
open Xanthos.Interop
open Xanthos.Runtime
open Xanthos.Runtime.ServiceExtensions
open Xanthos.Runtime.Validation
open Xanthos.Cli.Types

type ExecutionContext =
    { Globals: GlobalSettings
      Config: JvLinkConfig
      Activation: ClientActivation
      Logger: TraceLogger }

let private modeString activation =
    match activation.Mode with
    | Com -> "COM"
    | Stub _ -> "STUB"

let private describeError (err: XanthosError) = Errors.toString err

/// Sample payloads for stub mode E2E testing.
/// These payloads contain the text "Stub payload" which E2E tests check for.
let private sampleStubPayloads =
    [| Text.encodeShiftJis "Stub payload 1 - sample data for testing"
       Text.encodeShiftJis "Stub payload 2 - additional test data"
       Text.encodeShiftJis "Stub payload 3 - more data for coverage" |]

/// Creates a stub client with sample payloads for E2E testing.
let private createStubClient () =
    JvLinkStub.FromPayloads sampleStubPayloads :> IJvLinkClient

/// Creates a new client instance based on activation mode.
/// Each call returns a fresh client to ensure proper ownership semantics.
/// Returns Error if COM client creation fails when in COM mode (no silent fallback).
let private tryCreateClient (ctx: ExecutionContext) : Result<IJvLinkClient, string> =
    match ctx.Activation.Mode with
    | Com ->
        match ComClientFactory.tryCreate ctx.Config.UseJvGets with
        | Ok client -> Ok client
        | Error fault ->
            // Do NOT silently fallback - evidence must match behavior
            let details =
                match fault.Details with
                | null
                | "" -> "unknown reason"
                | d -> d

            Error $"COM client creation failed: {details}"
    | Stub _ -> Ok(createStubClient ())

/// Creates a JvLinkService with a new client.
/// The returned service owns the client - caller MUST use 'use' to ensure disposal.
/// Returns Error if client creation fails (no silent fallback).
let private tryCreateService (ctx: ExecutionContext) : Result<JvLinkService, string> =
    match tryCreateClient ctx with
    | Ok client -> Ok(new JvLinkService(client, ctx.Config, ctx.Logger))
    | Error msg -> Error msg

let private printEvidence ctx (service: JvLinkService) =
    let modeText = modeString ctx.Activation

    let version =
        match service.GetJVLinkVersion() with
        | Ok v -> v
        | Error _ -> "unknown"

    printfn "EVIDENCE:MODE=%s" modeText
    printfn "EVIDENCE:VERSION=%s" version
    version

let createExecutionContext globals =
    result {
        // Pass UseJvGets from global settings; when None, env var controls it
        let! config = JvLinkConfig.create globals.Sid globals.SavePath globals.ServiceKey globals.UseJvGets

        // Determine activation mode without creating a client.
        // Clients are created per-service to ensure proper ownership semantics.
        let activation =
            match globals.StubPreference with
            | StubPreference.ForcedByUser ->
                { Mode = Stub "forced by user"
                  FallbackCom = false }
            | StubPreference.ForcedByPlatform ->
                { Mode = Stub "platform limitation"
                  FallbackCom = false }
            | StubPreference.PreferCom ->
                if OperatingSystem.IsWindows() then
                    // Probe COM availability to determine mode
                    match ComClientFactory.tryCreate None with
                    | Ok probeClient ->
                        // Dispose the probe client immediately - actual clients are created per-service
                        match box probeClient with
                        | :? IDisposable as d -> d.Dispose()
                        | _ -> ()

                        { Mode = Com; FallbackCom = false }
                    | Error fault ->
                        // COM activation failed, fall back to stub - include error details for diagnostics
                        let details =
                            match fault.Details with
                            | null
                            | "" -> "unknown reason"
                            | d -> d

                        { Mode = Stub $"COM activation failed: {details}"
                          FallbackCom = true }
                else
                    { Mode = Stub "non-Windows fallback"
                      FallbackCom = false }

        let logger = TraceLogger.ofConsole ()

        return
            { Globals = globals
              Config = config
              Activation = activation
              Logger = logger }
    }

let describeMode activation =
    match activation.Mode with
    | Com -> "COM"
    | Stub reason -> $"Stub ({reason})"

let configureDiagnostics enabled (logger: TraceLogger) =
    if enabled then
        Diagnostics.register (fun m -> logger.Debug($"[COM] {m}"))
    else
        Diagnostics.clear ()

let reportError label err =
    printfn "%s: %s" label (describeError err)
    2

/// Executes a function with a service, handling client creation failures.
/// Returns exit code 2 if client creation fails.
let private withService ctx (f: JvLinkService -> int) : int =
    match tryCreateService ctx with
    | Ok service ->
        use service = service
        f service
    | Error msg ->
        printfn "Client creation failed: %s" msg
        2

let runVersion ctx =
    withService ctx (fun service ->
        if ctx.Globals.EnableDiagnostics && ctx.Activation.Mode = Com then
            printfn "CALL JVInit"

        let version = printEvidence ctx service
        printfn "JV-Link version: %s" version
        0)

let runDownload ctx args =
    withService ctx (fun service ->
        let _ = printEvidence ctx service

        match service.FetchPayloads(args.Request) with
        | Ok payloads ->
            payloads
            |> List.iteri (fun idx payload ->
                let preview =
                    if payload.Data.Length > 50 then
                        Text.decodeShiftJis payload.Data.[0..49] + "..."
                    else
                        Text.decodeShiftJis payload.Data

                printfn "Payload %d: %d bytes - %s" (idx + 1) payload.Data.Length preview)

            match args.OutputDirectory with
            | Some dir ->
                Directory.CreateDirectory dir |> ignore

                payloads
                |> List.iteri (fun idx payload ->
                    let filename = Path.Combine(dir, $"payload_{idx + 1:D3}.bin")
                    File.WriteAllBytes(filename, payload.Data))

                printfn "Persisted %d file(s) to %s" payloads.Length dir
            | None -> ()

            printfn "Download completed (spec=%s option=%d)." args.Request.Spec args.Request.Option
            0
        | Error err -> reportError "Download failed" err)

let runRealtime ctx args =
    withService ctx (fun service ->
        let _ = printEvidence ctx service

        if args.Continuous then
            printfn "Starting continuous realtime streaming for %s (key=%s)..." args.Spec args.Key
            printfn "Press Ctrl+C to stop streaming."
        else
            printfn "Starting realtime streaming for %s (key=%s)..." args.Spec args.Key

        let mutable count = 0
        let mutable lastError: XanthosError option = None
        let mutable cancelled = false

        if args.Continuous then
            // Continuous mode: use StreamRealtimeAsync which polls until cancelled
            use cts = new CancellationTokenSource()

            // Create handler as a named delegate so we can remove it later
            let cancelHandler =
                ConsoleCancelEventHandler(fun _ eventArgs ->
                    printfn ""
                    printfn "Cancellation requested..."
                    eventArgs.Cancel <- true // Prevent immediate termination
                    cancelled <- true
                    // Only cancel if CTS hasn't been disposed
                    try
                        cts.Cancel()
                    with :? ObjectDisposedException ->
                        ())

            // Register the handler
            Console.CancelKeyPress.AddHandler(cancelHandler)

            try
                let stream =
                    service.StreamRealtimeAsync(args.Spec, args.Key, cancellationToken = cts.Token)

                // Consume the async enumerable synchronously for CLI
                task {
                    let enumerator = stream.GetAsyncEnumerator(cts.Token)

                    try
                        let mutable hasNext = true

                        while hasNext && not cancelled do
                            let! moveResult = enumerator.MoveNextAsync()
                            hasNext <- moveResult

                            if hasNext then
                                match enumerator.Current with
                                | Ok payload ->
                                    count <- count + 1
                                    printfn "Realtime payload %d: %d bytes" count payload.Data.Length
                                | Error err ->
                                    lastError <- Some err
                                    printfn "Stream error: %s" (describeError err)
                    finally
                        enumerator.DisposeAsync().AsTask().Wait()
                }
                |> fun t ->
                    try
                        t.Wait()
                    with :? AggregateException as ex ->
                        // Check if cancellation was the cause
                        match ex.InnerExceptions |> Seq.tryFind (fun e -> e :? OperationCanceledException) with
                        | Some _ ->
                            cancelled <- true
                            printfn "Streaming cancelled by user."
                        | None -> reraise ()
            finally
                // Always remove the handler to prevent accumulation and ObjectDisposedException
                Console.CancelKeyPress.RemoveHandler(cancelHandler)
        else
            // Default mode: use StreamRealtimePayloads which exits on EndOfStream
            for result in service.StreamRealtimePayloads(args.Spec, args.Key) do
                match result with
                | Ok payload ->
                    count <- count + 1
                    printfn "Realtime payload %d: %d bytes" count payload.Data.Length
                | Error err ->
                    lastError <- Some err
                    printfn "Stream error: %s" (describeError err)

        // Only show completion message if not cancelled
        if cancelled then
            0
        else
            match lastError with
            | Some err ->
                printfn "Realtime stream ended with error for %s (key=%s)." args.Spec args.Key
                reportError "Realtime streaming failed" err
            | None ->
                printfn "Realtime stream completed for %s (key=%s). Total payloads: %d" args.Spec args.Key count
                0)

let runSetSaveFlag ctx value =
    withService ctx (fun service ->
        let _ = printEvidence ctx service

        match service.SetSaveDownloadsEnabled value with
        | Ok() ->
            printfn "Save flag set to %b." value
            0
        | Error err -> reportError "Failed to set save flag" err)

let runGetSaveFlag ctx =
    withService ctx (fun service ->
        match service.GetSaveDownloadsEnabled() with
        | Ok value ->
            printfn "Save flag: %b" value
            0
        | Error err -> reportError "Failed to get save flag" err)

let runSetSavePath ctx path =
    withService ctx (fun service ->
        let _ = printEvidence ctx service

        match service.SetSavePath path with
        | Ok() ->
            printfn "Save path updated to %s" path
            0
        | Error err -> reportError "Failed to set save path" err)

let runGetSavePath ctx =
    withService ctx (fun service ->
        match service.GetSavePath() with
        | Ok path ->
            printfn "Save path: %s" path
            0
        | Error err -> reportError "Failed to get save path" err)

let runSetServiceKey ctx key =
    withService ctx (fun service ->
        let _ = printEvidence ctx service

        match service.SetServiceKey key with
        | Ok() ->
            printfn "Service key updated."
            0
        | Error err -> reportError "Failed to set service key" err)

let runGetServiceKey ctx =
    withService ctx (fun service ->
        match service.GetServiceKey() with
        | Ok key ->
            printfn "Service key: %s" key
            0
        | Error err -> reportError "Failed to get service key" err)

let runSetPayoffDialog ctx value =
    withService ctx (fun service ->
        let _ = printEvidence ctx service

        match service.SetPayoffDialogSuppressed value with
        | Ok() ->
            printfn "Payoff dialog suppression set to %b." value
            0
        | Error err -> reportError "Failed to set payoff dialog" err)

let runGetPayoffDialog ctx =
    withService ctx (fun service ->
        match service.GetPayoffDialogSuppressed() with
        | Ok value ->
            printfn "Payoff dialog suppressed: %b" value
            0
        | Error err -> reportError "Failed to get payoff dialog" err)

let runSetParentHwnd ctx hwnd =
    withService ctx (fun service ->
        let _ = printEvidence ctx service

        match service.SetParentWindowHandle hwnd with
        | Ok() ->
            printfn "Parent HWND set to %A." hwnd
            0
        | Error err -> reportError "Failed to set parent HWND" err)

let runGetParentHwnd ctx =
    withService ctx (fun service ->
        match service.GetParentWindowHandle() with
        | Ok hwnd ->
            printfn "Parent HWND: %A" hwnd
            0
        | Error err -> reportError "Failed to get parent HWND" err)

let runCourseFile ctx key =
    withService ctx (fun service ->
        let _ = printEvidence ctx service

        match service.GetCourseDiagram key with
        | Ok diagram ->
            let explanation = diagram.Explanation |> Option.defaultValue "(no explanation)"

            printfn "Course file [%s]: Path=%s Explanation=%s" key diagram.FilePath explanation
            0
        | Error err -> reportError "Failed to get course file" err)

let runCourseFile2 ctx args =
    withService ctx (fun service ->
        let _ = printEvidence ctx service

        match service.GetCourseDiagramBasic(args.Key, args.OutputPath) with
        | Ok diagram ->
            printfn "Course file (v2) [%s]: Path=%s" args.Key diagram.FilePath
            0
        | Error err -> reportError "Failed to get course file (v2)" err)

let runSilksFile ctx args =
    withService ctx (fun service ->
        let _ = printEvidence ctx service

        match service.GenerateSilksFile(args.Pattern, args.OutputPath) with
        | Ok silks ->
            let path = silks.FilePath |> Option.defaultValue args.OutputPath
            printfn "Silks image written to %s" path
            0
        | Error err -> reportError "Failed to generate silks file" err)

let runSilksBinary ctx pattern =
    withService ctx (fun service ->
        let _ = printEvidence ctx service

        match service.GetSilksBinary pattern with
        | Ok silks ->
            let size = silks.Data |> Option.map (fun d -> d.Length) |> Option.defaultValue 0
            printfn "Generated %d bytes of silks data for %s" size pattern
            0
        | Error err -> reportError "Failed to get silks binary" err)

let runMovieCheck ctx key =
    withService ctx (fun service ->
        let _ = printEvidence ctx service

        match service.CheckMovieAvailability key with
        | Ok availability ->
            printfn "Movie availability for %s: %A" key availability
            0
        | Error err -> reportError "Failed to check movie availability" err)

let runMovieCheckWithType ctx args =
    withService ctx (fun service ->
        let _ = printEvidence ctx service

        let movieType = MovieType.fromCode args.MovieTypeCode

        match service.CheckMovieAvailability(movieType, args.MovieKey) with
        | Ok availability ->
            printfn "Movie availability for type=%s key=%s: %A" args.MovieTypeCode args.MovieKey availability
            0
        | Error err -> reportError "Failed to check movie availability with type" err)

let runMoviePlay ctx key =
    withService ctx (fun service ->
        let _ = printEvidence ctx service

        match service.PlayMovie key with
        | Ok() ->
            printfn "JVMVPlay succeeded for key=%s" key
            0
        | Error err -> reportError "Failed to play movie" err)

let runMoviePlayWithType ctx args =
    withService ctx (fun service ->
        let _ = printEvidence ctx service

        let movieType = MovieType.fromCode args.MovieTypeCode

        match service.PlayMovie(movieType, args.MovieKey) with
        | Ok() ->
            printfn "JVMVPlayWithType succeeded for type=%s key=%s" args.MovieTypeCode args.MovieKey
            0
        | Error err -> reportError "Failed to play movie with type" err)

let runMovieOpen ctx args =
    withService ctx (fun service ->
        let _ = printEvidence ctx service

        let movieType = MovieType.fromCode args.MovieOpenType

        match service.FetchWorkoutVideos(movieType, args.MovieSearchKey) with
        | Ok listings ->
            printfn "JVMVOpen succeeded for type=%s search=%s." args.MovieOpenType args.MovieSearchKey
            printfn "Found %d workout video listing(s)." listings.Length

            for listing in listings do
                printfn "  Workout: %s" listing.RawKey

            0
        | Error err -> reportError "Failed to open movie" err)

let runStatus ctx =
    withService ctx (fun service ->
        let _ = printEvidence ctx service

        match service.GetStatus() with
        | Ok count ->
            printfn "Status: Completed %d file(s)." count
            0
        | Error err -> reportError "Failed to get status" err)

let runSkip ctx =
    withService ctx (fun service ->
        let _ = printEvidence ctx service

        match service.SkipCurrentFile() with
        | Ok() ->
            printfn "JVSkip succeeded."
            0
        | Error err -> reportError "Failed to skip" err)

let runCancel ctx =
    withService ctx (fun service ->
        let _ = printEvidence ctx service

        match service.CancelDownload() with
        | Ok() ->
            printfn "JVCancel succeeded."
            0
        | Error err -> reportError "Failed to cancel" err)

let runDeleteFile ctx name =
    withService ctx (fun service ->
        let _ = printEvidence ctx service

        match service.DeleteFile name with
        | Ok() ->
            printfn "JVFiledelete succeeded for %s." name
            0
        | Error err -> reportError "Failed to delete file" err)

let runTotalReadSize ctx =
    withService ctx (fun service ->
        let _ = printEvidence ctx service

        match service.GetTotalReadFileSize() with
        | Ok size ->
            printfn "Total read file size: %d bytes" size
            0
        | Error err -> reportError "Failed to get total read file size" err)

let runCurrentReadSize ctx =
    withService ctx (fun service ->
        let _ = printEvidence ctx service

        match service.GetCurrentReadFileSize() with
        | Ok size ->
            printfn "Current file size: %d bytes" size
            0
        | Error err -> reportError "Failed to get current read file size" err)

let runCurrentFileTimestamp ctx =
    withService ctx (fun service ->
        let _ = printEvidence ctx service

        match service.GetCurrentFileTimestamp() with
        | Ok timestamp ->
            match timestamp with
            | Some ts -> printfn "Current file timestamp: %s" (ts.ToString("o"))
            | None -> printfn "Current file timestamp: (none)"

            0
        | Error err -> reportError "Failed to get current file timestamp" err)

/// Creates a separate service instance for realtime fetching.
/// This is necessary because JvLinkService sessions cannot be shared -
/// calling StreamRealtimePayloads on the watch service would close the watch session.
let private fetchRealtimeWithSeparateSession (ctx: ExecutionContext) (req: WatchEventRealtimeRequest) =
    // Create a separate client for realtime fetching - use tryCreateClient for consistency
    match tryCreateClient ctx with
    | Ok client ->
        // JvLinkService takes ownership of the client - disposing the service disposes the client
        use realtimeService = new JvLinkService(client, ctx.Config)
        printfn "Opening realtime session for %s (key=%s)..." req.Dataspec req.Key

        let payloads = ResizeArray<_>()
        let mutable errorOccurred = false

        for result in realtimeService.StreamRealtimePayloads(req.Dataspec, req.Key) do
            match result with
            | Ok payload -> payloads.Add(payload)
            | Error err ->
                printfn "Realtime fetch error: %s" (describeError err)
                errorOccurred <- true

        if not errorOccurred then
            printfn "Received %d realtime payload(s)." payloads.Count

            for p in payloads do
                printfn "  Payload: %d bytes" p.Data.Length
    // Service and client are automatically disposed when leaving scope
    | Error msg -> printfn "Skipping realtime fetch: %s" msg

let runWatchEvents ctx args =
    withService ctx (fun watchService ->
        let _ = printEvidence ctx watchService

        match watchService.StartWatchEvents() with
        | Ok() ->
            let durationText =
                match args.Duration with
                | Some d -> $"{d.TotalSeconds} seconds"
                | None -> "indefinite"

            printfn "Watch events started successfully (%s)." durationText

            if args.OpenAfterRealtime then
                printfn "Will open realtime session on event trigger (using separate service)."
            // Subscribe to events and print them
            use subscription =
                watchService.WatchEvents.Subscribe(fun result ->
                    match result with
                    | Ok ev ->
                        printfn "Event received: %A" ev.Event
                        // If --open-after is specified, open a realtime session for this event
                        // IMPORTANT: Use a separate service instance to avoid session interference
                        if args.OpenAfterRealtime then
                            match WatchEvent.toRealtimeRequest ev with
                            | Some req -> fetchRealtimeWithSeparateSession ctx req
                            | None -> printfn "No realtime dataspec for event type %A" ev.Event
                    | Error err -> printfn "Event error: %s" (describeError err))
            // Wait for specified duration or until cancelled
            match args.Duration with
            | Some d ->
                Thread.Sleep(int d.TotalMilliseconds)
                printfn "Watch duration elapsed."
            | None ->
                printfn "Press Ctrl+C to stop watching..."
                Thread.Sleep(Timeout.Infinite)

            watchService.StopWatchEvents() |> ignore
            printfn "Watch events stopped."
            0
        | Error err ->
            printfn "Failed to start watch events: %s" (describeError err)
            2)

let runSetUiProperties ctx =
    withService ctx (fun service ->
        let _ = printEvidence ctx service

        match service.ShowConfigurationDialog() with
        | Ok() ->
            printfn "JVSetUIProperties succeeded."
            0
        | Error err -> reportError "Failed to show configuration dialog" err)

/// All known record types for coverage tracking
let private knownRecordTypes =
    [
      // Race data
      "TK"
      "RA"
      "SE"
      "HR"
      // Odds data
      "O1"
      "O2"
      "O3"
      "O4"
      "O5"
      "O6"
      // Vote count data
      "H1"
      "H5"
      "H6"
      // Master data
      "UM"
      "KS"
      "CH"
      "BR"
      "BN"
      "HN"
      "SK"
      "RC"
      // Analysis data
      "CK"
      "HC"
      "HS"
      "HY"
      "YS"
      "BT"
      "CS"
      "DM"
      "TM"
      "WF"
      "WC"
      // Real-time data
      "WH"
      "WE"
      "AV"
      "JC"
      "TC"
      "CC"
      "JG" ]

let runCaptureFixtures ctx args =
    // Fixture capture requires real COM - refuse if using stub
    match ctx.Activation.Mode with
    | Stub reason ->
        printfn "ERROR: capture-fixtures requires real COM connection."
        printfn "Cannot capture fixtures: %s" reason
        printfn "Run this command on Windows with JV-Link installed."
        2
    | Com ->
        // Force JVGets when explicitly requested, regardless of env var defaults.
        let ctxForFixtures =
            if args.UseJvGets then
                printfn "Using JVGets mode (--use-jvgets)"

                { ctx with
                    Config =
                        { ctx.Config with
                            UseJvGets = Some true } }
            else
                ctx

        match tryCreateService ctxForFixtures with
        | Error msg ->
            printfn "ERROR: COM client creation failed: %s" msg
            2
        | Ok service ->

            use service = service
            let _ = printEvidence ctx service
            printfn "Starting fixture capture..."
            printfn "Output directory: %s" args.FixturesOutputDir
            printfn "Specs: %s" (String.concat ", " args.Specs)
            printfn "From time: %s" (args.FromTime.ToString("yyyy-MM-dd HH:mm:ss"))

            match args.ToTime with
            | Some toTime -> printfn "To time: %s" (toTime.ToString("yyyy-MM-dd HH:mm:ss"))
            | None -> printfn "To time: (no limit)"

            printfn "Max records per type: %d" args.MaxRecordsPerType

            // Create base output directory
            Directory.CreateDirectory(args.FixturesOutputDir) |> ignore
            let mutable totalCaptured = 0
            let mutable totalErrors = 0
            let mutable parseErrors = 0
            let capturedTypes = ResizeArray<string>()

            // Filter function for ToTime
            let filterByToTime (payloads: JvPayload list) =
                match args.ToTime with
                | None -> payloads
                | Some toTime ->
                    payloads
                    |> List.filter (fun p ->
                        match p.Timestamp with
                        | Some ts -> ts <= toTime
                        | None -> true // Include payloads without timestamp
                    )

            for spec in args.Specs do
                printfn ""
                printfn "Processing spec: %s" spec
                let specDir = Path.Combine(args.FixturesOutputDir, spec.Replace("/", "_"))

                let request = Validation.createOpenRequest spec args.FromTime 1

                match service.FetchPayloads(request) with
                | Ok payloads ->
                    let filteredPayloads = filterByToTime payloads
                    printfn "  Fetched %d payload(s) (after filter: %d)" payloads.Length filteredPayloads.Length

                    // Group by record type
                    let grouped =
                        filteredPayloads
                        |> List.groupBy (fun p -> PayloadParser.getRecordTypeId p.Data)
                        |> List.filter (fun (rt, _) -> not (String.IsNullOrEmpty rt))

                    // Only create spec directory if there are records to write
                    if not (List.isEmpty grouped) then
                        Directory.CreateDirectory(specDir) |> ignore

                        for (recordType, records) in grouped do
                            let typeDir = Path.Combine(specDir, recordType)
                            Directory.CreateDirectory(typeDir) |> ignore

                            let toCapture = records |> List.truncate args.MaxRecordsPerType
                            let mutable typeParseErrors = 0

                            // Validate that captured records can be parsed
                            for payload in toCapture do
                                match PayloadParser.parsePayload payload with
                                | Ok _ -> ()
                                | Error _ ->
                                    typeParseErrors <- typeParseErrors + 1
                                    parseErrors <- parseErrors + 1

                            let statusIcon = if typeParseErrors = 0 then "✓" else "⚠"

                            printfn
                                "    %s %s: %d record(s) (capturing %d, parse errors: %d)"
                                statusIcon
                                recordType
                                records.Length
                                toCapture.Length
                                typeParseErrors

                            // Track captured types
                            if not (capturedTypes.Contains recordType) then
                                capturedTypes.Add recordType

                            toCapture
                            |> List.iteri (fun idx payload ->
                                let filename = Path.Combine(typeDir, $"{idx + 1:D4}.bin")
                                File.WriteAllBytes(filename, payload.Data)

                                // Validate and include parse status in metadata
                                let parseStatus =
                                    match PayloadParser.parsePayload payload with
                                    | Ok _ -> "ok"
                                    | Error err -> $"error: {describeError err}"

                                // Write metadata JSON alongside
                                let metaFilename = Path.Combine(typeDir, $"{idx + 1:D4}.meta.json")

                                let timestampJson =
                                    match payload.Timestamp with
                                    | Some t -> sprintf "\"%s\"" (t.ToString("o"))
                                    | None -> "null"

                                let meta =
                                    $"{{\"timestamp\": {timestampJson}, \"byteLength\": {payload.Data.Length}, \"recordType\": \"{recordType}\", \"parseStatus\": \"{parseStatus}\"}}"

                                File.WriteAllText(metaFilename, meta)
                                totalCaptured <- totalCaptured + 1)
                    else
                        printfn "  No records to capture for this spec."

                | Error err ->
                    printfn "  ERROR: %s" (describeError err)
                    totalErrors <- totalErrors + 1

            // Print comprehensive summary
            printfn ""
            printfn "=========================================="
            printfn "  CAPTURE SUMMARY"
            printfn "=========================================="
            printfn ""
            printfn "Records captured: %d" totalCaptured
            printfn "Parse errors: %d" parseErrors
            printfn "Spec fetch errors: %d" totalErrors
            printfn ""

            // Coverage analysis
            let capturedSet = capturedTypes |> Set.ofSeq
            let knownSet = knownRecordTypes |> Set.ofList
            let covered = Set.intersect capturedSet knownSet |> Set.count
            let missing = Set.difference knownSet capturedSet
            let extra = Set.difference capturedSet knownSet

            printfn "COVERAGE ANALYSIS"
            printfn "-----------------"

            printfn
                "Record types captured: %d/%d (%.1f%%)"
                covered
                knownRecordTypes.Length
                (float covered / float knownRecordTypes.Length * 100.0)

            printfn ""

            if not (Set.isEmpty missing) then
                let missingStr = missing |> Set.toList |> String.concat ", "
                printfn "Missing record types (%d): %s" missing.Count missingStr
                printfn ""

            if not (Set.isEmpty extra) then
                let extraStr = extra |> Set.toList |> String.concat ", "
                printfn "Unknown record types captured (%d): %s" extra.Count extraStr
                printfn ""

            // Group captured by category for better overview
            let categoryMap =
                [ "Race Data", [ "TK"; "RA"; "SE"; "HR" ]
                  "Odds Data", [ "O1"; "O2"; "O3"; "O4"; "O5"; "O6" ]
                  "Vote Count", [ "H1"; "H5"; "H6" ]
                  "Master Data", [ "UM"; "KS"; "CH"; "BR"; "BN"; "HN"; "SK"; "RC" ]
                  "Analysis Data", [ "CK"; "HC"; "HS"; "HY"; "YS"; "BT"; "CS"; "DM"; "TM"; "WF"; "WC" ]
                  "Real-time Data", [ "WH"; "WE"; "AV"; "JC"; "TC"; "CC"; "JG" ] ]

            printfn "COVERAGE BY CATEGORY"
            printfn "--------------------"

            for (category, types) in categoryMap do
                let capturedInCategory = types |> List.filter (fun t -> capturedSet.Contains t)
                let pct = float capturedInCategory.Length / float types.Length * 100.0

                let status =
                    if capturedInCategory.Length = types.Length then
                        "✓"
                    else
                        " "

                printfn "%s %-15s: %d/%d (%.0f%%)" status category capturedInCategory.Length types.Length pct

            printfn ""

            if parseErrors > 0 then
                printfn "WARNING: %d record(s) failed to parse. Check metadata files for details." parseErrors
                1
            elif totalErrors > 0 then
                1
            else
                0

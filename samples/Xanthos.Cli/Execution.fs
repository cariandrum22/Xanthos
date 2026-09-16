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
      Logger: TraceLogger
      WriteLine: string -> unit }

let private write (ctx: ExecutionContext) format = Printf.kprintf ctx.WriteLine format

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

    write ctx "EVIDENCE:MODE=%s" modeText
    write ctx "EVIDENCE:VERSION=%s" version
    version

let createExecutionContextWithWriter writer globals =
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
            | StubPreference.PreferCom -> { Mode = Com; FallbackCom = false }

        let logger =
            { Info = fun message -> writer $"[INFO ] {message}"
              Warn = fun message -> writer $"[WARN ] {message}"
              Error = fun message -> writer $"[ERROR] {message}"
              Debug = fun message -> writer $"[DEBUG] {message}" }

        return
            { Globals = globals
              Config = config
              Activation = activation
              Logger = logger
              WriteLine = writer }
    }

let createExecutionContext globals =
    createExecutionContextWithWriter Console.WriteLine globals

let describeMode activation =
    match activation.Mode with
    | Com -> "COM"
    | Stub reason -> $"Stub ({reason})"

let configureDiagnostics enabled (logger: TraceLogger) =
    if enabled then
        Diagnostics.register (fun m -> logger.Debug($"[COM] {m}"))
    else
        Diagnostics.clear ()

let reportErrorWithWriter writer label err =
    Printf.kprintf writer "%s: %s" label (describeError err)
    2

let reportError label err =
    reportErrorWithWriter Console.WriteLine label err

/// Executes a function with a service, handling client creation failures.
/// Returns exit code 2 if client creation fails.
let private withService ctx (f: JvLinkService -> int) : int =
    match tryCreateService ctx with
    | Ok service ->
        use service = service

        match service.Initialize() with
        | Ok() ->
            printEvidence ctx service |> ignore
            f service
        | Error err -> reportErrorWithWriter ctx.WriteLine "Initialization failed" err
    | Error msg ->
        write ctx "Client creation failed: %s" msg
        2

let runVersion ctx =
    withService ctx (fun service ->
        if ctx.Globals.EnableDiagnostics && ctx.Activation.Mode = Com then
            write ctx "CALL JVInit"

        let version = printEvidence ctx service
        write ctx "JV-Link version: %s" version
        0)

let runDownload ctx args =
    withService ctx (fun service ->
        printEvidence ctx service |> ignore

        // When --max-records is set, use StreamPayloads (lazy seq) so that JV-Link
        // stops reading after N records instead of fetching all 1000+ files first.
        let fetchResult =
            match args.MaxRecords with
            | Some n ->
                let mutable firstError: XanthosError option = None

                let payloads =
                    service.StreamPayloads(args.Request)
                    |> Seq.choose (fun r ->
                        match r with
                        | Ok p -> Some p
                        | Error err ->
                            if firstError.IsNone then
                                firstError <- Some err

                            None)
                    |> Seq.truncate n
                    |> Seq.toList

                match firstError with
                | Some err when payloads.IsEmpty -> Error err
                | _ -> Ok payloads
            | None -> service.FetchPayloads(args.Request)

        match fetchResult with
        | Ok payloads ->
            payloads
            |> List.iteri (fun idx payload ->
                let fullText = Text.decodeShiftJis payload.Data

                let preview =
                    if fullText.Length > 40 then
                        fullText.[..39] + "..."
                    else
                        fullText

                write ctx "Payload %d: %d bytes - %s" (idx + 1) payload.Data.Length preview)

            match args.OutputDirectory with
            | Some dir ->
                Directory.CreateDirectory dir |> ignore

                payloads
                |> List.iteri (fun idx payload ->
                    let filename = Path.Combine(dir, $"payload_{idx + 1:D3}.bin")
                    File.WriteAllBytes(filename, payload.Data))

                write ctx "Persisted %d file(s) to %s" payloads.Length dir
            | None -> ()

            write ctx "Download completed (spec=%s option=%d)." args.Request.Spec args.Request.Option
            0
        | Error err -> reportErrorWithWriter ctx.WriteLine "Download failed" err)

let runRealtime ctx args =
    withService ctx (fun service ->
        printEvidence ctx service |> ignore

        if args.Continuous then
            write ctx "Starting continuous realtime streaming for %s (key=%s)..." args.Spec args.Key
            write ctx "Press Ctrl+C to stop streaming."
        else
            write ctx "Starting realtime streaming for %s (key=%s)..." args.Spec args.Key

        let mutable count = 0
        let mutable lastError: XanthosError option = None
        let mutable cancelled = false

        if args.Continuous then
            // Continuous mode: use StreamRealtimeAsync which polls until cancelled
            use cts = new CancellationTokenSource()

            // Create handler as a named delegate so we can remove it later
            let cancelHandler =
                ConsoleCancelEventHandler(fun _ eventArgs ->
                    write ctx ""
                    write ctx "Cancellation requested..."
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
                                    write ctx "Realtime payload %d: %d bytes" count payload.Data.Length
                                | Error err ->
                                    lastError <- Some err
                                    write ctx "Stream error: %s" (describeError err)
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
                            write ctx "Streaming cancelled by user."
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
                    write ctx "Realtime payload %d: %d bytes" count payload.Data.Length
                | Error err ->
                    lastError <- Some err
                    write ctx "Stream error: %s" (describeError err)

        // Only show completion message if not cancelled
        if cancelled then
            0
        else
            match lastError with
            | Some err ->
                write ctx "Realtime stream ended with error for %s (key=%s)." args.Spec args.Key
                reportErrorWithWriter ctx.WriteLine "Realtime streaming failed" err
            | None ->
                write ctx "Realtime stream completed for %s (key=%s). Total payloads: %d" args.Spec args.Key count
                0)

let runSetSaveFlag ctx value =
    withService ctx (fun service ->
        printEvidence ctx service |> ignore

        match service.SetSaveDownloadsEnabled value with
        | Ok() ->
            write ctx "Save flag set to %b." value
            0
        | Error err -> reportErrorWithWriter ctx.WriteLine "Failed to set save flag" err)

let runGetSaveFlag ctx =
    withService ctx (fun service ->
        match service.GetSaveDownloadsEnabled() with
        | Ok value ->
            write ctx "Save flag: %b" value
            0
        | Error err -> reportErrorWithWriter ctx.WriteLine "Failed to get save flag" err)

let runSetSavePath ctx path =
    withService ctx (fun service ->
        printEvidence ctx service |> ignore

        match service.SetSavePath path with
        | Ok() ->
            write ctx "Save path updated to %s" path
            0
        | Error err -> reportErrorWithWriter ctx.WriteLine "Failed to set save path" err)

let runGetSavePath ctx =
    withService ctx (fun service ->
        match service.GetSavePath() with
        | Ok path ->
            write ctx "Save path: %s" path
            0
        | Error err -> reportErrorWithWriter ctx.WriteLine "Failed to get save path" err)

let runSetServiceKey ctx key =
    withService ctx (fun service ->
        printEvidence ctx service |> ignore

        match service.SetServiceKey key with
        | Ok() ->
            write ctx "Service key updated."
            0
        | Error err -> reportErrorWithWriter ctx.WriteLine "Failed to set service key" err)

let runGetServiceKey ctx =
    withService ctx (fun service ->
        match service.GetServiceKey() with
        | Ok key ->
            write
                ctx
                "Service key: %s"
                (if String.IsNullOrWhiteSpace key then
                     "not configured"
                 else
                     "registered")

            0
        | Error err -> reportErrorWithWriter ctx.WriteLine "Failed to get service key" err)

let runSetPayoffDialog ctx value =
    withService ctx (fun service ->
        printEvidence ctx service |> ignore

        match service.SetPayoffDialogSuppressed value with
        | Ok() ->
            write ctx "Payoff dialog suppression set to %b." value
            0
        | Error err -> reportErrorWithWriter ctx.WriteLine "Failed to set payoff dialog" err)

let runGetPayoffDialog ctx =
    withService ctx (fun service ->
        match service.GetPayoffDialogSuppressed() with
        | Ok value ->
            write ctx "Payoff dialog suppressed: %b" value
            0
        | Error err -> reportErrorWithWriter ctx.WriteLine "Failed to get payoff dialog" err)

let runSetParentHwnd ctx hwnd =
    withService ctx (fun service ->
        printEvidence ctx service |> ignore

        match service.SetParentWindowHandle hwnd with
        | Ok() ->
            write ctx "Parent HWND set to %A." hwnd
            0
        | Error err -> reportErrorWithWriter ctx.WriteLine "Failed to set parent HWND" err)

let runGetParentHwnd ctx =
    withService ctx (fun service ->
        match service.GetParentWindowHandle() with
        | Ok hwnd ->
            write ctx "Parent HWND: %A" hwnd
            0
        | Error err -> reportErrorWithWriter ctx.WriteLine "Failed to get parent HWND" err)

let runCourseFile ctx key =
    withService ctx (fun service ->
        printEvidence ctx service |> ignore

        match service.GetCourseDiagram key with
        | Ok diagram ->
            match diagram.Explanation with
            | Some explanation -> write ctx "Course file [%s]: Path=%s Explanation=%s" key diagram.FilePath explanation
            | None -> write ctx "Course file [%s]: Path=%s" key diagram.FilePath

            0
        | Error err -> reportErrorWithWriter ctx.WriteLine "Failed to get course file" err)

let runCourseFile2 ctx (args: CourseFile2Args) =
    withService ctx (fun service ->
        printEvidence ctx service |> ignore

        match service.GetCourseDiagramBasic(args.Key, args.OutputPath) with
        | Ok diagram ->
            write ctx "Course file (v2) [%s]: Path=%s" args.Key diagram.FilePath
            0
        | Error err -> reportErrorWithWriter ctx.WriteLine "Failed to get course file (v2)" err)

let runSilksFile ctx args =
    withService ctx (fun service ->
        printEvidence ctx service |> ignore

        match service.GenerateSilksFile(args.Pattern, args.OutputPath) with
        | Ok silks ->
            let path = silks.FilePath |> Option.defaultValue args.OutputPath
            write ctx "Silks image written to %s" path
            0
        | Error err -> reportErrorWithWriter ctx.WriteLine "Failed to generate silks file" err)

let runSilksBinary ctx pattern =
    withService ctx (fun service ->
        printEvidence ctx service |> ignore

        match service.GetSilksBinary pattern with
        | Ok silks ->
            let size = silks.Data |> Option.map (fun d -> d.Length) |> Option.defaultValue 0
            write ctx "Generated %d bytes of silks data for %s" size pattern
            0
        | Error err -> reportErrorWithWriter ctx.WriteLine "Failed to get silks binary" err)

let runMovieCheck ctx key =
    withService ctx (fun service ->
        printEvidence ctx service |> ignore

        match service.CheckMovieAvailability key with
        | Ok availability ->
            write ctx "Movie availability for %s: %A" key availability
            0
        | Error err -> reportErrorWithWriter ctx.WriteLine "Failed to check movie availability" err)

let runMovieCheckWithType ctx args =
    withService ctx (fun service ->
        printEvidence ctx service |> ignore

        let movieType = MovieType.fromCode args.MovieTypeCode

        match service.CheckMovieAvailability(movieType, args.MovieKey) with
        | Ok availability ->
            write ctx "Movie availability for type=%s key=%s: %A" args.MovieTypeCode args.MovieKey availability
            0
        | Error err -> reportErrorWithWriter ctx.WriteLine "Failed to check movie availability with type" err)

let runMoviePlay ctx key =
    withService ctx (fun service ->
        printEvidence ctx service |> ignore

        match service.PlayMovie key with
        | Ok() ->
            write ctx "JVMVPlay succeeded for key=%s" key
            0
        | Error err -> reportErrorWithWriter ctx.WriteLine "Failed to play movie" err)

let runMoviePlayWithType ctx args =
    withService ctx (fun service ->
        printEvidence ctx service |> ignore

        let movieType = MovieType.fromCode args.MovieTypeCode

        match service.PlayMovie(movieType, args.MovieKey) with
        | Ok() ->
            write ctx "JVMVPlayWithType succeeded for type=%s key=%s" args.MovieTypeCode args.MovieKey
            0
        | Error err -> reportErrorWithWriter ctx.WriteLine "Failed to play movie with type" err)

let runMovieOpen ctx args =
    withService ctx (fun service ->
        printEvidence ctx service |> ignore

        let movieType = MovieType.fromCode args.MovieOpenType

        match service.FetchWorkoutVideos(movieType, args.MovieSearchKey) with
        | Ok listings ->
            write ctx "JVMVOpen succeeded for type=%s search=%s." args.MovieOpenType args.MovieSearchKey
            write ctx "Found %d workout video listing(s)." listings.Length

            for listing in listings do
                write ctx "  Workout: %s" listing.RawKey

            0
        | Error err -> reportErrorWithWriter ctx.WriteLine "Failed to open movie" err)

let runStatus ctx =
    withService ctx (fun service ->
        printEvidence ctx service |> ignore

        match service.GetStatus() with
        | Ok count ->
            write ctx "Status: Completed %d file(s)." count
            0
        | Error err -> reportErrorWithWriter ctx.WriteLine "Failed to get status" err)

let runSkip ctx =
    withService ctx (fun service ->
        printEvidence ctx service |> ignore

        match service.SkipCurrentFile() with
        | Ok() ->
            write ctx "JVSkip succeeded."
            0
        | Error err -> reportErrorWithWriter ctx.WriteLine "Failed to skip" err)

let runCancel ctx =
    withService ctx (fun service ->
        printEvidence ctx service |> ignore

        match service.CancelDownload() with
        | Ok() ->
            write ctx "JVCancel succeeded."
            0
        | Error err -> reportErrorWithWriter ctx.WriteLine "Failed to cancel" err)

let runDeleteFile ctx name =
    withService ctx (fun service ->
        printEvidence ctx service |> ignore

        match service.DeleteFile name with
        | Ok() ->
            write ctx "JVFiledelete succeeded for %s." name
            0
        | Error err -> reportErrorWithWriter ctx.WriteLine "Failed to delete file" err)

let runTotalReadSize ctx =
    withService ctx (fun service ->
        printEvidence ctx service |> ignore

        match service.GetTotalReadFileSizeBytes() with
        | Ok size ->
            write ctx "Total read file size: %d bytes" size
            0
        | Error err -> reportErrorWithWriter ctx.WriteLine "Failed to get total read file size" err)

let runCurrentReadSize ctx =
    withService ctx (fun service ->
        printEvidence ctx service |> ignore

        match service.GetCurrentReadFileSize() with
        | Ok size ->
            write ctx "Current file size: %d bytes" size
            0
        | Error err -> reportErrorWithWriter ctx.WriteLine "Failed to get current read file size" err)

let runCurrentFileTimestamp ctx =
    withService ctx (fun service ->
        printEvidence ctx service |> ignore

        match service.GetCurrentFileTimestamp() with
        | Ok timestamp ->
            match timestamp with
            | Some ts -> write ctx "Current file timestamp: %s" (ts.ToString("o"))
            | None -> write ctx "Current file timestamp: (none)"

            0
        | Error err -> reportErrorWithWriter ctx.WriteLine "Failed to get current file timestamp" err)

/// Creates a separate service instance for realtime fetching.
/// This is necessary because JvLinkService sessions cannot be shared -
/// calling StreamRealtimePayloads on the watch service would close the watch session.
let private fetchRealtimeWithSeparateSession (ctx: ExecutionContext) (req: WatchEventRealtimeRequest) =
    // Create a separate client for realtime fetching - use tryCreateClient for consistency
    match tryCreateClient ctx with
    | Ok client ->
        // JvLinkService takes ownership of the client - disposing the service disposes the client
        use realtimeService = new JvLinkService(client, ctx.Config)
        write ctx "Opening realtime session for %s (key=%s)..." req.Dataspec req.Key

        let payloads = ResizeArray<_>()
        let mutable errorOccurred = false

        for result in realtimeService.StreamRealtimePayloads(req.Dataspec, req.Key) do
            match result with
            | Ok payload -> payloads.Add(payload)
            | Error err ->
                write ctx "Realtime fetch error: %s" (describeError err)
                errorOccurred <- true

        if not errorOccurred then
            write ctx "Received %d realtime payload(s)." payloads.Count

            for p in payloads do
                write ctx "  Payload: %d bytes" p.Data.Length
    // Service and client are automatically disposed when leaving scope
    | Error msg -> write ctx "Skipping realtime fetch: %s" msg

let runWatchEvents ctx args =
    withService ctx (fun watchService ->
        printEvidence ctx watchService |> ignore

        match watchService.StartWatchEvents() with
        | Ok() ->
            let durationText =
                match args.Duration with
                | Some d -> $"{d.TotalSeconds} seconds"
                | None -> "indefinite"

            write ctx "Watch events started successfully (%s)." durationText

            if args.OpenAfterRealtime then
                write ctx "Will open realtime session on event trigger (using separate service)."
            // Subscribe to events and print them
            use subscription =
                watchService.WatchEvents.Subscribe(fun result ->
                    match result with
                    | Ok ev ->
                        write ctx "Event received: %A" ev.Event
                        // If --open-after is specified, open a realtime session for this event
                        // IMPORTANT: Use a separate service instance to avoid session interference
                        if args.OpenAfterRealtime then
                            match WatchEvent.toRealtimeRequest ev with
                            | Some req -> fetchRealtimeWithSeparateSession ctx req
                            | None -> write ctx "No realtime dataspec for event type %A" ev.Event
                    | Error err -> write ctx "Event error: %s" (describeError err))
            // Wait for specified duration or until cancelled
            match args.Duration with
            | Some d ->
                Thread.Sleep(int d.TotalMilliseconds)
                write ctx "Watch duration elapsed."
            | None ->
                write ctx "Press Ctrl+C to stop watching..."
                Thread.Sleep(Timeout.Infinite)

            watchService.StopWatchEvents() |> ignore
            write ctx "Watch events stopped."
            0
        | Error err ->
            write ctx "Failed to start watch events: %s" (describeError err)
            2)

let runSetUiProperties ctx =
    withService ctx (fun service ->
        printEvidence ctx service |> ignore

        match service.ShowConfigurationDialog() with
        | Ok() ->
            write ctx "JVSetUIProperties succeeded."
            0
        | Error err -> reportErrorWithWriter ctx.WriteLine "Failed to show configuration dialog" err)

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
        write ctx "ERROR: capture-fixtures requires real COM connection."
        write ctx "Cannot capture fixtures: %s" reason
        write ctx "Run this command on Windows with JV-Link installed."
        2
    | Com ->
        // Force JVGets when explicitly requested, regardless of env var defaults.
        let ctxForFixtures =
            if args.UseJvGets then
                write ctx "Using JVGets mode (--use-jvgets)"

                { ctx with
                    Config =
                        { ctx.Config with
                            UseJvGets = Some true } }
            else
                ctx

        match tryCreateService ctxForFixtures with
        | Error msg ->
            write ctx "ERROR: COM client creation failed: %s" msg
            2
        | Ok service ->

            use service = service
            printEvidence ctx service |> ignore
            write ctx "Starting fixture capture..."
            write ctx "Output directory: %s" args.FixturesOutputDir
            write ctx "Specs: %s" (String.concat ", " args.Specs)
            write ctx "From time: %s" (args.FromTime.ToString("yyyy-MM-dd HH:mm:ss"))

            match args.ToTime with
            | Some toTime -> write ctx "To time: %s" (toTime.ToString("yyyy-MM-dd HH:mm:ss"))
            | None -> write ctx "To time: (no limit)"

            write ctx "Max records per type: %d" args.MaxRecordsPerType

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
                write ctx ""
                write ctx "Processing spec: %s" spec
                let specDir = Path.Combine(args.FixturesOutputDir, spec.Replace("/", "_"))

                let request = Validation.createOpenRequest spec args.FromTime 1

                // Use StreamPayloads with a total read cap to avoid reading all
                // records (which can exceed the E2E timeout for large datasets).
                let totalReadCap = args.MaxRecordsPerType * knownRecordTypes.Length * 3
                let mutable firstError: XanthosError option = None

                let payloads =
                    service.StreamPayloads(request)
                    |> Seq.choose (fun r ->
                        match r with
                        | Ok p -> Some p
                        | Error err ->
                            if firstError.IsNone then
                                firstError <- Some err

                            None)
                    |> Seq.truncate totalReadCap
                    |> Seq.toList

                let fetchResult =
                    match firstError with
                    | Some err when payloads.IsEmpty -> Error err
                    | _ -> Ok payloads

                match fetchResult with
                | Ok payloads ->
                    let filteredPayloads = filterByToTime payloads

                    write
                        ctx
                        "  Fetched %d payload(s) (after filter: %d, read cap: %d)"
                        payloads.Length
                        filteredPayloads.Length
                        totalReadCap

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

                            write
                                ctx
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

                                File.WriteAllText(metaFilename, meta, ConsoleEncoding.utf8NoBom)
                                totalCaptured <- totalCaptured + 1)
                    else
                        write ctx "  No records to capture for this spec."

                | Error err ->
                    write ctx "  ERROR: %s" (describeError err)
                    totalErrors <- totalErrors + 1

            // Print comprehensive summary
            write ctx ""
            write ctx "=========================================="
            write ctx "  CAPTURE SUMMARY"
            write ctx "=========================================="
            write ctx ""
            write ctx "Records captured: %d" totalCaptured
            write ctx "Parse errors: %d" parseErrors
            write ctx "Spec fetch errors: %d" totalErrors
            write ctx ""

            // Coverage analysis
            let capturedSet = capturedTypes |> Set.ofSeq
            let knownSet = knownRecordTypes |> Set.ofList
            let covered = Set.intersect capturedSet knownSet |> Set.count
            let missing = Set.difference knownSet capturedSet
            let extra = Set.difference capturedSet knownSet

            write ctx "COVERAGE ANALYSIS"
            write ctx "-----------------"

            write
                ctx
                "Record types captured: %d/%d (%.1f%%)"
                covered
                knownRecordTypes.Length
                (float covered / float knownRecordTypes.Length * 100.0)

            write ctx ""

            if not (Set.isEmpty missing) then
                let missingStr = missing |> Set.toList |> String.concat ", "
                write ctx "Missing record types (%d): %s" missing.Count missingStr
                write ctx ""

            if not (Set.isEmpty extra) then
                let extraStr = extra |> Set.toList |> String.concat ", "
                write ctx "Unknown record types captured (%d): %s" extra.Count extraStr
                write ctx ""

            // Group captured by category for better overview
            let categoryMap =
                [ "Race Data", [ "TK"; "RA"; "SE"; "HR" ]
                  "Odds Data", [ "O1"; "O2"; "O3"; "O4"; "O5"; "O6" ]
                  "Vote Count", [ "H1"; "H5"; "H6" ]
                  "Master Data", [ "UM"; "KS"; "CH"; "BR"; "BN"; "HN"; "SK"; "RC" ]
                  "Analysis Data", [ "CK"; "HC"; "HS"; "HY"; "YS"; "BT"; "CS"; "DM"; "TM"; "WF"; "WC" ]
                  "Real-time Data", [ "WH"; "WE"; "AV"; "JC"; "TC"; "CC"; "JG" ] ]

            write ctx "COVERAGE BY CATEGORY"
            write ctx "--------------------"

            for (category, types) in categoryMap do
                let capturedInCategory = types |> List.filter capturedSet.Contains
                let pct = float capturedInCategory.Length / float types.Length * 100.0

                let status =
                    if capturedInCategory.Length = types.Length then
                        "✓"
                    else
                        " "

                write ctx "%s %-15s: %d/%d (%.0f%%)" status category capturedInCategory.Length types.Length pct

            write ctx ""

            if parseErrors > 0 then
                write ctx "WARNING: %d record(s) failed to parse. Check metadata files for details." parseErrors
                1
            elif totalErrors > 0 then
                1
            else
                0

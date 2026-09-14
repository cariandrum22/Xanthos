module Xanthos.Cli.FunctionalExecution

open System
open System.IO
open System.Threading
open System.Collections.Concurrent
open Xanthos
open Xanthos.Cli.Types
open Xanthos.Cli.Execution

exception private CommandFailure of JvError

/// Internal host boundary. Production has one COM factory and never falls back.
type internal Dependencies =
    { Connect: unit -> Result<Session, JvError>
      WriteLine: string -> unit
      CancellationToken: CancellationToken
      HandleConsoleCancellation: bool
      HasDesktop: bool
      EventQueueCapacity: int
      Mode: string }

let private connection () =
    let id = Environment.GetEnvironmentVariable "XANTHOS_COM_PROGID"

    if String.IsNullOrWhiteSpace id then
        ConnectionOptions.Default
    else
        { ProgId = id }

let internal productionDependencies () =
    { Connect = fun () -> JvLink.connect (connection ())
      WriteLine = Console.WriteLine
      CancellationToken = CancellationToken.None
      HandleConsoleCancellation = true
      EventQueueCapacity = 256
      HasDesktop =
        not (OperatingSystem.IsWindows())
        || Diagnostics.Process.GetCurrentProcess().SessionId <> 0
      Mode = "COM" }

type internal CommandRunner(dependencies: Dependencies) =
    let printfn format =
        Printf.kprintf dependencies.WriteLine format

    let require =
        function
        | Ok value -> value
        | Error error -> raise (CommandFailure error)

    let invalid api message =
        raise (
            CommandFailure
                { Api = api
                  Code = None
                  Kind = JvErrorKind.InvalidInput
                  Outputs = Map.empty
                  Message = message }
        )

    let parse spec (data: byte[]) =
        let id = RecordBytes.ascii "" "RecordId" 1 2 data |> Result.defaultValue ""

        let raceDate =
            if List.contains id [ "O1"; "O2"; "O3"; "O4"; "O5"; "O6" ] then
                RecordBytes.date id "RaceDate" 12 data
                |> Result.toOption
                |> Option.bind snd
                |> Option.defaultValue DateOnly.MaxValue
            else
                DateOnly.MaxValue

        let options =
            DataSpecs.parseOptions spec raceDate
            |> Option.defaultValue Records.ParseOptions.Default

        match Records.parseWith options data with
        | Ok _ -> id
        | Error e -> invalid "Records.parse" $"{e.RecordId}.{e.Field} byte={e.Position} length={e.Length}: {e.Message}"

    let useGets (ctx: ExecutionContext) =
        let isTrue (text: string) =
            not (String.IsNullOrEmpty text)
            && not (List.contains (text.ToLowerInvariant()) [ "0"; "false"; "no"; "off" ])

        match ctx.Config.UseJvGets with
        | Some value -> value
        | None ->
            let read = Environment.GetEnvironmentVariable "XANTHOS_USE_JVREAD"
            let gets = Environment.GetEnvironmentVariable "XANTHOS_USE_JVGETS"

            if not (isNull read) then not (isTrue read)
            elif not (isNull gets) then isTrue gets
            else true

    let withConnection (config: Runtime.JvLinkConfig) action =
        let mutable exitCode = 0

        let report error =
            exitCode <- 2
            printfn "%s failed: code=%A %s" error.Api error.Code error.Message

            for name in [ "key"; "origin"; "capacity" ] do
                error.Outputs
                |> Map.tryFind name
                |> Option.iter (fun value -> printfn "%s=%s" name value)

            error.Outputs
            |> Map.tryFind "filename"
            |> Option.iter (fun value -> printfn "Recovery filename: %s" value)

        match dependencies.Connect() with
        | Error e ->
            printfn "%s client creation failed: %s" dependencies.Mode e.Message
            2
        | Ok session ->
            try
                try
                    JvLink.init config.Sid session |> require

                    config.ServiceKey
                    |> Option.iter (fun key -> JvLink.setServiceKey key session |> require)

                    config.SavePath
                    |> Option.iter (fun path -> JvLink.setSavePath path session |> require)

                    printfn "EVIDENCE:MODE=%s" dependencies.Mode
                    printfn "EVIDENCE:API=FUNCTIONAL"
                    printfn "EVIDENCE:VERSION=%s" (JvLink.getVersion session |> require)
                    action session
                with
                | CommandFailure e -> report e
                | ex ->
                    exitCode <- 2
                    printfn "Command failed: %s" ex.Message
            finally
                match JvLink.disconnect session with
                | Ok() -> ()
                | Error error -> report error

            exitCode

    let opened request session =
        match DataSpecs.validateOpen request with
        | Ok() -> ()
        | Error message -> invalid "JVOpen" message

        match JvLink.openData request session |> require with
        | OpenOutcome.Opened metadata ->
            printfn "OPEN files=%d downloads=%d" metadata.ReadCount metadata.DownloadCount
            true
        | OpenOutcome.NoData _ ->
            printfn "NO_DATA spec=%s" request.Dataspec
            false

    let readRecords useGets spec maximum output onRecord (cancel: CancellationToken) session =
        let mutable count = 0
        let mutable eof = false

        while not eof && count < maximum && not cancel.IsCancellationRequested do
            let result = (if useGets then JvLink.gets else JvLink.read) session |> require

            match result.State with
            | ReadState.Record ->
                let id = parse spec result.Data
                count <- count + 1

                output
                |> Option.iter (fun dir ->
                    Directory.CreateDirectory dir |> ignore
                    File.WriteAllBytes(Path.Combine(dir, $"{id}_{count:D6}.bin"), result.Data))

                onRecord id result

                printfn
                    "RECORD id=%s bytes=%d parsed=true method=%s"
                    id
                    result.ByteCount
                    (if useGets then "JVGets" else "JVRead")
            | ReadState.FileBoundary -> ()
            | ReadState.DownloadPending -> cancel.WaitHandle.WaitOne(100) |> ignore
            | ReadState.EndOfStream -> eof <- true

        if cancel.IsCancellationRequested then
            JvLink.cancel session |> require

        count

    let withCancellation action =
        use source =
            CancellationTokenSource.CreateLinkedTokenSource(dependencies.CancellationToken)

        let handler =
            ConsoleCancelEventHandler(fun _ args ->
                args.Cancel <- true
                source.Cancel())

        if dependencies.HandleConsoleCancellation then
            Console.CancelKeyPress.AddHandler handler

        try
            action source.Token
        finally
            if dependencies.HandleConsoleCancellation then
                Console.CancelKeyPress.RemoveHandler handler

    let download ctx (args: DownloadArgs) check session =
        let request =
            { Dataspec = args.Request.Spec
              FromTime = args.Request.FromTime
              ToTime = None
              Option = args.Request.Option }

        let useGets = useGets ctx

        withCancellation (fun token ->
            try
                if opened request session then
                    printfn "STATUS completed=%d" (JvLink.status session |> require)

                    let count =
                        readRecords
                            useGets
                            request.Dataspec
                            (defaultArg args.MaxRecords (if check then 1 else Int32.MaxValue))
                            args.OutputDirectory
                            (fun _ _ -> ())
                            token
                            session

                    if check && count = 0 then
                        invalid "session-check" "A non-empty parsed response is required."

                    if check then
                        JvLink.skip session |> require
                        printfn "SKIP succeeded"
                        JvLink.cancel session |> require
                        printfn "CANCEL succeeded"

                    printfn "Download completed (spec=%s option=%d records=%d)." request.Dataspec request.Option count
                elif check then
                    invalid "session-check" "NoData cannot satisfy the stateful success scenario."
            finally
                JvLink.closeData session |> require
                printfn "CLOSE succeeded"

            if check then
                try
                    if not (opened request session) then
                        invalid "session-check" "Reopen returned NoData."

                    let count =
                        readRecords useGets request.Dataspec 1 None (fun _ _ -> ()) token session

                    if count <> 1 then
                        invalid "session-check" "Reopen did not yield a parsed record."

                    printfn "REOPEN succeeded"
                finally
                    JvLink.closeData session |> require)

    let realtime useGets (args: RealtimeArgs) (token: CancellationToken) session =
        let mutable again = true

        while again && not token.IsCancellationRequested do
            try
                match JvLink.openRealtime args.Spec args.Key session |> require with
                | RealtimeOpenOutcome.NoData -> printfn "NO_DATA spec=%s key=%s" args.Spec args.Key
                | RealtimeOpenOutcome.Opened ->
                    readRecords useGets args.Spec Int32.MaxValue None (fun _ _ -> ()) token session
                    |> ignore
            finally
                JvLink.closeData session |> require

            again <- args.Continuous

            if again then
                token.WaitHandle.WaitOne(500) |> ignore

        printfn "Realtime stream completed for %s (key=%s)." args.Spec args.Key

    let movie (args: MovieOpenArgs) (token: CancellationToken) session =
        try
            match JvLink.movieOpen args.MovieOpenType args.MovieSearchKey session |> require with
            | VideoOpenOutcome.NoData ->
                printfn "NO_DATA movie type=%s search=%s" args.MovieOpenType args.MovieSearchKey
            | VideoOpenOutcome.Opened ->
                let mutable eof = false

                while not eof && not token.IsCancellationRequested do
                    match JvLink.movieRead session |> require with
                    | VideoReadOutcome.Record(text, bytes, capacity) ->
                        printfn "MOVIE bytes=%d capacity=%d key=%s" bytes capacity text
                    | VideoReadOutcome.EndOfStream _ -> eof <- true
                    | VideoReadOutcome.DownloadPending _ -> token.WaitHandle.WaitOne(100) |> ignore
        finally
            JvLink.closeData session |> require

    let watch config (args: WatchArgs) (token: CancellationToken) session =
        use queue = new BlockingCollection<JvEvent>(dependencies.EventQueueCapacity)
        let gate = obj ()
        let mutable overflow = None
        let mutable accepting = true
        let mutable accepted = 0L
        let mutable processed = 0L

        let enqueue event =
            let mutable failed = false

            lock gate (fun () ->
                if accepting && overflow.IsNone then
                    if queue.TryAdd event then
                        accepted <- accepted + 1L
                    else
                        failed <- true

                        overflow <-
                            Some
                                { Api = "cliEventQueue"
                                  Code = None
                                  Kind = JvErrorKind.Busy
                                  Outputs =
                                    Map.ofList
                                        [ "key", event.RawKey
                                          "origin", string event.Kind
                                          "capacity", string dependencies.EventQueueCapacity ]
                                  Message =
                                    "CLI event queue capacity was exceeded; pending keys are listed for later retrieval." })

            if failed then
                printfn
                    "QUEUE_OVERFLOW origin=%A rawKey=%s capacity=%d"
                    event.Kind
                    event.RawKey
                    dependencies.EventQueueCapacity

        use subscription = JvLink.subscribe enqueue session |> require
        let timer = Diagnostics.Stopwatch.StartNew()
        let mutable bodyFailed = false

        try
            try
                while not token.IsCancellationRequested
                      && (args.Duration |> Option.forall (fun span -> timer.Elapsed < span)) do
                    lock gate (fun () -> overflow)
                    |> Option.iter (fun e -> raise (CommandFailure e))

                    JvLink.subscriptionError subscription
                    |> Option.iter (fun e -> raise (CommandFailure e))

                    let mutable evt = Unchecked.defaultof<JvEvent>

                    if queue.TryTake(&evt) then
                        printfn "EVENT origin=%A rawKey=%s" evt.Kind evt.RawKey

                        if args.OpenAfterRealtime then
                            let request = JvLink.toRealtimeRequest evt |> require

                            let code =
                                withConnection
                                    config
                                    (realtime
                                        true
                                        { Spec = request.Dataspec
                                          Key = request.Key
                                          Continuous = false }
                                        token)

                            if code <> 0 then
                                invalid "watch-events" "Event retrieval failed."

                        processed <- processed + 1L
                    else
                        token.WaitHandle.WaitOne(100) |> ignore
                // Cancellation/duration must not hide an overflow that raced with exit.
                lock gate (fun () ->
                    accepting <- false
                    overflow)
                |> Option.orElseWith (fun () -> JvLink.subscriptionError subscription)
                |> Option.iter (fun e -> raise (CommandFailure e))
            with _ ->
                bodyFailed <- true
                reraise ()
        finally
            lock gate (fun () -> accepting <- false)
            let stopped = JvLink.unsubscribe subscription
            // Accepted keys are retained in output even when retrieval cannot continue.
            let pending = queue.Count
            let mutable evt = Unchecked.defaultof<JvEvent>

            while queue.TryTake(&evt) do
                printfn "EVENT_PENDING origin=%A rawKey=%s retrieval=not-run" evt.Kind evt.RawKey

            printfn "Watch summary: accepted=%d processed=%d pending=%d" accepted processed pending

            match stopped with
            | Error error when bodyFailed -> printfn "Cleanup failed: %s code=%A %s" error.Api error.Code error.Message
            | result -> result |> require

        printfn "Watch stopped."

    member _.Run(ctx, command) =
        withConnection ctx.Config (fun session ->
            let doneCall result =
                result |> require
                printfn "Operation succeeded."

            match command with
            | Version -> printfn "JV-Link version: %s" (JvLink.getVersion session |> require)
            | Download args -> download ctx args false session
            | SessionCheck args -> download ctx args true session
            | Realtime args -> withCancellation (fun token -> realtime (useGets ctx) args token session)
            | Status -> printfn "Status: Completed %d file(s)." (JvLink.status session |> require)
            | Skip -> JvLink.skip session |> doneCall
            | Cancel -> JvLink.cancel session |> doneCall
            | DeleteFile name -> JvLink.deleteFile name session |> doneCall
            | SetSaveFlag enabled -> JvLink.setSaveFlag enabled session |> doneCall
            | GetSaveFlag -> printfn "Save flag: %b" ((JvLink.getSaveFlag session |> require) <> 0)
            | SetSavePath path -> JvLink.setSavePath path session |> doneCall
            | GetSavePath -> printfn "Save path: %s" (JvLink.getSavePath session |> require)
            | SetServiceKey key -> JvLink.setServiceKey key session |> doneCall
            | GetServiceKey ->
                printfn
                    "Service key: %s"
                    (if String.IsNullOrWhiteSpace(JvLink.getServiceKey session |> require) then
                         "not configured"
                     else
                         "registered")
            | SetParentHwnd handle -> JvLink.setParentWindowHandle handle session |> doneCall
            | GetParentHwnd -> invalid "ParentHWnd" "ParentHWnd is write-only."
            | SetPayoffDialog _ -> invalid "m_payflag" "m_payflag is read-only; use set-ui-properties."
            | GetPayoffDialog -> printfn "Payoff dialog suppressed: %b" ((JvLink.getPayFlag session |> require) <> 0)
            | SetUiProperties -> JvLink.configureUi session |> doneCall
            | TotalReadSize ->
                printfn "Total read file size: %d bytes" (JvLink.getTotalReadFileSize session |> require).Bytes
            | CurrentReadSize ->
                printfn "Current file size: %d bytes" (JvLink.getCurrentReadFileSize session |> require)
            | CurrentFileTimestamp ->
                printfn "Current file timestamp: %s" (JvLink.getCurrentFileTimestamp session |> require)
            | CourseFile key ->
                let image = JvLink.courseFile key session |> require

                printfn
                    "Course file [%s]: State=%A Path=%s Explanation=%s"
                    key
                    image.State
                    image.Value.Filepath
                    image.Value.Explanation
            | CourseFile2 args ->
                let image = JvLink.courseFile2 args.Key args.OutputPath session |> require
                printfn "Course file (v2) [%s]: State=%A Path=%s" args.Key image.State image.Value
            | SilksFile args ->
                let image = JvLink.silksFile args.Pattern args.OutputPath session |> require
                printfn "Silks image: State=%A Path=%s" image.State image.Value
            | SilksBinary pattern ->
                let image = JvLink.silksBinary pattern session |> require
                printfn "Silks image: State=%A bytes=%d" image.State image.Value.Length
            | MovieCheck key -> printfn "Movie availability: %A" (JvLink.movieCheck key session |> require)
            | MovieCheckWithType args ->
                printfn
                    "Movie availability: %A"
                    (JvLink.movieCheckWithType args.MovieTypeCode args.MovieKey session |> require)
            | MoviePlay key -> JvLink.moviePlay key session |> doneCall
            | MoviePlayWithType args -> JvLink.moviePlayWithType args.MovieTypeCode args.MovieKey session |> doneCall
            | MovieOpen args -> withCancellation (fun token -> movie args token session)
            | WatchEvents args -> withCancellation (fun token -> watch ctx.Config args token session)
            | CaptureFixtures args ->
                withCancellation (fun token ->
                    Directory.CreateDirectory args.FixturesOutputDir |> ignore
                    let version = JvLink.getVersion session |> require

                    for spec in args.Specs do
                        let counts = Collections.Generic.Dictionary<string, int>()

                        let request =
                            { Dataspec = spec
                              FromTime = args.FromTime
                              ToTime = args.ToTime
                              Option = 1 }

                        try
                            if opened request session then
                                readRecords
                                    args.UseJvGets
                                    spec
                                    Int32.MaxValue
                                    None
                                    (fun id record ->
                                        let count =
                                            match counts.TryGetValue id with
                                            | true, n -> n
                                            | _ -> 0

                                        if count < args.MaxRecordsPerType then
                                            let filename =
                                                Path.Combine(
                                                    args.FixturesOutputDir,
                                                    $"{spec}_{id}_{count + 1:D3}.bin"
                                                )

                                            File.WriteAllBytes(filename, record.Data)

                                            let metadata =
                                                {| mode = dependencies.Mode
                                                   api = "FUNCTIONAL"
                                                   sdkVersion = version
                                                   pointerSize = IntPtr.Size
                                                   capturedAt = DateTimeOffset.UtcNow
                                                   dataspec = spec
                                                   fromTime = request.FromTime.ToString("yyyyMMddHHmmss")
                                                   toTime =
                                                    request.ToTime
                                                    |> Option.map (fun t -> t.ToString("yyyyMMddHHmmss"))
                                                    |> Option.toObj
                                                   requestTimeZone = "Asia/Tokyo"
                                                   option = request.Option
                                                   recordType = id
                                                   byteLength = record.Data.Length
                                                   sourceFile = record.Filename
                                                   readMethod = if args.UseJvGets then "JVGets" else "JVRead"
                                                   sha256 =
                                                    Convert.ToHexString(
                                                        Security.Cryptography.SHA256.HashData record.Data
                                                    )
                                                   parseStatus = "ok" |}

                                            File.WriteAllText(
                                                Path.ChangeExtension(filename, ".meta.json"),
                                                Text.Json.JsonSerializer.Serialize(metadata)
                                            )

                                            counts[id] <- count + 1)
                                    token
                                    session
                                |> ignore
                        finally
                            JvLink.closeData session |> require)
            | Help -> ())

let internal runWith dependencies ctx command =
    CommandRunner(dependencies).Run(ctx, command)

let run ctx command =
    runWith (productionDependencies ()) ctx command

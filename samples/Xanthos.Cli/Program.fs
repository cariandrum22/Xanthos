module Xanthos.Cli.Program

open System
open System.Text
open Xanthos.Cli.Types
open Xanthos.Cli.Parsing
open Xanthos.Cli.Execution

let usage =
    """
Usage:
  xanthos.cli [global options] <command> [command options]

Global Options:
  --sid <sid>             JV-Link SID (default: UNKNOWN, or set XANTHOS_JVLINK_SID).
  --service-key <key>     Service key for JV-Init (or XANTHOS_JVLINK_SERVICE_KEY).
  --save-path <path>      Directory for persisted files (or XANTHOS_JVLINK_SAVE_PATH).
  --stub                  Force stub mode (default on non-Windows).
  --diag                  Enable COM diagnostics output.
  --use-jvgets            Force JVGets (default) regardless of env vars.
  --no-jvgets             Force JVRead (equivalent to XANTHOS_USE_JVREAD=1).
  --help                  Show this help text.

Commands:

  Data Retrieval:
    download              Bulk download payloads via JVOpen + JVRead/JVGets.
      --spec <dataspec>     Data specification (required, e.g., RACE, TOKU).
      --from <timestamp>    Start time YYYYMMDDHHmmss (required).
      --option <1-4>        JVOpen option (default: 1).
      --output <dir>        Output directory for persisted files.

    realtime              Stream realtime payloads via JVRTOpen.
      --spec <dataspec>     Data specification (required, e.g., 0B12, 0B11).
      --key <key>           Request key (required). Formats:
                            - YYYYMMDDJJKKHHRR (race-specific)
                            - YYYYMMDD (daily)
                            - WatchEvent raw key
      --continuous          Keep polling until Ctrl+C (default: exit on stream end).

  Session Control:
    status                Display current session status.
    skip                  Skip current file in session.
    cancel                Cancel current session.
    delete-file           Remove a persisted JV file.
      --name <filename>     Filename to delete (required).

  Event Monitoring:
    watch-events          Observe JV shared-memory notifications.
      --duration <sec>      Duration in seconds (optional, runs indefinitely).
      --open-after          Open realtime session after receiving event.

  Configuration:
    set-save-flag         Set save flag (true/false).
      --value <bool>        Value to set (required).
    get-save-flag         Get current save flag value.
    set-save-path         Set save path directory.
      --value <path>        Path to set (required).
    get-save-path         Get current save path.
    set-service-key       Set service key.
      --value <key>         Key to set (required).
    get-service-key       Get current service key.
    set-parent-hwnd       Set parent window handle.
      --value <handle>      Handle value as integer (required).
    get-parent-hwnd       Get current parent window handle (not supported in COM).
    set-payoff-dialog     Control payoff dialog display (not supported in COM; use set-ui-properties).
      --value <bool>        Enable/disable (required).
    get-payoff-dialog     Get payoff dialog setting.
    set-ui-properties     Synchronize UI state.

  Assets:
    course-file           Get course diagram file path.
      --key <code>          Course key (required).
    course-file2          Save course diagram to a file (v2).
      --key <code>          Course key (required).
      --output <path>       Output file path (required).
    silks-file            Generate silks file.
      --pattern <code>      Pattern code (required).
      --output <path>       Output file path (required).
    silks-binary          Get silks binary data.
      --pattern <code>      Pattern code (required).

  Movie APIs:
    movie-check           Check movie availability.
      --key <key>           Movie key (required).
    movie-check-with-type Check movie availability by type.
      --movie-type <code>   Movie type code (required).
      --key <key>           Movie key (required).
    movie-play            Play movie.
      --key <key>           Movie key (required).
    movie-play-with-type  Play movie by type.
      --movie-type <code>   Movie type code (required).
      --key <key>           Movie key (required).
    movie-open            Retrieve all workout video listings.
      --movie-type <code>   Movie type code (required).
      --search-key <key>    Search key (required).

  Metrics:
    version               Display JV-Link version information.
    total-read-size       Report total file size for session.
    current-read-size     Report current read position.
    current-file-timestamp Report current file timestamp.

  Testing (Windows only):
    capture-fixtures      Capture real COM records as test fixtures.
                          All options have defaults - just run 'capture-fixtures' to start.
      --output <dir>        Output directory (default: tests/fixtures).
      --specs <list>        Comma-separated dataspecs (default: RACE,DIFF).
      --from <timestamp>    Start time YYYYMMDDHHmmss (default: 30 days ago).
      --to <timestamp>      End time YYYYMMDDHHmmss (optional).
      --max-records <n>     Max records per type (default: 10).
      --use-jvgets          Force JVGets (default).
"""

let private printHelp () =
    printfn "%s" (usage.Trim())
    0

let private configureConsoleEncoding () =
    // JV-Link returns Japanese text; when stdout/stderr is redirected (E2E harness, Test Explorer, CI),
    // the default Windows code page output can be decoded as UTF-8 by the consumer and appear garbled.
    // Prefer UTF-8 for redirected output to make captured logs readable.
    try
        let utf8 = UTF8Encoding(false)

        if Console.IsOutputRedirected || Console.IsErrorRedirected then
            Console.OutputEncoding <- utf8

        if Console.IsInputRedirected then
            Console.InputEncoding <- utf8
    with _ ->
        ()

let private runCommand ctx command =
    match command with
    | Download args -> runDownload ctx args
    | Realtime args -> runRealtime ctx args
    | Status -> runStatus ctx
    | Skip -> runSkip ctx
    | Cancel -> runCancel ctx
    | DeleteFile name -> runDeleteFile ctx name
    | WatchEvents args -> runWatchEvents ctx args
    | SetSaveFlag value -> runSetSaveFlag ctx value
    | GetSaveFlag -> runGetSaveFlag ctx
    | SetSavePath path -> runSetSavePath ctx path
    | GetSavePath -> runGetSavePath ctx
    | SetServiceKey key -> runSetServiceKey ctx key
    | GetServiceKey -> runGetServiceKey ctx
    | CourseFile key -> runCourseFile ctx key
    | CourseFile2 args -> runCourseFile2 ctx args
    | SilksFile args -> runSilksFile ctx args
    | SilksBinary pattern -> runSilksBinary ctx pattern
    | MovieCheck key -> runMovieCheck ctx key
    | MovieCheckWithType args -> runMovieCheckWithType ctx args
    | MoviePlay key -> runMoviePlay ctx key
    | MoviePlayWithType args -> runMoviePlayWithType ctx args
    | MovieOpen args -> runMovieOpen ctx args
    | SetUiProperties -> runSetUiProperties ctx
    | Version -> runVersion ctx
    | TotalReadSize -> runTotalReadSize ctx
    | CurrentReadSize -> runCurrentReadSize ctx
    | CurrentFileTimestamp -> runCurrentFileTimestamp ctx
    | SetParentHwnd handle -> runSetParentHwnd ctx handle
    | GetParentHwnd -> runGetParentHwnd ctx
    | SetPayoffDialog value -> runSetPayoffDialog ctx value
    | GetPayoffDialog -> runGetPayoffDialog ctx
    | CaptureFixtures args -> runCaptureFixtures ctx args
    | Help -> printHelp ()

[<STAThread>]
[<EntryPoint>]
let main argv =
    configureConsoleEncoding ()

    match parseInput argv with
    | Error msg ->
        printfn "%s\n%s" msg (usage.Trim())
        1
    | Ok parsed ->
        match parsed.Command with
        | Help -> printHelp ()
        | _ ->
            match createExecutionContext parsed.Globals with
            | Error err -> reportError "Configuration error" err
            | Ok ctx ->
                printfn "[diag] Client mode = %s" (describeMode ctx.Activation)
                configureDiagnostics ctx.Globals.EnableDiagnostics ctx.Logger
                // Each command creates its own service with a fresh client.
                // The service owns the client and disposes it when done.
                runCommand ctx parsed.Command

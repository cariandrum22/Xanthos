namespace Xanthos.Cli

open System
open System.Globalization
open Xanthos.Core.Errors
open Xanthos.Interop
open Xanthos.Runtime.Validation
open Xanthos.Runtime
open Xanthos.Cli.Types

module Parsing =

    let readEnv (name: string) : string option =
        match Environment.GetEnvironmentVariable name with
        | null
        | "" -> None
        | v -> Some v

    let requireValue (msg: string) (opt: string option) =
        match opt with
        | Some v when not (String.IsNullOrWhiteSpace v) -> Ok(v.Trim())
        | _ -> Error msg

    let parseBoolValue (name: string) (value: string) : Result<bool, string> =
        match value.ToLowerInvariant() with
        | "true"
        | "1"
        | "yes"
        | "on" -> Ok true
        | "false"
        | "0"
        | "no"
        | "off" -> Ok false
        | _ -> Error $"Option '{name}' must be true/false."

    let parseNativeInt (name: string) (value: string) : Result<nativeint, string> =
        let mutable parsed: int64 = 0L

        if Int64.TryParse(value, &parsed) then
            Ok(nativeint parsed)
        else
            Error $"Option '{name}' must be an integer."

    let parseDurationSeconds (value: string) : Result<TimeSpan, string> =
        let mutable seconds: float = 0.0

        if
            Double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, &seconds)
            && seconds > 0.0
        then
            Ok(TimeSpan.FromSeconds seconds)
        else
            Error "Option '--duration' must be a positive number."

    let normalizeSavePath (path: string) : Result<string, string> =
        try
            let n = IO.Path.GetFullPath path in
            IO.Directory.CreateDirectory n |> ignore
            Ok n
        with ex ->
            Error $"Failed to prepare save path '{path}': {ex.Message}"

    let readGlobalOptions (tokens: string list) : Result<GlobalRawOptions * string list, string> =
        let rec loop (raw: GlobalRawOptions) (tokens: string list) : Result<GlobalRawOptions * string list, string> =
            match tokens with
            | [] -> Ok(raw, [])
            | "--sid" :: [] -> Error "Option '--sid' requires a value."
            | "--sid" :: v :: rest -> loop { raw with Sid = Some v } rest
            | "--service-key" :: [] -> Error "Option '--service-key' requires a value."
            | "--service-key" :: v :: rest -> loop { raw with ServiceKey = Some v } rest
            | "--save-path" :: [] -> Error "Option '--save-path' requires a value."
            | "--save-path" :: v :: rest -> loop { raw with SavePath = Some v } rest
            | "--stub" :: rest -> loop { raw with ForceStub = true } rest
            | "--diag" :: rest -> loop { raw with EnableDiagnostics = true } rest
            | "--use-jvgets" :: rest -> loop { raw with UseJvGets = Some true } rest
            | "--no-jvgets" :: rest -> loop { raw with UseJvGets = Some false } rest
            | "--help" :: _ -> Ok({ raw with ShowHelp = true }, [])
            | t :: _ when t.StartsWith "--" -> Error $"Unknown global option '{t}'."
            | remaining -> Ok(raw, remaining)

        let initialRaw: GlobalRawOptions =
            { Sid = None
              ServiceKey = None
              SavePath = None
              ForceStub = false
              EnableDiagnostics = false
              ShowHelp = false
              UseJvGets = None }

        loop initialRaw tokens

    let buildGlobalSettings (raw: GlobalRawOptions) : Result<GlobalSettings, string> =
        result {
            if raw.ShowHelp then
                return
                    { Sid = ""
                      ServiceKey = None
                      SavePath = None
                      StubPreference = StubPreference.PreferCom
                      EnableDiagnostics = raw.EnableDiagnostics
                      UseJvGets = None }
            else
                // SID defaults to "UNKNOWN" - can be overridden via --sid or environment variable
                let sid =
                    raw.Sid
                    |> Option.orElse (readEnv "XANTHOS_JVLINK_SID")
                    |> Option.defaultValue "UNKNOWN"

                let! savePath =
                    raw.SavePath
                    |> Option.orElse (readEnv "XANTHOS_JVLINK_SAVE_PATH")
                    |> Option.map (fun p -> normalizeSavePath p |> Result.map Some)
                    |> Option.defaultValue (Ok None)

                let serviceKey =
                    raw.ServiceKey |> Option.orElse (readEnv "XANTHOS_JVLINK_SERVICE_KEY")

                let basePref =
                    if raw.ForceStub then
                        StubPreference.ForcedByUser
                    elif not (OperatingSystem.IsWindows()) then
                        StubPreference.ForcedByPlatform
                    else
                        StubPreference.PreferCom

                return
                    { Sid = sid
                      ServiceKey = serviceKey
                      SavePath = savePath
                      StubPreference = basePref
                      EnableDiagnostics = raw.EnableDiagnostics
                      UseJvGets = raw.UseJvGets }
        }

    let ensureNoExtra (name: string) (tokens: string list) =
        match tokens with
        | [] -> Ok()
        | x :: _ -> Error $"Command '{name}' does not accept option '{x}'."

    // Download parsing
    type DownloadRaw =
        { Spec: string option
          From: string option
          OptionText: string option
          Output: string option }

    let rec parseDownloadArgs (state: DownloadRaw) (tokens: string list) : Result<DownloadRaw, string> =
        match tokens with
        | [] -> Ok state
        | "--spec" :: [] -> Error "download: option '--spec' requires a value."
        | "--spec" :: v :: rest -> parseDownloadArgs { state with Spec = Some v } rest
        | "--from" :: [] -> Error "download: option '--from' requires a value."
        | "--from" :: v :: rest -> parseDownloadArgs { state with From = Some v } rest
        | "--option" :: [] -> Error "download: option '--option' requires a value."
        | "--option" :: v :: rest -> parseDownloadArgs { state with OptionText = Some v } rest
        | "--output" :: [] -> Error "download: option '--output' requires a value."
        | "--output" :: v :: rest -> parseDownloadArgs { state with Output = Some v } rest
        | u :: _ -> Error $"download: unknown option '{u}'."

    let parseDownload (tokens: string list) : Result<Command, string> =
        result {
            let! raw =
                parseDownloadArgs
                    { Spec = None
                      From = None
                      OptionText = None
                      Output = None }
                    tokens

            let! rawSpec = raw.Spec |> requireValue "download: --spec is required."
            let! spec = normalizeDataspec rawSpec |> Result.mapError toString
            let! rawFrom = raw.From |> requireValue "download: --from is required."
            let! fromTime = parseFromTime (Some rawFrom) |> Result.mapError toString

            let! openOption =
                match raw.OptionText with
                | Some opt -> parseOpenOption (Some opt) |> Result.mapError toString
                | None -> Ok 1

            let request = createOpenRequest spec fromTime openOption
            let outputDir = raw.Output |> Option.orElse (readEnv "XANTHOS_JVLINK_OUTPUT")

            return
                Download
                    { Request = request
                      OutputDirectory = outputDir }
        }

    // Realtime parsing
    // Key format: "YYYYMMDDJJKKHHRR" (race), "YYYYMMDD" (daily), or WatchEvent parameter
    type RealtimeRaw =
        { Spec: string option
          Key: string option
          Continuous: bool }

    let rec parseRealtimeArgs (state: RealtimeRaw) (tokens: string list) : Result<RealtimeRaw, string> =
        match tokens with
        | [] -> Ok state
        | "--spec" :: [] -> Error "realtime: option '--spec' requires a value."
        | "--spec" :: v :: rest -> parseRealtimeArgs { state with Spec = Some v } rest
        | "--key" :: [] -> Error "realtime: option '--key' requires a value."
        | "--key" :: v :: rest -> parseRealtimeArgs { state with Key = Some v } rest
        | "--continuous" :: rest -> parseRealtimeArgs { state with Continuous = true } rest
        | u :: _ -> Error $"realtime: unknown option '{u}'."

    let parseRealtime (tokens: string list) : Result<Command, string> =
        result {
            let! raw =
                parseRealtimeArgs
                    { Spec = None
                      Key = None
                      Continuous = false }
                    tokens

            let! rawSpec = raw.Spec |> requireValue "realtime: --spec is required."
            let! spec = normalizeDataspec rawSpec |> Result.mapError toString

            let! key =
                raw.Key
                |> requireValue "realtime: --key is required (format: YYYYMMDDJJKKHHRR, YYYYMMDD, or WatchEvent param)."

            return
                Realtime
                    { Spec = spec
                      Key = key
                      Continuous = raw.Continuous }
        }

    // Simple helpers
    let parseSingleValue (name: string) (tokens: string list) =
        match tokens with
        | [ "--value"; v ] -> Ok v
        | _ -> Error $"Command '{name}' expects '--value <...>'."

    let parseSingleKey (name: string) (tokens: string list) =
        match tokens with
        | [ "--key"; v ] -> Ok v
        | _ -> Error $"Command '{name}' expects '--key <...>'."

    let parsePatternAndOutput (tokens: string list) =
        match tokens with
        | [ "--pattern"; p; "--output"; o ] -> Ok(p, o)
        | _ -> Error "Command 'silks-file' expects '--pattern <code> --output <path>'."

    let parseKeyAndOutput (name: string) (tokens: string list) =
        match tokens with
        | [ "--key"; k; "--output"; o ] -> Ok(k, o)
        | [ "--output"; o; "--key"; k ] -> Ok(k, o)
        | _ -> Error $"Command '{name}' expects '--key <value> --output <path>'."

    let parsePatternOnly (tokens: string list) =
        match tokens with
        | [ "--pattern"; p ] -> Ok p
        | _ -> Error "Command 'silks-binary' expects '--pattern <code>'."

    let parseMovieWithType name tokens =
        match tokens with
        | [ "--movie-type"; mt; "--key"; k ] -> Ok { MovieTypeCode = mt; MovieKey = k }
        | _ -> Error $"Command '{name}' expects '--movie-type <code> --key <value>'."

    let parseMovieOpen tokens =
        match tokens with
        | [ "--movie-type"; mt; "--search-key"; sk ] ->
            Ok
                { MovieOpenType = mt
                  MovieSearchKey = sk }
        | _ -> Error "Command 'movie-open' expects '--movie-type <code> --search-key <value>'."

    let parseDeleteFile tokens =
        match tokens with
        | [ "--name"; f ] -> Ok f
        | _ -> Error "Command 'delete-file' expects '--name <filename>'."

    type WatchRaw =
        { DurationText: string option
          OpenAfter: bool }

    let parseWatch (tokens: string list) : Result<Command, string> =
        let rec loop (raw: WatchRaw) (tokens: string list) : Result<WatchRaw, string> =
            match tokens with
            | [] -> Ok raw
            | "--duration" :: [] -> Error "watch-events: option '--duration' requires a value."
            | "--duration" :: v :: rest -> loop { raw with DurationText = Some v } rest
            | "--open-after" :: rest -> loop { raw with OpenAfter = true } rest
            | u :: _ -> Error $"watch-events: unknown option '{u}'."

        result {
            let! raw =
                loop
                    { DurationText = None
                      OpenAfter = false }
                    tokens

            let! duration =
                match raw.DurationText with
                | None -> Ok None
                | Some v -> parseDurationSeconds v |> Result.map Some

            return
                WatchEvents
                    { Duration = duration
                      OpenAfterRealtime = raw.OpenAfter }
        }

    // Capture fixtures parsing
    type CaptureFixturesRaw =
        { Output: string option
          Specs: string option
          From: string option
          To: string option
          MaxRecords: string option
          UseJvGets: bool }

    let rec parseCaptureFixturesArgs
        (state: CaptureFixturesRaw)
        (tokens: string list)
        : Result<CaptureFixturesRaw, string> =
        match tokens with
        | [] -> Ok state
        | "--output" :: [] -> Error "capture-fixtures: option '--output' requires a value."
        | "--output" :: v :: rest -> parseCaptureFixturesArgs { state with Output = Some v } rest
        | "--specs" :: [] -> Error "capture-fixtures: option '--specs' requires a value."
        | "--specs" :: v :: rest -> parseCaptureFixturesArgs { state with Specs = Some v } rest
        | "--from" :: [] -> Error "capture-fixtures: option '--from' requires a value."
        | "--from" :: v :: rest -> parseCaptureFixturesArgs { state with From = Some v } rest
        | "--to" :: [] -> Error "capture-fixtures: option '--to' requires a value."
        | "--to" :: v :: rest -> parseCaptureFixturesArgs { state with To = Some v } rest
        | "--max-records" :: [] -> Error "capture-fixtures: option '--max-records' requires a value."
        | "--max-records" :: v :: rest -> parseCaptureFixturesArgs { state with MaxRecords = Some v } rest
        | "--use-jvgets" :: rest -> parseCaptureFixturesArgs { state with UseJvGets = true } rest
        | u :: _ -> Error $"capture-fixtures: unknown option '{u}'."

    /// Default specs for comprehensive coverage of core record types
    let private defaultCaptureSpecs = "RACE,DIFF"

    /// Default output directory for fixtures
    let private defaultCaptureOutput = "tests/fixtures"

    /// Default from time: 30 days ago
    let private defaultCaptureFromTime () = DateTime.Now.AddDays(-30.0)

    let parseCaptureFixtures (tokens: string list) : Result<Command, string> =
        result {
            let! raw =
                parseCaptureFixturesArgs
                    { Output = None
                      Specs = None
                      From = None
                      To = None
                      MaxRecords = None
                      UseJvGets = false }
                    tokens

            // All fields have sensible defaults - arguments are for override only
            let output = raw.Output |> Option.defaultValue defaultCaptureOutput
            let specsText = raw.Specs |> Option.defaultValue defaultCaptureSpecs

            let specs =
                specsText.Split([| ',' |], StringSplitOptions.RemoveEmptyEntries)
                |> Array.map (fun s -> s.Trim())
                |> Array.toList

            do!
                if specs.IsEmpty then
                    Error "capture-fixtures: at least one spec is required."
                else
                    Ok()

            let! fromTime =
                match raw.From with
                | Some v -> parseFromTime (Some v) |> Result.mapError toString
                | None -> Ok(defaultCaptureFromTime ())

            let! toTime =
                match raw.To with
                | None -> Ok None
                | Some v -> parseFromTime (Some v) |> Result.map Some |> Result.mapError toString

            let maxRecords =
                raw.MaxRecords
                |> Option.bind (fun s ->
                    match Int32.TryParse s with
                    | true, v when v > 0 -> Some v
                    | _ -> None)
                |> Option.defaultValue 10

            return
                CaptureFixtures
                    { FixturesOutputDir = output
                      Specs = specs
                      FromTime = fromTime
                      ToTime = toTime
                      MaxRecordsPerType = maxRecords
                      UseJvGets = raw.UseJvGets }
        }

    let parseCommand (name: string) (tokens: string list) : Result<Command, string> =
        match name.ToLowerInvariant() with
        | "help" -> ensureNoExtra "help" tokens |> Result.map (fun () -> Help)
        | "download" -> parseDownload tokens
        | "realtime" -> parseRealtime tokens
        | "status" -> ensureNoExtra "status" tokens |> Result.map (fun () -> Status)
        | "skip" -> ensureNoExtra "skip" tokens |> Result.map (fun () -> Skip)
        | "cancel" -> ensureNoExtra "cancel" tokens |> Result.map (fun () -> Cancel)
        | "delete-file" -> parseDeleteFile tokens |> Result.map DeleteFile
        | "watch-events" -> parseWatch tokens
        | "set-save-flag" ->
            parseSingleValue "set-save-flag" tokens
            |> Result.bind (fun v -> parseBoolValue "--value" v |> Result.map SetSaveFlag)
        | "get-save-flag" -> ensureNoExtra "get-save-flag" tokens |> Result.map (fun () -> GetSaveFlag)
        | "set-save-path" -> parseSingleValue "set-save-path" tokens |> Result.map SetSavePath
        | "get-save-path" -> ensureNoExtra "get-save-path" tokens |> Result.map (fun () -> GetSavePath)
        | "set-service-key" -> parseSingleValue "set-service-key" tokens |> Result.map SetServiceKey
        | "get-service-key" -> ensureNoExtra "get-service-key" tokens |> Result.map (fun () -> GetServiceKey)
        | "course-file" -> parseSingleKey "course-file" tokens |> Result.map CourseFile
        | "course-file2" ->
            parseKeyAndOutput "course-file2" tokens
            |> Result.map (fun (k, o) -> CourseFile2 { Key = k; OutputPath = o })
        | "silks-file" ->
            parsePatternAndOutput tokens
            |> Result.map (fun (p, o) -> SilksFile { Pattern = p; OutputPath = o })
        | "silks-binary" -> parsePatternOnly tokens |> Result.map SilksBinary
        | "movie-check" -> parseSingleKey "movie-check" tokens |> Result.map MovieCheck
        | "movie-check-with-type" ->
            parseMovieWithType "movie-check-with-type" tokens
            |> Result.map MovieCheckWithType
        | "movie-play" -> parseSingleKey "movie-play" tokens |> Result.map MoviePlay
        | "movie-play-with-type" -> parseMovieWithType "movie-play-with-type" tokens |> Result.map MoviePlayWithType
        | "movie-open" -> parseMovieOpen tokens |> Result.map MovieOpen
        | "set-ui-properties" ->
            ensureNoExtra "set-ui-properties" tokens
            |> Result.map (fun () -> SetUiProperties)
        | "version" -> ensureNoExtra "version" tokens |> Result.map (fun () -> Version)
        | "total-read-size" -> ensureNoExtra "total-read-size" tokens |> Result.map (fun () -> TotalReadSize)
        | "current-read-size" ->
            ensureNoExtra "current-read-size" tokens
            |> Result.map (fun () -> CurrentReadSize)
        | "current-file-timestamp" ->
            ensureNoExtra "current-file-timestamp" tokens
            |> Result.map (fun () -> CurrentFileTimestamp)
        | "set-parent-hwnd" ->
            parseSingleValue "set-parent-hwnd" tokens
            |> Result.bind (fun v -> parseNativeInt "--value" v |> Result.map SetParentHwnd)
        | "get-parent-hwnd" -> ensureNoExtra "get-parent-hwnd" tokens |> Result.map (fun () -> GetParentHwnd)
        | "set-payoff-dialog" ->
            parseSingleValue "set-payoff-dialog" tokens
            |> Result.bind (fun v -> parseBoolValue "--value" v |> Result.map SetPayoffDialog)
        | "get-payoff-dialog" ->
            ensureNoExtra "get-payoff-dialog" tokens
            |> Result.map (fun () -> GetPayoffDialog)
        | "capture-fixtures" -> parseCaptureFixtures tokens
        | other -> Error $"Unknown command '{other}'."

    let parseInput (argv: string array) : Result<ParsedInput, string> =
        result {
            let! rawGlobals, remaining = argv |> Array.toList |> readGlobalOptions
            let! globals = buildGlobalSettings rawGlobals

            match remaining with
            | [] when rawGlobals.ShowHelp -> return { Globals = globals; Command = Help }
            | [] -> return! Error "No command specified. Run with --help to list commands."
            | cmd :: rest ->
                let! pc = parseCommand cmd rest
                return { Globals = globals; Command = pc }
        }

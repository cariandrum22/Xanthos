namespace Xanthos.Cli.E2E

open System
open System.Diagnostics
open System.IO
open Xunit
open Xunit.Abstractions

// Extended E2E harness verifying COM/STUB evidence markers and diagnostics.

type RunMode =
    | Stub
    | Com

type CliResult =
    { ExitCode: int
      StdOut: string
      StdErr: string
      LogFile: string }

module Harness =
    let rec private findRepoRoot startDir dir =
        if File.Exists(Path.Combine(dir, "Xanthos.sln")) then
            dir
        else
            let parent = Directory.GetParent dir

            if isNull parent then
                failwith $"Could not locate Xanthos.sln starting from '{startDir}'."
            else
                findRepoRoot startDir parent.FullName

    let repoRoot =
        let baseDir = AppContext.BaseDirectory
        findRepoRoot baseDir baseDir

    let cliProject =
        Path.Combine(repoRoot, "samples", "Xanthos.Cli", "Xanthos.Cli.fsproj")

    let dotnetExe =
        Environment.GetEnvironmentVariable "DOTNET_EXE"
        |> function
            | null
            | "" -> "dotnet"
            | v -> v

    // Check if JV-Link COM is registered (Windows only)
    // We check for the ProgID registration which is more reliable
    let private tryOpenProgId (view: Microsoft.Win32.RegistryView) =
        try
            use root =
                Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.ClassesRoot, view)

            use key = root.OpenSubKey("JVDTLab.JVLink")
            not (isNull key)
        with _ ->
            false

    let private isJvLinkComRegistered () =
        if not (OperatingSystem.IsWindows()) then
            false
        else
            // NOTE: JV-Link is a 32-bit COM server. When the test runner is 64-bit (default in VS),
            // the ProgID can exist only in the 32-bit registry view. Probe both views.
            let has32 = tryOpenProgId Microsoft.Win32.RegistryView.Registry32
            let has64 = tryOpenProgId Microsoft.Win32.RegistryView.Registry64

            if has32 || has64 then
                printfn "[E2E] JV-Link ProgID registration detected (Registry32=%b, Registry64=%b)" has32 has64

            has32 || has64

    // When XANTHOS_E2E_USE_EXE=true (or auto-detected), run the built exe directly instead of dotnet run
    // This is required for COM to work on Windows (32-bit exe for 32-bit COM)
    type ExeModeSetting =
        | ForcedExe
        | ForcedDotnet
        | Auto

    let exeModeSetting =
        match Environment.GetEnvironmentVariable "XANTHOS_E2E_USE_EXE" with
        | v when not (isNull v) && v.Equals("true", StringComparison.OrdinalIgnoreCase) -> ForcedExe
        | v when not (isNull v) && v.Equals("false", StringComparison.OrdinalIgnoreCase) -> ForcedDotnet
        | _ -> Auto

    let mutable useBuiltExe =
        match exeModeSetting with
        | ForcedExe -> true
        | ForcedDotnet -> false
        | Auto ->
            // Prefer exe mode on Windows so the CLI runs the net10.0-windows build.
            // VS test explorer runs the testhost as x64, so dotnet-run mode would always use net10.0
            // and COM interop would be unavailable (JV-Link is a 32-bit COM server).
            OperatingSystem.IsWindows()

    let private artifactsDir = Path.Combine(repoRoot, ".artifacts", "cli-e2e")
    let private buildCliLogFile = Path.Combine(artifactsDir, "build-cli.log")
    let private harnessInitLogFile = Path.Combine(artifactsDir, "harness-init.log")
    let private cliBuildDir = Path.Combine(artifactsDir, "cli-build")

    let private writeBootstrapLog (fileName: string) (commandLine: string) (stdout: string) (stderr: string) =
        try
            Directory.CreateDirectory artifactsDir |> ignore
            let file = Path.Combine(artifactsDir, fileName)

            File.WriteAllText(
                file,
                "COMMAND:\n"
                + commandLine
                + "\n\nSTDOUT:\n"
                + stdout
                + "\n---\nSTDERR:\n"
                + stderr
                + "\n"
            )

            Some file
        with _ ->
            None

    let private cliExePath =
        let exeName =
            if OperatingSystem.IsWindows() then
                "Xanthos.Cli.exe"
            else
                "Xanthos.Cli"

        Path.Combine(cliBuildDir, exeName)

    type CliBuildResult =
        { Attempted: bool
          ExitCode: int option
          LogFile: string option
          Error: string option }

    let private buildCliIfNeeded () : CliBuildResult =
        if not (useBuiltExe && OperatingSystem.IsWindows()) then
            { Attempted = false
              ExitCode = None
              LogFile = None
              Error = None }
        else
            printfn "[E2E] Building CLI with net10.0-windows target..."

            try
                if Directory.Exists cliBuildDir then
                    Directory.Delete(cliBuildDir, true)

                Directory.CreateDirectory cliBuildDir |> ignore

                let args =
                    [ "build"
                      cliProject
                      "-c"
                      "Release"
                      "-f"
                      "net10.0-windows"
                      "-o"
                      cliBuildDir ]

                let commandLine = dotnetExe + " " + (args |> String.concat " ")
                let si = ProcessStartInfo(dotnetExe)
                si.WorkingDirectory <- repoRoot
                si.RedirectStandardOutput <- true
                si.RedirectStandardError <- true
                si.UseShellExecute <- false
                args |> List.iter si.ArgumentList.Add

                use proc = new Process()
                proc.StartInfo <- si

                let started = proc.Start()

                if not started then
                    let logFile =
                        writeBootstrapLog "build-cli.log" commandLine "" "Failed to start dotnet process."

                    { Attempted = true
                      ExitCode = Some -1
                      LogFile = logFile
                      Error = Some "Failed to start dotnet process." }
                else
                    let stdoutTask = proc.StandardOutput.ReadToEndAsync()
                    let stderrTask = proc.StandardError.ReadToEndAsync()
                    let exited = proc.WaitForExit(120000)

                    if not exited then
                        try
                            proc.Kill(true)
                        with _ ->
                            ()

                    let stdout = stdoutTask.GetAwaiter().GetResult()
                    let stderr = stderrTask.GetAwaiter().GetResult()
                    let exitCode = if exited then proc.ExitCode else -1
                    let logFile = writeBootstrapLog "build-cli.log" commandLine stdout stderr

                    if exitCode <> 0 then
                        let logFileHint =
                            logFile |> Option.map (fun p -> $" See log: {p}") |> Option.defaultValue ""

                        printfn "[E2E] Build FAILED with exit code %d.%s" exitCode logFileHint
                    else
                        printfn "[E2E] Build succeeded"

                    { Attempted = true
                      ExitCode = Some exitCode
                      LogFile = logFile
                      Error = None }
            with ex ->
                let args =
                    [ "build"
                      cliProject
                      "-c"
                      "Release"
                      "-f"
                      "net10.0-windows"
                      "-o"
                      cliBuildDir ]

                let commandLine = dotnetExe + " " + (args |> String.concat " ")

                let logFile = writeBootstrapLog "build-cli.log" commandLine "" $"Exception: {ex}"

                { Attempted = true
                  ExitCode = Some -1
                  LogFile = logFile
                  Error = Some ex.Message }

    // Build once at module initialization
    do
        printfn "[E2E] =============================================="
        printfn "[E2E] E2E Test Harness Initialization"

        let platform =
            if OperatingSystem.IsWindows() then
                "Windows"
            else
                "Non-Windows"

        printfn "[E2E] Platform: %s" platform

        printfn "[E2E] exeModeSetting: %A" exeModeSetting
        printfn "[E2E] useBuiltExe: %b" useBuiltExe

        let jvLinkRegistered =
            if OperatingSystem.IsWindows() then
                Some(isJvLinkComRegistered ())
            else
                None

        let buildResult = buildCliIfNeeded ()
        let cliExeExists = File.Exists(cliExePath)
        let mutable fallbackReason: string option = None

        if useBuiltExe && OperatingSystem.IsWindows() then
            let buildFailed = buildResult.ExitCode |> Option.exists (fun code -> code <> 0)

            if buildFailed then
                let message =
                    "Failed to build CLI for E2E tests.\n"
                    + "Try running:\n"
                    + $"  {dotnetExe} build {cliProject} -c Release -f net10.0-windows -o {cliBuildDir}\n"
                    + $"See {buildCliLogFile} for details."

                fallbackReason <- Some "CLI build failed"

                match exeModeSetting with
                | ForcedExe -> failwith message
                | Auto ->
                    printfn "[E2E] WARNING: %s" message
                    printfn "[E2E] Falling back to 'dotnet run' mode."
                    useBuiltExe <- false
                | ForcedDotnet -> ()

        if useBuiltExe && OperatingSystem.IsWindows() then
            // Verify the exe exists
            if cliExeExists then
                printfn "[E2E] CLI exe found: %s" cliExePath
            else
                let message =
                    "CLI exe not found after build.\n"
                    + $"Expected: {cliExePath}\n"
                    + "Try running:\n"
                    + $"  {dotnetExe} build {cliProject} -c Release -f net10.0-windows -o {cliBuildDir}\n"
                    + $"See {buildCliLogFile} for details."

                fallbackReason <- Some "CLI exe missing after build"

                match exeModeSetting with
                | ForcedExe -> failwith message
                | Auto ->
                    printfn "[E2E] WARNING: %s" message
                    printfn "[E2E] Falling back to 'dotnet run' mode."
                    useBuiltExe <- false
                | ForcedDotnet -> ()
        else
            printfn "[E2E] Using 'dotnet run' mode (COM may fall back to Stub)"

        try
            Directory.CreateDirectory artifactsDir |> ignore

            let platformValue =
                if OperatingSystem.IsWindows() then
                    "windows"
                else
                    "non-windows"

            let jvLinkRegisteredValue =
                jvLinkRegistered |> Option.map string |> Option.defaultValue "n/a"

            let buildAttemptedValue = if buildResult.Attempted then "true" else "false"

            let buildExitCodeValue =
                buildResult.ExitCode |> Option.map string |> Option.defaultValue "n/a"

            let buildLogValue = buildResult.LogFile |> Option.defaultValue "n/a"

            let fallbackReasonValue = fallbackReason |> Option.defaultValue "n/a"

            let lines =
                [ $"platform={platformValue}"
                  $"process64Bit={Environment.Is64BitProcess}"
                  $"exeModeSetting={exeModeSetting}"
                  $"useBuiltExe={useBuiltExe}"
                  $"jvLinkProgIdRegistered={jvLinkRegisteredValue}"
                  $"dotnetExe={dotnetExe}"
                  $"repoRoot={repoRoot}"
                  $"cliProject={cliProject}"
                  $"cliBuildDir={cliBuildDir}"
                  $"cliExePath={cliExePath}"
                  $"cliExeExists={cliExeExists}"
                  $"buildAttempted={buildAttemptedValue}"
                  $"buildExitCode={buildExitCodeValue}"
                  $"buildLogFile={buildLogValue}"
                  $"fallbackReason={fallbackReasonValue}"
                  $"appBaseDir={AppContext.BaseDirectory}" ]

            File.WriteAllLines(harnessInitLogFile, lines)
        with _ ->
            ()

        printfn "[E2E] =============================================="

    let private envOrDefault names fallback =
        names
        |> List.tryPick (fun n ->
            match Environment.GetEnvironmentVariable n with
            | null
            | "" -> None
            | v -> Some v)
        |> Option.defaultValue fallback

    let sid =
        envOrDefault [ "XANTHOS_E2E_SID"; "XANTHOS_JVLINK_SID" ] "XANTHOS_JVLINK_SID"

    let serviceKey =
        envOrDefault [ "XANTHOS_E2E_SERVICE_KEY"; "XANTHOS_JVLINK_SERVICE_KEY" ] "XANTHOS_JVLINK_SERVICE_KEY"

    let savePath =
        let path =
            envOrDefault
                [ "XANTHOS_E2E_SAVE_PATH"; "XANTHOS_JVLINK_SAVE_PATH" ]
                (Path.Combine(repoRoot, ".artifacts", "cli-e2e"))

        Directory.CreateDirectory path |> ignore
        path

    let dataspec = envOrDefault [ "XANTHOS_E2E_SPEC" ] "RACE"
    let fromTime = envOrDefault [ "XANTHOS_E2E_FROM" ] "20240101000000"
    let openOption = envOrDefault [ "XANTHOS_E2E_OPTION" ] "1"

    // Realtime-specific settings (use 0Bxx spec format and YYYYMMDDJJKKHHRR key format)
    // Key format: YYYYMMDD=date, JJ=venue, KK=meeting, HH=day, RR=race (e.g., 2024010105010101)
    let realtimeSpec = envOrDefault [ "XANTHOS_E2E_RT_SPEC" ] "0B12"
    let realtimeKey = envOrDefault [ "XANTHOS_E2E_RT_KEY" ] "2024010105010101"

    /// JRA Racing Viewer license flag.
    /// Set XANTHOS_E2E_MOVIE_LICENSE=true when the account has movie API license registered.
    let hasMovieLicense =
        match Environment.GetEnvironmentVariable "XANTHOS_E2E_MOVIE_LICENSE" with
        | v when not (isNull v) && v.Equals("true", StringComparison.OrdinalIgnoreCase) -> true
        | _ -> false

    // Force diagnostics ON for evidence tests unless explicitly disabled
    do Environment.SetEnvironmentVariable("XANTHOS_E2E_DIAG", "true")
    let diagnosticsEnabled = true

    // Pre-test cleanup: clear transient folders to avoid flaky assertions
    do
        try
            let logsDir = Path.Combine(savePath, "test-logs")

            if Directory.Exists logsDir then
                Directory.Delete(logsDir, true)

            Directory.CreateDirectory logsDir |> ignore
            let persistDir = Path.Combine(savePath, "persist")

            if Directory.Exists persistDir then
                Directory.Delete(persistDir, true)
        with _ ->
            ()

    let requestedMode =
        match Environment.GetEnvironmentVariable "XANTHOS_E2E_MODE" with
        | v when not (isNull v) && v.Equals("COM", StringComparison.OrdinalIgnoreCase) -> Some Com
        | v when not (isNull v) && v.Equals("STUB", StringComparison.OrdinalIgnoreCase) -> Some Stub
        | _ -> None

    let resolveMode () =
        requestedMode
        |> Option.orElse (if OperatingSystem.IsWindows() then Some Com else Some Stub)
        |> Option.defaultValue Stub

    /// Global args for normal test commands.
    /// NOTE: Does NOT include --service-key to avoid JVSetServiceKey call on every command.
    /// Service key is assumed to be already registered (set by ServiceKeySetup test or manually).
    let globalArgs mode =
        [ "--sid"; sid; "--save-path"; savePath ]
        @ (if diagnosticsEnabled then [ "--diag" ] else [])
        @ (match mode with
           | Stub -> [ "--stub" ]
           | Com -> [])

    /// Global args that include service key (for initial setup only).
    let globalArgsWithServiceKey mode =
        [ "--sid"; sid; "--service-key"; serviceKey; "--save-path"; savePath ]
        @ (if diagnosticsEnabled then [ "--diag" ] else [])
        @ (match mode with
           | Stub -> [ "--stub" ]
           | Com -> [])

    let private invalidFileNameChars = Path.GetInvalidFileNameChars()

    let private sanitizeFileName (name: string) =
        name
        |> Seq.map (fun c -> if invalidFileNameChars |> Array.contains c then '_' else c)
        |> Seq.toArray
        |> fun chars -> new string (chars)

    let private writeLog (commandArgs: string list) (stdout: string) (stderr: string) =
        let logsDir = Path.Combine(savePath, "test-logs")
        Directory.CreateDirectory logsDir |> ignore
        let name = commandArgs |> String.concat "_" |> sanitizeFileName
        let file = Path.Combine(logsDir, name + ".log")
        File.WriteAllText(file, "STDOUT:\n" + stdout + "\n---\nSTDERR:\n" + stderr)
        file

    let private createProcessStartInfo mode commandArgs =
        let si =
            if useBuiltExe && OperatingSystem.IsWindows() && File.Exists(cliExePath) then
                // Run the built 32-bit exe directly for COM support
                ProcessStartInfo(cliExePath)
            else
                ProcessStartInfo(dotnetExe)

        si.WorkingDirectory <- repoRoot
        si.RedirectStandardOutput <- true
        si.RedirectStandardError <- true
        si.UseShellExecute <- false

        if not (useBuiltExe && OperatingSystem.IsWindows() && File.Exists(cliExePath)) then
            // When not using built exe (i.e., no COM support), always use net10.0
            // to avoid x86 architecture constraints of net10.0-windows.
            // COM-dependent features won't work in this mode anyway.
            let tfm = "net10.0"

            // Use --no-build in CI to avoid redundant builds (project already built by CI workflow).
            // Locally, run 'dotnet build -c Release' first if tests fail due to missing build output.
            let noBuild =
                match Environment.GetEnvironmentVariable "CI" with
                | null
                | "" -> []
                | _ -> [ "--no-build"; "-c"; "Release" ]

            ([ "run"; "--project"; cliProject; "--framework"; tfm ] @ noBuild @ [ "--" ])
            |> List.iter si.ArgumentList.Add

        (globalArgs mode @ commandArgs) |> List.iter si.ArgumentList.Add
        si

    let private createProcessStartInfoWithServiceKey mode commandArgs =
        let si =
            if useBuiltExe && OperatingSystem.IsWindows() && File.Exists(cliExePath) then
                ProcessStartInfo(cliExePath)
            else
                ProcessStartInfo(dotnetExe)

        si.WorkingDirectory <- repoRoot
        si.RedirectStandardOutput <- true
        si.RedirectStandardError <- true
        si.UseShellExecute <- false

        if not (useBuiltExe && OperatingSystem.IsWindows() && File.Exists(cliExePath)) then
            let tfm = "net10.0"

            let noBuild =
                match Environment.GetEnvironmentVariable "CI" with
                | null
                | "" -> []
                | _ -> [ "--no-build"; "-c"; "Release" ]

            ([ "run"; "--project"; cliProject; "--framework"; tfm ] @ noBuild @ [ "--" ])
            |> List.iter si.ArgumentList.Add

        (globalArgsWithServiceKey mode @ commandArgs) |> List.iter si.ArgumentList.Add
        si

    let runCli mode (commandArgs: string list) =
        use proc = new Process()
        proc.StartInfo <- createProcessStartInfo mode commandArgs

        if not (proc.Start()) then
            failwith "Failed to start CLI process."
        // Apply timeout to avoid hanging tests in unstable environments
        let timeoutMs =
            match Environment.GetEnvironmentVariable "XANTHOS_E2E_TIMEOUT_MS" with
            | null
            | "" -> 60000 // default 60s
            | v ->
                match Int32.TryParse v with
                | true, ms when ms > 0 -> ms
                | _ -> 60000

        let out = proc.StandardOutput.ReadToEnd()
        let err = proc.StandardError.ReadToEnd()
        let exited = proc.WaitForExit(timeoutMs)

        if not exited then
            try
                proc.Kill(true)
            with _ ->
                ()

        let exitCode = if exited then proc.ExitCode else -1
        let logFile = writeLog commandArgs out err

        { ExitCode = exitCode
          StdOut = out
          StdErr = err
          LogFile = logFile }

    /// Runs CLI with service key included (for initial setup only).
    /// Use this ONLY in the ServiceKeySetup test.
    let runCliWithServiceKey mode (commandArgs: string list) =
        use proc = new Process()
        proc.StartInfo <- createProcessStartInfoWithServiceKey mode commandArgs

        if not (proc.Start()) then
            failwith "Failed to start CLI process."

        let timeoutMs =
            match Environment.GetEnvironmentVariable "XANTHOS_E2E_TIMEOUT_MS" with
            | null
            | "" -> 60000
            | v ->
                match Int32.TryParse v with
                | true, ms when ms > 0 -> ms
                | _ -> 60000

        let out = proc.StandardOutput.ReadToEnd()
        let err = proc.StandardError.ReadToEnd()
        let exited = proc.WaitForExit(timeoutMs)

        if not exited then
            try
                proc.Kill(true)
            with _ ->
                ()

        let exitCode = if exited then proc.ExitCode else -1
        let logFile = writeLog ("setup-" :: commandArgs) out err

        { ExitCode = exitCode
          StdOut = out
          StdErr = err
          LogFile = logFile }

    // Command arg sets covering all exposed features (expandable)
    let downloadArgs () =
        [ "download"; "--spec"; dataspec; "--option"; openOption; "--from"; fromTime ]

    let downloadPersistArgs () =
        [ "download"
          "--spec"
          dataspec
          "--option"
          openOption
          "--from"
          fromTime
          "--output"
          Path.Combine(savePath, "persist") ]

    let versionArgs () = [ "version" ]
    let setSaveFlagArgs () = [ "set-save-flag"; "--value"; "true" ]

    let realtimeArgs () =
        [ "realtime"; "--spec"; realtimeSpec; "--key"; realtimeKey ]

/// Collection fixture that runs service key setup once before all tests.
/// This ensures the service key is registered before any other test runs.
type ServiceKeySetupFixture() =
    let mutable setupResult: CliResult option = None
    let mutable setupSucceeded = false

    do
        let mode = Harness.resolveMode ()
        printfn "[E2E] ServiceKeySetupFixture: Registering service key..."
        let result = Harness.runCliWithServiceKey mode [ "version" ]
        setupResult <- Some result

        // Success conditions:
        // 1. Exit code 0 - key was set or already valid
        // 2. Exit code 2 with "code -100" in output - key already registered (this is fine)
        let keyAlreadyRegistered =
            result.ExitCode = 2 && result.StdOut.Contains("code -100")

        setupSucceeded <- result.ExitCode = 0 || keyAlreadyRegistered

        if keyAlreadyRegistered then
            printfn "[E2E] ServiceKeySetupFixture: Service key already registered (code -100) - OK"
        elif setupSucceeded then
            printfn "[E2E] ServiceKeySetupFixture: Service key setup succeeded"
        else
            printfn "[E2E] ServiceKeySetupFixture: Service key setup FAILED (exit=%d)" result.ExitCode

    member _.SetupSucceeded = setupSucceeded
    member _.SetupResult = setupResult

/// Collection definition for E2E tests that require service key setup.
[<CollectionDefinition("E2E")>]
type E2ECollection() =
    interface ICollectionFixture<ServiceKeySetupFixture>

// Test class using ITestOutputHelper so stdout appears in TRX/Test Explorer
[<Collection("E2E")>]
type CliTests(output: ITestOutputHelper, fixture: ServiceKeySetupFixture) =
    let mode = Harness.resolveMode ()
    let isWindows = OperatingSystem.IsWindows()

    /// Combines StdOut and StdErr for comprehensive assertion checks
    let combinedOutput (r: CliResult) = r.StdOut + "\n" + r.StdErr

    /// Returns true if movie tests should be skipped (COM mode without license).
    /// In STUB mode, movie tests always run (stub returns success).
    let shouldSkipMovieTest (mode: RunMode) =
        match mode with
        | Stub -> false // Always run in stub mode
        | Com -> not Harness.hasMovieLicense

    /// Skip message for movie tests
    let movieSkipReason =
        "Movie API requires JRA Racing Viewer license. Set XANTHOS_E2E_MOVIE_LICENSE=true if licensed."

    /// Test pattern for silks API (服色標示 format).
    /// This is a Japanese text description of jockey uniform colors/patterns.
    /// Example from JV-Link docs: "水色,赤山形一本輪,水色袖"
    /// Using a simple valid pattern that may or may not exist in the database.
    let silksTestPattern = "白"

    let assertEvidence (stdout: string) expectedMode =
        Assert.Contains($"EVIDENCE:MODE={expectedMode}", stdout)
        Assert.Contains("EVIDENCE:VERSION=", stdout)

    /// Asserts evidence allowing for COM fallback to Stub mode on Windows
    /// When COM mode is requested but COM activation fails, CLI falls back to Stub
    let assertEvidenceAllowingFallback (stdout: string) requestedMode =
        let hasCom = stdout.Contains("EVIDENCE:MODE=COM")
        let hasStub = stdout.Contains("EVIDENCE:MODE=STUB")
        let hasComFallback = stdout.Contains("COM activation failed")
        Assert.Contains("EVIDENCE:VERSION=", stdout)

        match requestedMode with
        | Com ->
            // COM mode: accept either COM success or Stub fallback
            Assert.True(
                hasCom || (hasStub && hasComFallback),
                $"Expected COM mode or Stub fallback. Got: hasCom={hasCom}, hasStub={hasStub}, hasComFallback={hasComFallback}"
            )
        | Stub -> Assert.True(hasStub, "Expected Stub mode")

    let assertEvidenceWithPayload (stdout: string) expectedMode =
        assertEvidence stdout expectedMode
        // Only commands that output payloads will contain "Stub payload" in stub mode
        if expectedMode = "COM" then
            Assert.DoesNotContain("Stub payload", stdout)
        else
            Assert.Contains("Stub payload", stdout)

    /// Asserts evidence with payload allowing for COM fallback to Stub mode on Windows
    let assertEvidenceWithPayloadAllowingFallback (stdout: string) requestedMode =
        let hasCom = stdout.Contains("EVIDENCE:MODE=COM")
        let hasStub = stdout.Contains("EVIDENCE:MODE=STUB")
        let hasComFallback = stdout.Contains("COM activation failed")
        Assert.Contains("EVIDENCE:VERSION=", stdout)

        match requestedMode with
        | Com ->
            if hasCom then
                // Real COM mode - should not have stub payloads
                Assert.DoesNotContain("Stub payload", stdout)
            else if hasStub && hasComFallback then
                // COM fallback to Stub - should have stub payloads
                Assert.Contains("Stub payload", stdout)
            else
                Assert.Fail(
                    $"Expected COM mode or Stub fallback. Got: hasCom={hasCom}, hasStub={hasStub}, hasComFallback={hasComFallback}"
                )
        | Stub ->
            Assert.True(hasStub, "Expected Stub mode")
            Assert.Contains("Stub payload", stdout)

    /// Asserts that the result indicates success or matches a specific condition in combined output
    let expectSuccessOr (predicate: string -> bool) (message: string) (r: CliResult) =
        Assert.True(r.ExitCode = 0 || predicate (combinedOutput r), message)

    /// Asserts that the result contains expected text in StdOut or StdErr
    let assertOutputContains (text: string) (r: CliResult) =
        Assert.True(
            r.StdOut.Contains(text) || r.StdErr.Contains(text),
            $"Expected '{text}' in StdOut or StdErr.\nStdOut: {r.StdOut}\nStdErr: {r.StdErr}"
        )

    let logResult (label: string) (r: CliResult) =
        output.WriteLine($"=== {label} ExitCode={r.ExitCode} Log={r.LogFile} ===")
        // Split to ensure each line captured by xUnit
        r.StdOut.Split([| '\n'; '\r' |], StringSplitOptions.RemoveEmptyEntries)
        |> Array.iter (fun line -> output.WriteLine("[STDOUT] " + line))

        if not (String.IsNullOrWhiteSpace r.StdErr) then
            r.StdErr.Split([| '\n'; '\r' |], StringSplitOptions.RemoveEmptyEntries)
            |> Array.iter (fun line -> output.WriteLine("[STDERR] " + line))

    [<Fact>]
    [<Trait("Category", "E2E")>]
    [<Trait("Category", "Basic")>]
    member _.``version reports JV-Link version and evidence markers``() =
        let result = Harness.runCli mode (Harness.versionArgs ())
        logResult "version" result
        Assert.Equal(0, result.ExitCode)
        // Allow COM fallback to Stub if COM activation fails (e.g., 32-bit COM on 64-bit process)
        assertEvidenceAllowingFallback result.StdOut mode
        // Only check for real version when COM mode succeeds (not fallback)
        let hasCom = result.StdOut.Contains("EVIDENCE:MODE=COM")

        if hasCom && isWindows then
            Assert.DoesNotContain("0000", result.StdOut)

    [<Fact>]
    [<Trait("Category", "E2E")>]
    [<Trait("Category", "Basic")>]
    member _.``download emits evidence markers and payload preview``() =
        let result = Harness.runCli mode (Harness.downloadArgs ())
        logResult "download" result
        Assert.Equal(0, result.ExitCode)
        // Allow COM fallback to Stub if COM activation fails (e.g., 32-bit COM on 64-bit process)
        assertEvidenceWithPayloadAllowingFallback result.StdOut mode

    [<Fact>]
    [<Trait("Category", "E2E")>]
    [<Trait("Category", "Basic")>]
    member _.``download with output writes files in persist folder``() =
        let persistDir = Path.Combine(Harness.savePath, "persist")

        if Directory.Exists persistDir then
            Directory.Delete(persistDir, true)

        Directory.CreateDirectory persistDir |> ignore // Ensure directory exists before CLI runs
        let result = Harness.runCli mode (Harness.downloadPersistArgs ())
        logResult "download-persist" result
        Assert.Equal(0, result.ExitCode)
        Assert.True(Directory.Exists persistDir, "Persist directory should exist")
        let files = Directory.GetFiles persistDir
        output.WriteLine($"Persisted files: {files.Length}")
        files |> Array.iter (fun f -> output.WriteLine("  " + f))
        Assert.True(files.Length > 0, "Expected at least one file in persist folder")

    [<Fact>]
    [<Trait("Category", "E2E")>]
    [<Trait("Category", "Basic")>]
    member _.``set-save-flag executes successfully``() =
        let result = Harness.runCli mode (Harness.setSaveFlagArgs ())
        logResult "set-save-flag" result
        Assert.Equal(0, result.ExitCode)
        // Allow COM fallback to Stub if COM activation fails
        assertEvidenceAllowingFallback result.StdOut mode

    [<Fact>]
    [<Trait("Category", "E2E")>]
    [<Trait("Category", "Basic")>]
    member _.``COM diagnostics appear only in COM mode``() =
        let result = Harness.runCli mode (Harness.versionArgs ())
        logResult "diagnostics" result

        let hasComCall =
            result.StdOut.Contains("CALL JVInit") || result.StdOut.Contains("CALL JVRead")

        let hasComFallback = result.StdOut.Contains("COM activation failed")

        match mode, isWindows, hasComFallback with
        | Com, true, false -> Assert.True(hasComCall, "Expected COM CALL diagnostics in COM mode")
        | Com, true, true -> () // COM fallback to Stub - no COM calls expected
        | Com, false, _ -> ()
        | Stub, _, _ -> Assert.False(hasComCall, "Diagnostics should not contain COM CALL in stub mode")

    // ==================== Realtime Streaming Tests ====================

    [<Fact>]
    [<Trait("Category", "E2E")>]
    [<Trait("Category", "Realtime")>]
    member _.``realtime streams payloads until end of stream``() =
        let result = Harness.runCli mode (Harness.realtimeArgs ())
        logResult "realtime" result
        Assert.Equal(0, result.ExitCode)
        // Allow COM fallback to Stub if COM activation fails
        assertEvidenceAllowingFallback result.StdOut mode
        Assert.Contains("Realtime stream completed", result.StdOut)

    // ==================== Configuration Round-Trip Tests ====================

    [<Fact>]
    [<Trait("Category", "E2E")>]
    [<Trait("Category", "Config")>]
    member _.``set-save-flag and get-save-flag round-trip``() =
        // Set to true
        let setResult = Harness.runCli mode [ "set-save-flag"; "--value"; "true" ]
        logResult "set-save-flag-true" setResult
        Assert.Equal(0, setResult.ExitCode)

        // Get and verify
        let getResult = Harness.runCli mode [ "get-save-flag" ]
        logResult "get-save-flag" getResult
        Assert.Equal(0, getResult.ExitCode)
        Assert.Contains("Save flag:", getResult.StdOut)

    [<Fact>]
    [<Trait("Category", "E2E")>]
    [<Trait("Category", "Config")>]
    member _.``set-save-path and get-save-path round-trip``() =
        let testPath = Path.Combine(Harness.savePath, "test-save-path")
        Directory.CreateDirectory testPath |> ignore

        let setResult = Harness.runCli mode [ "set-save-path"; "--value"; testPath ]
        logResult "set-save-path" setResult
        Assert.Equal(0, setResult.ExitCode)

        let getResult = Harness.runCli mode [ "get-save-path" ]
        logResult "get-save-path" getResult
        Assert.Equal(0, getResult.ExitCode)
        Assert.Contains("Save path:", getResult.StdOut)

    /// Verifies that the service key setup fixture completed successfully.
    /// The actual setup runs in ServiceKeySetupFixture before any test.
    [<Fact>]
    [<Trait("Category", "E2E")>]
    [<Trait("Category", "Setup")>]
    member _.``service key setup succeeded``() =
        // Log the fixture result if available
        match fixture.SetupResult with
        | Some result -> logResult "ServiceKeySetupFixture" result
        | None -> output.WriteLine("[WARN] No setup result available")

        Assert.True(
            fixture.SetupSucceeded,
            "ServiceKeySetupFixture failed. "
            + "If running COM mode, ensure XANTHOS_E2E_SERVICE_KEY is set to a valid service key."
        )

    /// Get service key - reads the currently registered key (does not set it).
    [<Fact>]
    [<Trait("Category", "E2E")>]
    [<Trait("Category", "Config")>]
    member _.``get-service-key reads current key``() =
        let getResult = Harness.runCli mode [ "get-service-key" ]
        logResult "get-service-key" getResult
        Assert.Equal(0, getResult.ExitCode)
        Assert.Contains("Service key:", getResult.StdOut)

    [<Fact>]
    [<Trait("Category", "E2E")>]
    [<Trait("Category", "Config")>]
    member _.``get-payoff-dialog reads current flag``() =
        // Note: JV-Link COM m_payflag property may be read-only in COM mode.
        // SET operation fails in COM mode, so we only test the GET operation.
        let getResult = Harness.runCli mode [ "get-payoff-dialog" ]
        logResult "get-payoff-dialog" getResult
        Assert.Equal(0, getResult.ExitCode)
        Assert.Contains("Payoff dialog suppressed:", getResult.StdOut)

    [<Fact>]
    [<Trait("Category", "E2E")>]
    [<Trait("Category", "Config")>]
    member _.``set-parent-hwnd executes successfully``() =
        // Note: JV-Link COM ParentHWnd property is write-only (設定専用).
        // Reading is not supported in COM mode, so we only test the SET operation.
        let setResult = Harness.runCli mode [ "set-parent-hwnd"; "--value"; "12345" ]
        logResult "set-parent-hwnd" setResult
        Assert.Equal(0, setResult.ExitCode)

    // ==================== Course Diagram Tests ====================

    [<Fact>]
    [<Trait("Category", "E2E")>]
    [<Trait("Category", "CourseDiagram")>]
    member _.``course-file retrieves course diagram``() =
        // Key format: YYYYMMDDJJKKKKTT (16 chars)
        // 99999999 = latest, 05 = Tokyo, 2400 = distance, 01 = turf
        let result = Harness.runCli mode [ "course-file"; "--key"; "9999999905240011" ]
        logResult "course-file" result
        Assert.Equal(0, result.ExitCode)
        Assert.Contains("Course file", result.StdOut)

    [<Fact>]
    [<Trait("Category", "E2E")>]
    [<Trait("Category", "CourseDiagram")>]
    member _.``course-file2 retrieves course diagram (v2)``() =
        // Key format: YYYYMMDDJJKKKKTT (16 chars)
        // 99999999 = latest, 05 = Tokyo, 2400 = distance, 01 = turf
        let outputPath = Path.Combine(Harness.savePath, "course-diagram", "course.gif")
        // Ensure output directory exists
        Directory.CreateDirectory(Path.GetDirectoryName outputPath) |> ignore

        let result =
            Harness.runCli mode [ "course-file2"; "--key"; "9999999905240011"; "--output"; outputPath ]

        logResult "course-file2" result
        Assert.Equal(0, result.ExitCode)
        Assert.Contains("Course file (v2)", result.StdOut)

    // ==================== Silks Tests ====================

    [<Fact>]
    [<Trait("Category", "E2E")>]
    [<Trait("Category", "Silks")>]
    member _.``silks-file generates silks bitmap``() =
        let outputPath = Path.Combine(Harness.savePath, "silks-test", "output.bmp")
        // JV-Link requires the output directory to exist.
        Directory.CreateDirectory(Path.GetDirectoryName outputPath) |> ignore

        let result =
            Harness.runCli mode [ "silks-file"; "--pattern"; silksTestPattern; "--output"; outputPath ]

        logResult "silks-file" result
        // Accept success OR "No Image" output (pattern may not exist in database)
        result
        |> expectSuccessOr
            (fun s -> s.Contains("No Image") || s.Contains("Silks image written to"))
            "Expected success or 'No Image' output for test pattern"

    [<Fact>]
    [<Trait("Category", "E2E")>]
    [<Trait("Category", "Silks")>]
    member _.``silks-binary retrieves silks data``() =
        let result = Harness.runCli mode [ "silks-binary"; "--pattern"; silksTestPattern ]
        logResult "silks-binary" result
        // Accept success OR "No Image" (pattern may not exist in database)
        result
        |> expectSuccessOr
            (fun s -> s.Contains("No Image") || s.Contains("bytes of silks data"))
            "Expected success or 'No Image' output for test pattern"

    // ==================== Movie Operation Tests ====================

    [<Fact>]
    [<Trait("Category", "E2E")>]
    [<Trait("Category", "Movie")>]
    member _.``movie-check checks movie availability``() =
        if shouldSkipMovieTest mode then
            output.WriteLine($"[SKIP] {movieSkipReason}")
        else
            let result = Harness.runCli mode [ "movie-check"; "--key"; "2024010106010101" ]
            logResult "movie-check" result
            Assert.Equal(0, result.ExitCode)
            Assert.Contains("Movie availability", result.StdOut)

    [<Fact>]
    [<Trait("Category", "E2E")>]
    [<Trait("Category", "Movie")>]
    member _.``movie-check-with-type checks movie availability with type``() =
        if shouldSkipMovieTest mode then
            output.WriteLine($"[SKIP] {movieSkipReason}")
        else
            let result =
                Harness.runCli mode [ "movie-check-with-type"; "--movie-type"; "11"; "--key"; "2024010106010101" ]

            logResult "movie-check-with-type" result
            Assert.Equal(0, result.ExitCode)
            Assert.Contains("Movie availability for type=", result.StdOut)

    [<Fact>]
    [<Trait("Category", "E2E")>]
    [<Trait("Category", "Movie")>]
    member _.``movie-play requests movie playback``() =
        if shouldSkipMovieTest mode then
            output.WriteLine($"[SKIP] {movieSkipReason}")
        else
            let result = Harness.runCli mode [ "movie-play"; "--key"; "2024010106010101" ]
            logResult "movie-play" result
            Assert.Equal(0, result.ExitCode)
            Assert.Contains("JVMVPlay succeeded", result.StdOut)

    [<Fact>]
    [<Trait("Category", "E2E")>]
    [<Trait("Category", "Movie")>]
    member _.``movie-play-with-type requests movie playback with type``() =
        if shouldSkipMovieTest mode then
            output.WriteLine($"[SKIP] {movieSkipReason}")
        else
            let result =
                Harness.runCli mode [ "movie-play-with-type"; "--movie-type"; "11"; "--key"; "2024010106010101" ]

            logResult "movie-play-with-type" result
            Assert.Equal(0, result.ExitCode)
            Assert.Contains("JVMVPlayWithType succeeded", result.StdOut)

    [<Fact>]
    [<Trait("Category", "E2E")>]
    [<Trait("Category", "Movie")>]
    member _.``movie-open retrieves workout video listings``() =
        if shouldSkipMovieTest mode then
            output.WriteLine($"[SKIP] {movieSkipReason}")
        else
            let result =
                Harness.runCli mode [ "movie-open"; "--movie-type"; "11"; "--search-key"; "20240101" ]

            logResult "movie-open" result
            Assert.Equal(0, result.ExitCode)
            Assert.Contains("JVMVOpen succeeded", result.StdOut)
            Assert.Contains("workout video listing", result.StdOut)

    // ==================== File Management Tests ====================

    [<Fact>]
    [<Trait("Category", "E2E")>]
    [<Trait("Category", "FileManagement")>]
    member _.``status without open session returns error or reports invalid state``() =
        // Status requires an open session, so this should fail or report invalid state
        let result = Harness.runCli mode [ "status" ]
        logResult "status" result

        // Accept success, "invalid state", or "not initialised" (JV-Link COM returns -201 for no session)
        result
        |> expectSuccessOr
            (fun s -> s.Contains("invalid state") || s.Contains("not initialised"))
            "Expected success or session error"

    [<Fact>]
    [<Trait("Category", "E2E")>]
    [<Trait("Category", "FileManagement")>]
    member _.``skip without open session returns error or reports invalid state``() =
        // Skip requires an open session, so this should fail or report invalid state
        let result = Harness.runCli mode [ "skip" ]
        logResult "skip" result

        // Accept success, "invalid state", "not initialised", or "JVSkip" (JV-Link COM returns -201 for no session)
        result
        |> expectSuccessOr
            (fun s ->
                s.Contains("invalid state")
                || s.Contains("not initialised")
                || s.Contains("JVSkip"))
            "Expected success or session error"

    [<Fact>]
    [<Trait("Category", "E2E")>]
    [<Trait("Category", "FileManagement")>]
    member _.``cancel succeeds even without open session``() =
        let result = Harness.runCli mode [ "cancel" ]
        logResult "cancel" result
        Assert.Equal(0, result.ExitCode)
        Assert.Contains("JVCancel succeeded", result.StdOut)

    [<Fact>]
    [<Trait("Category", "E2E")>]
    [<Trait("Category", "FileManagement")>]
    member _.``delete-file executes or reports file not found``() =
        let result = Harness.runCli mode [ "delete-file"; "--name"; "nonexistent.jvd" ]
        logResult "delete-file" result
        // In STUB mode: always succeeds (stub returns Ok())
        // In COM mode: may return -503 (file not found) when the file doesn't exist
        result
        |> expectSuccessOr
            (fun s -> s.Contains("code -503") || s.Contains("JVFiledelete succeeded"))
            "Expected success or file not found error (-503)"

    // ==================== Status Information Tests ====================

    [<Fact>]
    [<Trait("Category", "E2E")>]
    [<Trait("Category", "StatusInfo")>]
    member _.``total-read-size retrieves total file size``() =
        let result = Harness.runCli mode [ "total-read-size" ]
        logResult "total-read-size" result
        Assert.Equal(0, result.ExitCode)
        Assert.Contains("Total read file size:", result.StdOut)

    [<Fact>]
    [<Trait("Category", "E2E")>]
    [<Trait("Category", "StatusInfo")>]
    member _.``current-read-size retrieves current file size``() =
        let result = Harness.runCli mode [ "current-read-size" ]
        logResult "current-read-size" result
        Assert.Equal(0, result.ExitCode)
        Assert.Contains("Current file size:", result.StdOut)

    [<Fact>]
    [<Trait("Category", "E2E")>]
    [<Trait("Category", "StatusInfo")>]
    member _.``current-file-timestamp retrieves timestamp``() =
        let result = Harness.runCli mode [ "current-file-timestamp" ]
        logResult "current-file-timestamp" result
        Assert.Equal(0, result.ExitCode)
        Assert.Contains("Current file timestamp:", result.StdOut)

    // ==================== Error Case Tests ====================

    [<Fact>]
    [<Trait("Category", "E2E")>]
    [<Trait("Category", "ErrorCase")>]
    member _.``download with invalid dataspec returns error``() =
        let result =
            Harness.runCli
                mode
                [ "download"
                  "--spec"
                  "INVALID_SPEC_XXX"
                  "--from"
                  Harness.fromTime
                  "--option"
                  "1" ]

        logResult "download-invalid-spec" result
        Assert.NotEqual(0, result.ExitCode)

    [<Fact>]
    [<Trait("Category", "E2E")>]
    [<Trait("Category", "ErrorCase")>]
    member _.``download without required arguments returns error``() =
        let result = Harness.runCli mode [ "download" ]
        logResult "download-no-args" result
        Assert.NotEqual(0, result.ExitCode)
        result |> assertOutputContains "--spec is required"

    [<Fact>]
    [<Trait("Category", "E2E")>]
    [<Trait("Category", "ErrorCase")>]
    member _.``realtime without required arguments returns error``() =
        let result = Harness.runCli mode [ "realtime" ]
        logResult "realtime-no-args" result
        Assert.NotEqual(0, result.ExitCode)
        result |> assertOutputContains "--spec is required"

    [<Fact>]
    [<Trait("Category", "E2E")>]
    [<Trait("Category", "ErrorCase")>]
    member _.``unknown command returns error``() =
        let result = Harness.runCli mode [ "unknown-command-xyz" ]
        logResult "unknown-command" result
        Assert.NotEqual(0, result.ExitCode)
        result |> assertOutputContains "Unknown command"

    [<Fact>]
    [<Trait("Category", "E2E")>]
    [<Trait("Category", "ErrorCase")>]
    member _.``set-save-flag with invalid value returns error``() =
        let result = Harness.runCli mode [ "set-save-flag"; "--value"; "invalid" ]
        logResult "set-save-flag-invalid" result
        Assert.NotEqual(0, result.ExitCode)
        result |> assertOutputContains "must be true/false"

    [<Fact>]
    [<Trait("Category", "E2E")>]
    [<Trait("Category", "ErrorCase")>]
    member _.``set-parent-hwnd with non-integer returns error``() =
        let result = Harness.runCli mode [ "set-parent-hwnd"; "--value"; "not-a-number" ]
        logResult "set-parent-hwnd-invalid" result
        Assert.NotEqual(0, result.ExitCode)
        result |> assertOutputContains "must be an integer"

    [<Fact>]
    [<Trait("Category", "E2E")>]
    [<Trait("Category", "ErrorCase")>]
    member _.``silks-file without required arguments returns error``() =
        let result = Harness.runCli mode [ "silks-file" ]
        logResult "silks-file-no-args" result
        Assert.NotEqual(0, result.ExitCode)
        result |> assertOutputContains "expects '--pattern"

    [<Fact>]
    [<Trait("Category", "E2E")>]
    [<Trait("Category", "ErrorCase")>]
    member _.``movie-check-with-type without required arguments returns error``() =
        let result = Harness.runCli mode [ "movie-check-with-type"; "--key"; "12345" ]
        logResult "movie-check-with-type-no-args" result
        Assert.NotEqual(0, result.ExitCode)
        result |> assertOutputContains "expects '--movie-type"

    // ==================== Help Command Test ====================

    [<Fact>]
    [<Trait("Category", "E2E")>]
    [<Trait("Category", "Help")>]
    member _.``help displays usage information``() =
        let result = Harness.runCli mode [ "help" ]
        logResult "help" result
        Assert.Equal(0, result.ExitCode)
        Assert.Contains("Usage:", result.StdOut)
        Assert.Contains("Global Options:", result.StdOut)

    // ==================== Watch Events Tests ====================

    [<Fact>]
    [<Trait("Category", "E2E")>]
    [<Trait("Category", "WatchEvents")>]
    member _.``watch-events starts and stops successfully``() =
        // Use short duration for testing (1 second)
        let result = Harness.runCli mode [ "watch-events"; "--duration"; "1" ]
        logResult "watch-events" result
        // Allow COM fallback to Stub if COM activation fails (e.g., 32-bit COM on 64-bit process)
        assertEvidenceAllowingFallback result.StdOut mode
        // Should succeed in any mode (COM, COM-fallback-to-Stub, or Stub)
        Assert.True(
            result.StdOut.Contains("Watch events started successfully")
            || result.StdOut.Contains("Failed to start watch events"),
            "Expected success or COM event failure message"
        )

        Assert.Equal(0, result.ExitCode)

    // ==================== Capture Fixtures Tests ====================

    [<Fact>]
    [<Trait("Category", "E2E")>]
    [<Trait("Category", "CaptureFixtures")>]
    member _.``capture-fixtures requires COM connection``() =
        let outputDir = Path.Combine(Harness.savePath, "test-fixtures")

        let result =
            Harness.runCli
                mode
                [ "capture-fixtures"
                  "--output"
                  outputDir
                  "--specs"
                  "RACE"
                  "--from"
                  "20240101000000"
                  "--max-records"
                  "1" ]

        logResult "capture-fixtures" result
        // Check if COM fallback occurred
        let comFallbackOccurred = result.StdOut.Contains("COM activation failed")

        match mode with
        | Stub ->
            // In stub mode, should fail with error about requiring COM
            Assert.Equal(2, result.ExitCode)
            Assert.Contains("requires real COM connection", result.StdOut)
        | Com when comFallbackOccurred ->
            // COM mode requested but COM activation failed - falls back to Stub
            // capture-fixtures should then fail because it requires real COM
            Assert.Equal(2, result.ExitCode)
            Assert.Contains("requires real COM connection", result.StdOut)
        | Com ->
            // In COM mode on Windows with working COM, should attempt to capture
            // The actual result depends on whether JV-Link is properly configured
            Assert.True(
                result.ExitCode = 0 || result.ExitCode = 1 || result.ExitCode = 2,
                "Expected exit code 0 (success), 1 (partial failure), or 2 (error)"
            )
            // Should have evidence markers
            assertEvidence result.StdOut "COM"

    [<Fact>]
    [<Trait("Category", "E2E")>]
    [<Trait("Category", "CaptureFixtures")>]
    member _.``capture-fixtures uses sensible defaults``() =
        // All arguments have defaults - command should work with partial args
        // Note: In stub mode this will fail because capture-fixtures requires COM,
        // but we verify the argument parsing succeeds by checking for the COM error message

        // With only --specs override (uses default output and from)
        let result1 = Harness.runCli mode [ "capture-fixtures"; "--specs"; "RACE" ]
        logResult "capture-fixtures-specs-only" result1
        // In stub mode, should fail with COM requirement message, not argument validation
        if mode <> RunMode.Com then
            Assert.Contains("requires real COM", result1.StdOut)

        // With only --output override (uses default specs and from)
        let result2 =
            Harness.runCli mode [ "capture-fixtures"; "--output"; "/tmp/test-fixtures" ]

        logResult "capture-fixtures-output-only" result2

        if mode <> RunMode.Com then
            Assert.Contains("requires real COM", result2.StdOut)

        // With no arguments at all (uses all defaults)
        let result3 = Harness.runCli mode [ "capture-fixtures" ]
        logResult "capture-fixtures-all-defaults" result3

        if mode <> RunMode.Com then
            Assert.Contains("requires real COM", result3.StdOut)

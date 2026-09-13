namespace Xanthos.Cli.E2E

open System
open System.IO
open Xunit
open Xunit.Abstractions

[<Trait("Category", "StubX64")>]
type CliTests(output: ITestOutputHelper) =
    let mode = Stub
    let isWindows = OperatingSystem.IsWindows()

    /// Combines StdOut and StdErr for comprehensive assertion checks
    let combinedOutput (r: CliResult) = r.StdOut + "\n" + r.StdErr

    /// Returns true if movie tests should be skipped (COM mode without license).
    /// In STUB mode, movie tests always run (stub returns success).
    let shouldSkipMovieTest (mode: RunMode) =
        match mode with
        | Stub -> false // Always run in stub mode
        | Com -> false

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

    let assertRequestedEvidence (stdout: string) requestedMode =
        let expected = if requestedMode = Com then "COM" else "STUB"
        assertEvidence stdout expected
        Assert.DoesNotContain("COM activation failed", stdout)
        Assert.DoesNotContain("EVIDENCE:MODE=" + (if requestedMode = Com then "STUB" else "COM"), stdout)
        Assert.Contains("EVIDENCE:ARCH=X64", stdout)
        Assert.Contains("EVIDENCE:POINTER_SIZE=8", stdout)

    let assertRequestedPayload stdout requestedMode =
        assertRequestedEvidence stdout requestedMode

        if requestedMode = Stub then
            Assert.Contains("Stub payload", stdout)
        else
            Assert.DoesNotContain("Stub payload", stdout)

    /// Asserts that the result indicates success or matches a specific condition in combined output
    let expectSuccessOr (predicate: string -> bool) (message: string) (r: CliResult) =
        Assert.Equal(0, r.ExitCode)
        Assert.True(predicate (combinedOutput r), message)

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
    member _.``session-check rejects an explicit stub without activating COM``() =
        let result =
            Harness.runCliWithEnvironment
                Stub
                [ "XANTHOS_COM_PROGID", "Xanthos.Unregistered.TestComponent" ]
                [ "session-check"
                  "--spec"
                  "RACE"
                  "--from"
                  Harness.fromTime
                  "--max-records"
                  "1" ]

        output.WriteLine result.StdOut
        Assert.Equal(2, result.ExitCode)
        Assert.Contains("session-check requires explicit COM mode", result.StdOut)
        Assert.Contains("EVIDENCE:ARCH=X64", result.StdOut)
        Assert.DoesNotContain("CALL JVOpen", result.StdOut)
        Assert.DoesNotContain("COM activation", result.StdOut)

    [<Fact; Trait("Category", "E2E"); Trait("Category", "Basic")>]
    member _.``version reports JV-Link version and evidence markers``() =
        let result = Harness.runCli mode (Harness.versionArgs ())
        logResult "version" result
        Assert.Equal(0, result.ExitCode)
        assertRequestedEvidence result.StdOut mode
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
        assertRequestedPayload result.StdOut mode

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
        assertRequestedEvidence result.StdOut mode

    [<Fact>]
    [<Trait("Category", "E2E")>]
    [<Trait("Category", "Basic")>]
    member _.``COM diagnostics appear only in COM mode``() =
        let result = Harness.runCli mode (Harness.versionArgs ())
        logResult "diagnostics" result

        let hasComCall =
            result.StdOut.Contains("CALL JVInit") || result.StdOut.Contains("CALL JVRead")

        Assert.Equal(0, result.ExitCode)
        Assert.False(hasComCall, "STUB mode must not invoke COM.")
    // ==================== Realtime Streaming Tests ====================

    [<Fact>]
    [<Trait("Category", "E2E")>]
    [<Trait("Category", "Realtime")>]
    member _.``realtime streams payloads until end of stream``() =
        let result = Harness.runCli mode (Harness.realtimeArgs ())
        logResult "realtime" result
        Assert.Equal(0, result.ExitCode)
        assertRequestedEvidence result.StdOut mode
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
    member _.``initialization reuses configuration without registering a key``() =
        let result = Harness.runCli mode [ "version" ]
        logResult "existing-configuration" result
        Assert.Equal(0, result.ExitCode)
        Assert.DoesNotContain("CALL JVSetServiceKey", result.StdOut)

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

        Assert.Equal(0, result.ExitCode)
        Assert.Contains("Status: Completed 0 file(s).", result.StdOut)

    [<Fact>]
    [<Trait("Category", "E2E")>]
    [<Trait("Category", "FileManagement")>]
    member _.``skip without open session returns error or reports invalid state``() =
        // Skip requires an open session, so this should fail or report invalid state
        let result = Harness.runCli mode [ "skip" ]
        logResult "skip" result

        Assert.Equal(2, result.ExitCode)
        Assert.Contains("Skip requested before opening a dataspec", result.StdOut)

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
        assertRequestedEvidence result.StdOut mode
        Assert.Contains("Watch events started successfully", result.StdOut)

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
        Assert.Equal(2, result.ExitCode)
        Assert.Contains("requires real COM connection", result.StdOut)

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

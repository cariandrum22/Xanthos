namespace Xanthos.Cli.E2E

open System
open System.Diagnostics
open System.IO
open System.Text

type RunMode =
    | Stub
    | Com

type CliResult =
    { ExitCode: int
      StdOut: string
      StdErr: string
      LogFile: string }

module Harness =
    let rec private findRoot path =
        if File.Exists(Path.Combine(path, "Xanthos.sln")) then
            path
        else
            let parent = Directory.GetParent path

            if isNull parent then
                failwith "Repository root not found."

            findRoot parent.FullName

    let repoRoot = findRoot AppContext.BaseDirectory
    let savePath = Path.Combine(repoRoot, ".artifacts", "cli-e2e")
    let fromTime = "20260905000000"

    /// Successful COM runs must prove the requested backend, functional API and architecture.
    /// Failures before activation may omit backend markers, but can never advertise a fallback.
    let validateEvidence mode (result: CliResult) =
        let markers prefix =
            result.StdOut.Split('\n')
            |> Array.map (fun line -> line.TrimEnd('\r'))
            |> Array.filter (fun line -> line.StartsWith(prefix, StringComparison.Ordinal))
            |> Array.map (fun line -> line.Substring(prefix.Length))

        let reject message =
            failwith $"Invalid CLI evidence: {message}. Log: {result.LogFile}"

        let modes = markers "EVIDENCE:MODE="
        let expectedMode = if mode = Com then "COM" else "STUB"

        if modes |> Array.exists ((<>) expectedMode) then
            reject $"requested {expectedMode}, observed a different backend"

        if mode = Com then
            let require prefix value =
                let values = markers prefix

                if values.Length = 0 || values |> Array.exists ((<>) value) then
                    reject $"expected {prefix}{value}"

            require "EVIDENCE:ARCH=" "X64"
            require "EVIDENCE:POINTER_SIZE=" "8"

            if result.ExitCode = 0 then
                require "EVIDENCE:MODE=" "COM"
                require "EVIDENCE:API=" "FUNCTIONAL"

    let validateExecutable (path: string) =
        if not (Path.IsPathFullyQualified path) || not (File.Exists path) then
            failwith "XANTHOS_E2E_CLI_PATH must identify an existing absolute executable path."

        if OperatingSystem.IsWindows() then
            use stream = File.OpenRead path
            use reader = new BinaryReader(stream)

            if reader.ReadUInt16() <> 0x5a4dus then
                failwith "CLI executable is not a PE file."

            stream.Position <- 0x3cL
            let offset = reader.ReadInt32()
            stream.Position <- int64 offset

            if reader.ReadUInt32() <> 0x4550u || reader.ReadUInt16() <> 0x8664us then
                failwith "The configured CLI executable must have PE Machine AMD64 (x64)."

        path

    let private configuredExecutable () =
        match Environment.GetEnvironmentVariable "XANTHOS_E2E_CLI_PATH" with
        | null
        | "" -> None
        | path -> Some(validateExecutable path)

    let runCliWithEnvironment mode (environment: (string * string) list) (commandArgs: string list) =
        let executable = configuredExecutable ()

        if mode = Com && executable.IsNone then
            failwith "COM tests require XANTHOS_E2E_CLI_PATH pointing at the published x64 Windows CLI."

        let start = ProcessStartInfo()

        match executable with
        | Some path -> start.FileName <- path
        | None ->
            start.FileName <- "dotnet"

            let dll =
                Path.Combine(repoRoot, "samples", "Xanthos.Cli", "bin", "Release", "net10.0", "Xanthos.Cli.dll")

            if not (File.Exists dll) then
                failwith "Build the Release CLI before running E2E tests."

            start.ArgumentList.Add dll

        start.WorkingDirectory <- repoRoot
        start.UseShellExecute <- false
        start.RedirectStandardOutput <- true
        start.RedirectStandardError <- true
        start.StandardOutputEncoding <- UTF8Encoding(false)
        start.StandardErrorEncoding <- UTF8Encoding(false)
        // Reuse COM registration; never register a key or override its path implicitly.
        start.Environment.Remove "XANTHOS_JVLINK_SERVICE_KEY" |> ignore
        start.Environment.Remove "XANTHOS_JVLINK_SAVE_PATH" |> ignore
        start.Environment.Remove "XANTHOS_COM_PROGID" |> ignore

        for key, value in environment do
            start.Environment.[key] <- value

        let globals =
            match mode with
            | Stub -> [ "--stub"; "--sid"; "UNKNOWN"; "--save-path"; savePath; "--diag" ]
            | Com -> [ "--com"; "--sid"; "UNKNOWN"; "--diag" ]

        for arg in globals @ commandArgs do
            start.ArgumentList.Add arg

        use childProcess = new Process(StartInfo = start)

        if not (childProcess.Start()) then
            failwith "CLI childProcess could not start."

        let stdout = childProcess.StandardOutput.ReadToEndAsync()
        let stderr = childProcess.StandardError.ReadToEndAsync()
        // A real SDK dialog waits for the user. STUB tests have no interactive UI.
        let exited = childProcess.WaitForExit(if mode = Com then -1 else 60000)

        if not exited then
            childProcess.Kill(true)

        let output = stdout.GetAwaiter().GetResult()
        let errors = stderr.GetAwaiter().GetResult()
        let logs = Path.Combine(savePath, "test-logs")
        Directory.CreateDirectory logs |> ignore
        let logFile = Path.Combine(logs, Guid.NewGuid().ToString("N") + ".log")

        let metadata =
            $"TEST_POINTER_SIZE={IntPtr.Size}\nCLI_PATH={start.FileName}\nREQUESTED_MODE={mode}\n"

        File.WriteAllText(logFile, metadata + output + "\nSTDERR:\n" + errors, UTF8Encoding(false))

        let result =
            { ExitCode = if exited then childProcess.ExitCode else -1
              StdOut = output
              StdErr = errors
              LogFile = logFile }

        validateEvidence mode result
        result

    let runCli mode args = runCliWithEnvironment mode [] args
    let versionArgs () = [ "version" ]
    let setSaveFlagArgs () = [ "set-save-flag"; "--value"; "true" ]

    let downloadArgs () =
        [ "download"; "--spec"; "RACE"; "--from"; fromTime; "--option"; "1" ]

    let downloadPersistArgs () =
        downloadArgs () @ [ "--output"; Path.Combine(savePath, "persist") ]

    let realtimeArgs () =
        [ "realtime"; "--spec"; "0B12"; "--key"; "2026091205010101" ]

#r "../src/Xanthos/bin/Release/net10.0-windows/Xanthos.dll"

open System
open System.IO
open Xanthos

// Called only by test-settings-roundtrip.ps1 after its exclusive ownership gate.
// Values stay in memory; output contains comparisons only, never user settings.
// JVSetSaveFlag can ask to delete imported data. The operator must decline deletion;
// No need not cancel the setting change. Fresh-session reads below decide the result.
let require =
    function
    | Ok value -> value
    | Error error -> failwithf "%s code=%A" error.Api error.Code

let usingSession action =
    let session = JvLink.connect ConnectionOptions.Default |> require

    try
        JvLink.init "UNKNOWN" session |> require
        action session
    finally
        JvLink.disconnect session |> require

let snapshot () =
    usingSession (fun session -> JvLink.getSaveFlag session |> require, JvLink.getSavePath session |> require)

let equivalentPath (left: string) (right: string) =
    String.Equals(
        Path.TrimEndingDirectorySeparator(Path.GetFullPath left),
        Path.TrimEndingDirectorySeparator(Path.GetFullPath right),
        StringComparison.OrdinalIgnoreCase
    )

let run inject =
    let originalFlag, originalPath = snapshot ()
    let changedFlag = originalFlag = 0

    let directory =
        Path.Combine(Path.GetTempPath(), "Xanthos-Settings-" + Guid.NewGuid().ToString("N"))

    Directory.CreateDirectory directory |> ignore
    let mutable restored = false
    let mutable verified = false
    let mutable injected = false

    try
        try
            usingSession (fun session ->
                JvLink.setSaveFlag changedFlag session |> require
                JvLink.setSavePath directory session |> require)

            let flag, path = snapshot ()

            if flag <> (if changedFlag then 1 else 0) || not (equivalentPath path directory) then
                failwith "Fresh-session changed settings differ."

            verified <- true

            if inject then
                injected <- true
                raise (InvalidOperationException("controlled-after-change"))
        finally
            let errors = ResizeArray<string>()
            // Attempt both restorations even if the first operation fails.
            for restore in
                [ (fun s -> JvLink.setSaveFlag (originalFlag <> 0) s)
                  (fun s -> JvLink.setSavePath originalPath s) ] do
                try
                    usingSession (restore >> require)
                with error ->
                    errors.Add error.Message

            let flag, path = snapshot ()
            restored <- flag = originalFlag && equivalentPath path originalPath

            if errors.Count <> 0 || not restored then
                failwith "SDK settings restoration failed. Human recovery is required."
    with :? InvalidOperationException as error when injected && error.Message = "controlled-after-change" ->
        ()

    if not verified || not restored || (inject && not injected) then
        failwith "Incomplete settings verification."

    printfn "SETTINGS changedMatched=true restoredMatched=true injected=%b" inject

if not (OperatingSystem.IsWindows()) || IntPtr.Size <> 8 then
    failwith "Windows x64 is required."

if Environment.GetEnvironmentVariable "XANTHOS_SETTINGS_EXCLUSIVE" <> "verified" then
    failwith "Use the exclusive PowerShell entry point."

run false
run true

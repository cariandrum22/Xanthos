#r "../src/Xanthos/bin/Release/net10.0-windows/Xanthos.dll"
#load "SettingsRoundtrip.fsx"

open System
open System.IO
open System.Security.Cryptography
open Xanthos
open SettingsRoundtrip

// Called only by test-settings-roundtrip.ps1 after its exclusive ownership gate.
// Values stay in memory; output contains comparisons only, never user settings.
// Do not automate consent. A rejected setter remains a failed success-path test.
let require =
    function
    | Ok value -> value
    | Error error -> failwithf "%s code=%A" error.Api error.Code

let usingSession action =
    JvLink.withSession ConnectionOptions.Default (fun session ->
        JvLink.init "UNKNOWN" session |> Result.map (fun () -> action session))
    |> require

let snapshot () =
    usingSession (fun session ->
        { Flag = JvLink.getSaveFlag session |> require
          Path = JvLink.getSavePath session |> require })

let equivalentPath (left: string) (right: string) =
    String.Equals(
        Path.TrimEndingDirectorySeparator(Path.GetFullPath left),
        Path.TrimEndingDirectorySeparator(Path.GetFullPath right),
        StringComparison.OrdinalIgnoreCase
    )

let rejectLink (path: string) =
    if File.GetAttributes(path).HasFlag FileAttributes.ReparsePoint then
        failwith "Linked paths are not supported by settings verification."

// Read only cache/data. Keep filenames and hashes in memory, never in evidence logs.
let dataSnapshot originalPath =
    let rec scan relative path =
        rejectLink path

        if Directory.Exists path then
            (relative, "directory")
            :: (Directory.GetFileSystemEntries path
                |> Array.toList
                |> List.collect (fun child -> scan (Path.Combine(relative, Path.GetFileName child)) child))
        else
            use stream = File.OpenRead path
            [ relative, Convert.ToHexString(SHA256.HashData stream) ]

    rejectLink originalPath

    [ "cache"; "data" ]
    |> List.collect (fun name ->
        let path = Path.Combine(originalPath, name)

        if Path.Exists path then
            scan name path
        else
            [ name, "absent" ])
    |> Map.ofList

let run inject =
    let original = snapshot ()
    let originalData = dataSnapshot original.Path

    let directory =
        Path.Combine(Path.GetTempPath(), "Xanthos-Settings-" + Guid.NewGuid().ToString("N"))

    Directory.CreateDirectory directory |> ignore

    let rec verifyEmpty path =
        rejectLink path

        for child in Directory.GetFileSystemEntries path do
            if Directory.Exists child then
                verifyEmpty child
            else
                failwith "Verification save path contains a file; refusing flag change."

    let operations =
        { Snapshot = snapshot
          SetPath = fun path -> usingSession (JvLink.setSavePath path >> require)
          SetFlag = fun flag -> usingSession (JvLink.setSaveFlag flag >> require)
          SamePath = equivalentPath
          VerifyIsolated = fun () -> verifyEmpty directory
          VerifyOriginalData =
            fun () ->
                if dataSnapshot original.Path <> originalData then
                    failwith "Original SDK data changed during verification."
          Report = printfn "%s" }

    SettingsRoundtrip.run operations directory inject

if not (OperatingSystem.IsWindows()) || IntPtr.Size <> 8 then
    failwith "Windows x64 is required."

if Environment.GetEnvironmentVariable "XANTHOS_SETTINGS_EXCLUSIVE" <> "verified" then
    failwith "Use the exclusive PowerShell entry point."

let cases =
    match Array.toList fsi.CommandLineArgs[1..] with
    | []
    | [ "All" ] -> [ NoInjection; AfterPath; AfterFlag ]
    | [ "Normal" ] -> [ NoInjection ]
    | [ "AfterPath" ] -> [ AfterPath ]
    | [ "AfterFlag" ] -> [ AfterFlag ]
    | _ -> failwith "Expected All, Normal, AfterPath, or AfterFlag. Settings were not changed."

for injection in cases do
    run injection

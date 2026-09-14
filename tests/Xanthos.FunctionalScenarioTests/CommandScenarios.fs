namespace Xanthos.FunctionalScenarioTests

open System
open System.IO
open Microsoft.FSharp.Reflection
open Xunit
open Xanthos.Cli.Types

module CommandScenarios =
    // One stable Theory ID per Command case; arguments are explicit, not derived
    // from the production parser. Unsupported SDK access directions must fail.
    let private commands =
        [ "Help", [| "--help" |], "", 0
          "Download", [| "download"; "--spec"; "RACE"; "--from"; "20260905000000" |], "JVOpen", 0
          "SessionCheck", [| "session-check"; "--spec"; "RACE"; "--from"; "20260905000000" |], "JVOpen", 0
          "Realtime", [| "realtime"; "--spec"; "0B11"; "--key"; "202609130601" |], "JVRTOpen", 0
          "Status", [| "status" |], "JVStatus", 0
          "Skip", [| "skip" |], "JVSkip", 0
          "Cancel", [| "cancel" |], "JVCancel", 0
          "DeleteFile", [| "delete-file"; "--name"; "synthetic.jvd" |], "JVFiledelete", 0
          "WatchEvents", [| "watch-events"; "--duration"; "0.01" |], "JVWatchEvent", 0
          "SetSaveFlag", [| "set-save-flag"; "--value"; "false" |], "JVSetSaveFlag", 0
          "GetSaveFlag", [| "get-save-flag" |], "m_saveflag", 0
          "SetSavePath", [| "set-save-path"; "--value"; "$OUTPUT" |], "JVSetSavePath", 0
          "GetSavePath", [| "get-save-path" |], "m_savepath", 0
          "SetServiceKey", [| "set-service-key"; "--value"; "SYNTHETIC" |], "JVSetServiceKey", 0
          "GetServiceKey", [| "get-service-key" |], "m_servicekey", 0
          "CourseFile", [| "course-file"; "--key"; "9999999905240011" |], "JVCourseFile", 0
          "CourseFile2", [| "course-file2"; "--key"; "9999999905240011"; "--output"; "$OUTPUT" |], "JVCourseFile2", 0
          "SilksFile", [| "silks-file"; "--pattern"; "synthetic"; "--output"; "$OUTPUT" |], "JVFukuFile", 0
          "SilksBinary", [| "silks-binary"; "--pattern"; "synthetic" |], "JVFuku", 0
          "MovieCheck", [| "movie-check"; "--key"; "202609130601" |], "JVMVCheck", 0
          "MovieCheckWithType",
          [| "movie-check-with-type"; "--movie-type"; "00"; "--key"; "202609130601" |],
          "JVMVCheckWithType",
          0
          "MoviePlay", [| "movie-play"; "--key"; "202609130601" |], "JVMVPlay", 0
          "MoviePlayWithType",
          [| "movie-play-with-type"; "--movie-type"; "00"; "--key"; "202609130601" |],
          "JVMVPlayWithType",
          0
          "MovieOpen", [| "movie-open"; "--movie-type"; "11"; "--search-key"; "20260913" |], "JVMVOpen", 0
          "SetUiProperties", [| "set-ui-properties" |], "JVSetUIProperties", 0
          "Version", [| "version" |], "m_JVLinkVersion", 0
          "TotalReadSize", [| "total-read-size" |], "m_TotalReadFilesize", 0
          "CurrentReadSize", [| "current-read-size" |], "m_CurrentReadFilesize", 0
          "CurrentFileTimestamp", [| "current-file-timestamp" |], "m_CurrentFileTimestamp", 0
          "SetParentHwnd", [| "set-parent-hwnd"; "--value"; "123" |], "ParentHWnd", 0
          "GetParentHwnd", [| "get-parent-hwnd" |], "", 2
          "SetPayoffDialog", [| "set-payoff-dialog"; "--value"; "true" |], "", 2
          "GetPayoffDialog", [| "get-payoff-dialog" |], "m_payflag", 0
          "CaptureFixtures",
          [| "capture-fixtures"
             "--output"
             "$OUTPUT"
             "--specs"
             "RACE"
             "--from"
             "20260905000000"
             "--max-records"
             "1" |],
          "JVOpen",
          0 ]

    let Cases = commands |> Seq.map (fun (name, _, _, _) -> [| box name |])

    [<Fact>]
    let ``Every Command union case has exactly one explicit scenario`` () =
        let actual = commands |> List.map (fun (name, _, _, _) -> name) |> List.sort

        let expected =
            FSharpType.GetUnionCases(typeof<Command>) |> Array.map _.Name |> Array.sort

        Assert.Equal<string>(expected, actual)

    [<Theory; MemberData(nameof Cases)>]
    let ``Production command returns its specified result without COM`` name =
        let _, arguments, api, expected =
            commands |> List.find (fun (id, _, _, _) -> id = name)

        let directory =
            Path.Combine(Path.GetTempPath(), "Xanthos-Command-" + Guid.NewGuid().ToString("N"))

        let native = new NativeFake(SettingsStore())
        let bytes = Host.record "WH"
        let mutable emitted = false

        native.Handler <-
            fun operation args ->
                match operation with
                | "JVOpen" ->
                    emitted <- false
                    None
                | "JVGets" when not emitted ->
                    emitted <- true
                    args[0] <- box bytes
                    args[2] <- box "synthetic.jvd"
                    Some(Ok(box bytes.Length))
                | _ -> None

        try
            let code, output =
                Host.run
                    native
                    (arguments
                     |> Array.map (fun value -> if value = "$OUTPUT" then directory else value))

            Assert.True((code = expected), $"{name}: {output}")

            if name = "Help" then
                Assert.Empty native.Calls
            else
                Assert.Contains("EVIDENCE:MODE=FAKE", output)
                Assert.DoesNotContain("EVIDENCE:MODE=COM", output)

                if api <> "" then
                    Assert.Contains(api, native.Calls)
                else
                    Assert.Contains((if name = "GetParentHwnd" then "write-only" else "read-only"), output)

                Assert.Equal(1, native.Disposals)
        finally
            if Directory.Exists directory then
                Directory.Delete(directory, true)

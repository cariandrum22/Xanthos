namespace Xanthos.FunctionalScenarioTests

open System
open System.IO
open System.Threading
open Xunit
open Xanthos

module ScenarioTests =
    [<Theory>]
    [<InlineData("--use-jvgets")>]
    [<InlineData("--no-jvgets")>]
    let ``S01 actual parser and public API receive exact synthetic bytes`` reader =
        let native = new NativeFake(SettingsStore())
        let bytes = Host.record "WH"
        Host.supplyRecord native bytes

        let directory =
            Path.Combine(Path.GetTempPath(), "Xanthos-Scenario-" + Guid.NewGuid().ToString("N"))

        try
            let code, output =
                Host.run
                    native
                    [| reader
                       "download"
                       "--spec"
                       "RACE"
                       "--from"
                       "20260905000000"
                       "--output"
                       directory |]

            Assert.True((code = 0), output)
            Assert.Contains("EVIDENCE:MODE=FAKE", output)
            Assert.DoesNotContain("MODE=COM", output)
            Assert.Contains("RECORD id=WH", output)
            Assert.Equal<byte>(bytes, File.ReadAllBytes(Path.Combine(directory, "WH_000001.bin")))
            Assert.Equal(1, native.Disposals)
            Assert.Single(native.Calls |> Array.filter ((=) "JVClose")) |> ignore
        finally
            if Directory.Exists directory then
                Directory.Delete(directory, true)

    [<Fact>]
    let ``Q02 failed connection never invents a session or falls back`` () =
        let error =
            { Api = "activation"
              Code = None
              Kind = JvErrorKind.Invocation
              Outputs = Map.empty
              Message = "controlled activation failure" }

        let mutable attempts = 0

        let code, output =
            Host.runWith
                (fun () ->
                    attempts <- attempts + 1
                    Error error)
                CancellationToken.None
                [| "version" |]

        Assert.Equal(2, code)
        Assert.Equal(1, attempts)
        Assert.Contains("controlled activation failure", output)
        Assert.DoesNotContain("MODE=COM", output)

    [<Theory>]
    [<InlineData(false)>]
    [<InlineData(true)>]
    let ``Q02 native error or exception disposes the explicit owner`` throws =
        let native = new NativeFake(SettingsStore())

        native.Handler <-
            fun api _ ->
                if api = "JVStatus" then
                    if throws then
                        failwith "injected native exception"
                    else
                        Some(Ok(box -103))
                else
                    None

        let code, _ = Host.run native [| "status" |]
        Assert.Equal(2, code)
        Assert.Equal(1, native.Disposals)

    [<Fact>]
    let ``Q02 command output exception still releases its session`` () =
        let native = new NativeFake(SettingsStore())
        let mutable raised = false
        let output = ResizeArray<string>()

        let deps =
            Host.dependencies
                (fun () -> Ok(new Session(native)))
                (fun text ->
                    if text.StartsWith("JV-Link version:") && not raised then
                        raised <- true
                        failwith "controlled output failure"

                    output.Add text)
                CancellationToken.None

        let code = Xanthos.Cli.Program.runWith deps [| "--com"; "version" |]
        Assert.Equal(2, code)
        Assert.True raised
        Assert.Equal(1, native.Disposals)

    [<Fact>]
    let ``S16 command and disconnect failures are both observable`` () =
        let native = new NativeFake(SettingsStore())
        native.Handler <- fun api _ -> if api = "JVStatus" then Some(Ok(box -103)) else None

        native.DisposeError <-
            Some
                { Api = "JVClose"
                  Code = Some -201
                  Kind = JvErrorKind.Sdk
                  Outputs = Map.empty
                  Message = "controlled disconnect failure" }

        let code, output = Host.run native [| "status" |]
        Assert.Equal(2, code)
        Assert.Contains("-103", output)
        Assert.Contains("-201", output)
        Assert.Equal(1, native.Disposals)

    let private download =
        [| "download"; "--spec"; "RACE"; "--from"; "20260905000000" |]

    [<Fact>]
    let ``S02 NoData never reads and releases the session`` () =
        let native = new NativeFake(SettingsStore())
        native.Handler <- fun api _ -> if api = "JVOpen" then Some(Ok(box -1)) else None
        let code, output = Host.run native download
        Assert.True((code = 0), output)
        Assert.Contains("NO_DATA", output)
        Assert.DoesNotContain("JVGets", native.Calls)
        Assert.DoesNotContain("JVRead", native.Calls)
        Assert.Equal(1, native.Disposals)

    [<Fact>]
    let ``S03 pending boundary record EOF count only the record`` () =
        let native = new NativeFake(SettingsStore())
        let bytes = Host.record "WH"
        let states = Collections.Generic.Queue<int>([ -3; -1; bytes.Length; 0 ])

        native.Handler <-
            fun api args ->
                if api = "JVGets" then
                    args[0] <- box bytes
                    args[2] <- box "synthetic.jvd"
                    Some(Ok(box (states.Dequeue())))
                else
                    None

        let code, output = Host.run native download
        Assert.True((code = 0), output)
        Assert.Contains("records=1", output)
        Assert.Equal(4, native.Calls |> Array.filter ((=) "JVGets") |> Array.length)
        Assert.Empty states
        Assert.Equal(1, native.Disposals)

    [<Fact>]
    let ``S04 corrupt read retains filename and fresh session recovers`` () =
        let native = new NativeFake(SettingsStore())

        native.Handler <-
            fun api args ->
                if api = "JVGets" then
                    args[2] <- box "corrupt-owned.jvd"
                    Some(Ok(box -402))
                else
                    None

        let code, output = Host.run native download
        Assert.Equal(2, code)
        Assert.Contains("JVGets", output)
        Assert.Contains("-402", output)
        Assert.Contains("corrupt-owned.jvd", output)
        Assert.Equal(1, native.Disposals)
        let fresh = new NativeFake(SettingsStore())
        Host.supplyRecord fresh (Host.record "WH")
        let recovered, text = Host.run fresh download
        Assert.True((recovered = 0), text)

    [<Fact>]
    let ``S05 malformed record reports exact field offset and fails`` () =
        let native = new NativeFake(SettingsStore())
        let bytes = Host.record "WH"
        bytes[11] <- byte 'X'
        Host.supplyRecord native bytes
        let code, output = Host.run native download
        Assert.Equal(2, code)
        Assert.Contains("WH.Identity.Date byte=12 length=8", output)
        Assert.DoesNotContain("RECORD id=", output)
        Assert.Equal(1, native.Disposals)

    [<Theory>]
    [<InlineData(0, false)>]
    [<InlineData(-305, false)>]
    [<InlineData(0, true)>]
    let ``S06 S07 S08 consent gate waits until native return without retry`` nativeCode cancel =
        let native = new NativeFake(SettingsStore())
        use entered = new ManualResetEventSlim(false)
        use decision = new ManualResetEventSlim(false)
        use source = new CancellationTokenSource()

        native.Handler <-
            fun api args ->
                if api = "JVOpen" then
                    entered.Set()
                    decision.Wait()
                    args[3] <- box 0
                    args[4] <- box 0
                    args[5] <- box "20260905000000"
                    Some(Ok(box nativeCode))
                else
                    None

        let operation =
            Tasks.Task.Run(fun () -> Host.runWith (fun () -> Ok(new Session(native))) source.Token download)

        try
            Assert.True(entered.Wait(TimeSpan.FromSeconds 5.), "Native call did not start")

            if cancel then
                source.Cancel()

            Assert.False(operation.IsCompleted, "Native call was interrupted before the explicit decision")
            Assert.Equal(0, native.Disposals)
        finally
            decision.Set()

        Assert.True(operation.Wait(TimeSpan.FromSeconds 5.), "Owned controlled worker did not finish")
        let code, output = operation.Result
        Assert.True((code = (if nativeCode = -305 then 2 else 0)), output)

        if nativeCode = -305 then
            Assert.Contains("JVOpen", output)
            Assert.Contains("-305", output)

        if cancel then
            Assert.Contains("JVCancel", native.Calls)

        Assert.Equal(1, native.Calls |> Array.filter ((=) "JVOpen") |> Array.length)
        Assert.Equal(1, native.Disposals)

    [<Fact>]
    let ``S09 cancellation during read cancels then closes with no next read`` () =
        let native = new NativeFake(SettingsStore())
        use source = new CancellationTokenSource()

        native.Handler <-
            fun api _ ->
                if api = "JVGets" then
                    source.Cancel()
                    Some(Ok(box -3))
                else
                    None

        let code, output =
            Host.runWith (fun () -> Ok(new Session(native))) source.Token download

        Assert.True((code = 0), output)
        let tail = native.Calls |> Array.skipWhile ((<>) "JVGets")
        Assert.Equal<string>([| "JVGets"; "JVCancel"; "JVClose"; "Dispose" |], tail)

    [<Fact>]
    let ``S10 close and reopen use the same session with one close per open`` () =
        let native = new NativeFake(SettingsStore())
        let bytes = Host.record "WH"

        native.Handler <-
            fun api args ->
                if api = "JVGets" then
                    args[0] <- box bytes
                    args[2] <- box "owned.jvd"
                    Some(Ok(box bytes.Length))
                else
                    None

        let code, output =
            Host.run native [| "session-check"; "--spec"; "RACE"; "--from"; "20260905000000" |]

        Assert.True((code = 0), output)
        Assert.Contains("REOPEN succeeded", output)
        Assert.Equal(2, native.Calls |> Array.filter ((=) "JVOpen") |> Array.length)
        Assert.Equal(2, native.Calls |> Array.filter ((=) "JVClose") |> Array.length)
        Assert.Equal(1, native.Disposals)

    [<Theory>]
    [<InlineData(0)>]
    [<InlineData(-305)>]
    let ``S15 configuration cancel success differs from consent refusal`` nativeCode =
        let native = new NativeFake(SettingsStore())

        native.Handler <-
            fun api _ ->
                if api = "JVSetUIProperties" then
                    Some(Ok(box nativeCode))
                else
                    None

        let code, output = Host.run native [| "set-ui-properties" |]
        Assert.Equal((if nativeCode = 0 then 0 else 2), code)

        if nativeCode = -305 then
            Assert.Contains("-305", output)

        Assert.Equal(1, native.Disposals)

    [<Fact>]
    let ``S15 noninteractive UI rejection happens before connection`` () =
        let mutable attempts = 0

        let code, output =
            Host.runWith
                (fun () ->
                    attempts <- attempts + 1
                    failwith "Must not connect")
                CancellationToken.None
                [| "--non-interactive"; "set-ui-properties" |]

        Assert.Equal(2, code)
        Assert.Contains("signed-in desktop", output)
        Assert.Equal(0, attempts)

    [<Fact>]
    let ``S16 cleanup failure preserves its code and still disposes`` () =
        let native = new NativeFake(SettingsStore())
        native.Handler <- fun api _ -> if api = "JVClose" then Some(Ok(box -201)) else None
        let code, output = Host.run native download
        Assert.Equal(2, code)
        Assert.Contains("JVClose", output)
        Assert.Contains("-201", output)
        Assert.Equal(1, native.Disposals)

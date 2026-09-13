namespace Xanthos.FunctionalScenarioTests

open System
open System.Threading
open Xunit
open Xanthos
open Xanthos.Cli

module EventAssetScenarios =
    let private success =
        function
        | Ok value -> value
        | Error error -> failwithf "%A" error

    [<Fact>]
    let ``S12 production callback failure when CLI consumer is blocked is observable`` () =
        let native = new NativeFake(SettingsStore())
        let session = new Session(native)
        use entered = new ManualResetEventSlim(false)
        use release = new ManualResetEventSlim(false)
        use source = new CancellationTokenSource(TimeSpan.FromSeconds 10.)
        let output = ResizeArray<string>()
        native.OnWatch <- fun () -> native.Emit EventKind.Pay "202609130601"

        let writer (text: string) =
            output.Add text

            if text.StartsWith("EVENT origin=") then
                entered.Set()
                release.Wait()

        let deps =
            { Host.dependencies (fun () -> Ok session) writer source.Token with
                EventQueueCapacity = 1 }

        let operation =
            Tasks.Task.Run(fun () -> Program.runWith deps [| "--com"; "watch-events" |])

        try
            Assert.True(entered.Wait(TimeSpan.FromSeconds 5.))
            native.Emit EventKind.Pay "202609130602"
            native.Emit EventKind.Pay "202609130603"
            Assert.True(SpinWait.SpinUntil((fun () -> session.WatchError |> Option.isSome), 5000))
            Assert.Equal("eventCallback", session.WatchError.Value.Api)
        finally
            release.Set()

        Assert.True(operation.Wait(TimeSpan.FromSeconds 5.))
        Assert.Equal(2, operation.Result)
        Assert.Contains("CLI event queue capacity", String.Join("\n", output))
        Assert.Equal(1, native.Disposals)

    [<Fact>]
    let ``S11 all seven notifications retain exact request keys and parse retrieved records`` () =
        let examples =
            [| EventKind.Pay, "202609130601", "0B12", "HR"
               EventKind.Weight, "202609130601", "0B11", "WH"
               EventKind.JockeyChange, "JC20260913060120260913120000", "0B16", "JC"
               EventKind.Weather, "WE20260913060120260913120000", "0B16", "WE"
               EventKind.CourseChange, "CC20260913060120260913120000", "0B16", "CC"
               EventKind.Avoid, "AV20260913060120260913120000", "0B16", "AV"
               EventKind.TimeChange, "TC20260913060120260913120000", "0B16", "TC" |]

        use source = new CancellationTokenSource(TimeSpan.FromSeconds 10.)
        let natives = ResizeArray<NativeFake>()
        let requests = ResizeArray<string * string>()
        let output = ResizeArray<string>()
        let mutable records = 0

        let connect () =
            let native = new NativeFake(SettingsStore())

            if natives.Count = 0 then
                native.OnWatch <-
                    fun () ->
                        for kind, key, _, _ in examples do
                            native.Emit kind key
            else
                let _, _, _, id = examples[natives.Count - 1]
                Host.supplyRecord native (Host.record id)
                let reading = native.Handler

                native.Handler <-
                    fun api args ->
                        if api = "JVRTOpen" then
                            requests.Add(unbox args[0], unbox args[1])

                        reading api args

            natives.Add native
            Ok(new Session(native))

        let write line =
            output.Add line

            if line.StartsWith("RECORD id=", StringComparison.Ordinal) then
                records <- records + 1

                if records = 7 then
                    source.Cancel()

        let deps = Host.dependencies connect write source.Token
        let code = Program.runWith deps [| "--com"; "watch-events"; "--open-after" |]
        Assert.True((code = 0), String.Join("\n", output))
        Assert.Equal(7, records)
        Assert.Equal<(string * string)>(examples |> Array.map (fun (_, key, spec, _) -> spec, key), requests)
        Assert.Equal(8, natives.Count)

        for native in natives do
            Assert.Equal(1, native.Disposals)

        Assert.Single(natives[0].Calls |> Array.filter ((=) "JVWatchEventClose"))
        |> ignore

    [<Theory>]
    [<InlineData(false)>]
    [<InlineData(true)>]
    let ``S12 callback exception or overflow is visible and worker finishes`` overflow =
        let native = new NativeFake(SettingsStore())
        use session = new Session(native)
        use entered = new ManualResetEventSlim(false)
        use release = new ManualResetEventSlim(false)

        let subscription =
            JvLink.subscribeWithOptions
                { SubscriptionOptions.Default with
                    Capacity = 1 }
                (fun _ ->
                    entered.Set()
                    release.Wait()

                    if not overflow then
                        failwith "controlled callback failure")
                session
            |> success

        try
            native.Emit EventKind.Pay "202609130601"
            Assert.True(entered.Wait(TimeSpan.FromSeconds 5.))

            if overflow then
                native.Emit EventKind.Pay "202609130602"
                native.Emit EventKind.Pay "202609130603"
        finally
            release.Set()

        Assert.True(SpinWait.SpinUntil((fun () -> JvLink.subscriptionError subscription |> Option.isSome), 5000))
        let error = JvLink.subscriptionError subscription |> Option.get
        Assert.Equal((if overflow then "eventQueue" else "eventCallback"), error.Api)
        Assert.Equal((if overflow then "202609130603" else "202609130601"), error.Outputs["key"])
        JvLink.unsubscribe subscription |> success
        Assert.Single(native.Calls |> Array.filter ((=) "JVWatchEventClose")) |> ignore

    [<Theory>]
    [<InlineData(0)>]
    [<InlineData(-1)>]
    let ``S13 image outcomes retain full Japanese explanation and byte ownership`` returnCode =
        let native = new NativeFake(SettingsStore())
        let bytes = [| 0uy; 255uy; 66uy; 77uy |]

        native.Handler <-
            fun api args ->
                match api with
                | "JVCourseFile" ->
                    args[1] <- box "合成のコース.gif"
                    args[2] <- box "中央競馬\r\n説明全文"
                    Some(Ok(box returnCode))
                | "JVFuku" ->
                    args[1] <- box bytes
                    Some(Ok(box returnCode))
                | _ -> None

        let code, output = Host.run native [| "course-file"; "--key"; "9999999905240011" |]
        Assert.True((code = 0), output)
        let state = if returnCode = 0 then "Available" else "NoImage"

        Assert.Contains(
            $"Course file [9999999905240011]: State={state} Path=合成のコース.gif Explanation=中央競馬\r\n説明全文",
            output
        )

        let other = new NativeFake(SettingsStore())
        other.Handler <- native.Handler
        use session = new Session(other)
        let result = JvLink.silksBinary "synthetic" session |> success
        Assert.Equal(returnCode, result.ReturnCode)
        Assert.Equal<byte>(bytes, result.Value)
        bytes[0] <- 99uy
        Assert.Equal(0uy, result.Value[0])

    [<Theory>]
    [<InlineData(false)>]
    [<InlineData(true)>]
    let ``S14 movie pending record EOF and NoData retain text sizes and cleanup`` noData =
        let native = new NativeFake(SettingsStore())
        let states = Collections.Generic.Queue<int>([ -3; 5; 0 ])

        native.Handler <-
            fun api args ->
                match api with
                | "JVMVOpen" -> Some(Ok(box (if noData then -1 else 0)))
                | "JVMVRead" ->
                    Assert.Equal(4096, unbox<int> args[1])
                    args[0] <- box "entry"
                    args[1] <- box 21
                    Some(Ok(box (states.Dequeue())))
                | _ -> None

        let code, output =
            Host.run native [| "movie-open"; "--movie-type"; "11"; "--search-key"; "20260913" |]

        Assert.True((code = 0), output)

        if noData then
            Assert.Contains("NO_DATA movie", output)
            Assert.DoesNotContain("JVMVRead", native.Calls)
        else
            Assert.Contains("MOVIE bytes=5 capacity=21 key=entry", output)
            Assert.Empty states

        Assert.Single(native.Calls |> Array.filter ((=) "JVClose")) |> ignore
        Assert.Equal(1, native.Disposals)

    [<Fact>]
    let ``S12 production watch observes subscription overflow and fails`` () =
        let native = new NativeFake(SettingsStore())

        native.OnWatch <-
            fun () ->
                for _ in 1..257 do
                    native.Emit EventKind.Pay "202609130601"

        let code, output = Host.run native [| "watch-events"; "--duration"; "1" |]
        Assert.Equal(2, code)
        Assert.Contains("eventQueue", output)
        Assert.Contains("capacity was exceeded", output)
        Assert.Single(native.Calls |> Array.filter ((=) "JVWatchEventClose")) |> ignore
        Assert.Equal(1, native.Disposals)

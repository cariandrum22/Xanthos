namespace Xanthos.WindowsTests

open System
open System.Collections.Concurrent
open System.Threading
open System.Threading.Tasks
open Xunit
open Xanthos
open Xanthos.Interop
open Xanthos.Runtime

/// Reflection target executes through the production COM adapter and its real STA.
type LifecycleDispatchTarget() =
    let mutable closes = 0
    let mutable watchCloses = 0
    member val OnRead: unit -> unit = ignore with get, set
    member val OnWatchClose: unit -> unit = ignore with get, set
    member val OnWatch: unit -> unit = ignore with get, set
    member val CloseCode = 0 with get, set
    member val WatchCloseCode = 0 with get, set
    member val WatchCode = 0 with get, set
    member val ImageCode = 0 with get, set
    member _.Closes = Volatile.Read(&closes)
    member _.WatchCloses = Volatile.Read(&watchCloses)
    member _.JVInit(_: string) = 0

    member _.JVOpen(_: string, _: string, _: int, read: byref<int>, download: byref<int>, stamp: byref<string>) =
        read <- 1
        download <- 0
        stamp <- "20260912010203"
        0

    member this.JVRead(text: byref<string>, _: int, filename: byref<string>) =
        this.OnRead()
        text <- ""
        filename <- ""
        0

    member this.JVClose() =
        Interlocked.Increment(&closes) |> ignore
        this.CloseCode

    member this.JVWatchEvent() =
        this.OnWatch()
        this.WatchCode

    member this.JVCourseFile(_: string, path: byref<string>, explanation: byref<string>) =
        path <- "controlled-course.png"
        explanation <- "controlled explanation"
        this.ImageCode

    member this.JVCourseFile2(_: string, _: string) = this.ImageCode

    member this.JVWatchEventClose() =
        Interlocked.Increment(&watchCloses) |> ignore
        this.OnWatchClose()
        this.WatchCloseCode

    member _.m_CurrentReadFilesize = 3
    member _.m_TotalReadFilesize = 7
    member _.m_CurrentFileTimestamp = "20260912010203"
    member _.m_JVLinkVersion = "0500-controlled"
    member _.m_savepath = "controlled-path"
    member _.m_servicekey = "controlled-key"

type private OwnerProbe() =
    let mutable releases = 0
    member val Worker: Thread = null with get, set
    member _.Releases = Volatile.Read(&releases)

    member this.Activation(target: LifecycleDispatchTarget) =
        { ComClientActivation.Resolve = fun _ -> typeof<LifecycleDispatchTarget>
          Create =
            fun _ ->
                this.Worker <- Thread.CurrentThread
                box target
          Release =
            fun instance ->
                Assert.Same(target, instance)
                Assert.Equal(this.Worker.ManagedThreadId, Environment.CurrentManagedThreadId)
                Interlocked.Increment(&releases) |> ignore }

module LifecycleWindowsTests =
    let private ok =
        function
        | Ok value -> value
        | Error error -> failwithf "%A" error

    let private wait (signal: ManualResetEventSlim) =
        Assert.True(signal.Wait(5000), "Gate did not open")

    let private config =
        { Sid = "test"
          SavePath = None
          ServiceKey = None
          UseJvGets = Some false }

    let private openRequest: JvOpenRequest =
        { Spec = "RACE"
          FromTime = DateTime(2026, 9, 12)
          Option = 1 }

    [<Theory; InlineData(0); InlineData(-1); InlineData(-100)>]
    let ``Legacy course image adapter preserves absence instead of returning a nonexistent file`` code =
        let target = LifecycleDispatchTarget(ImageCode = code)
        let owner = OwnerProbe()
        use client = new ComJvLinkClient(owner.Activation target)
        let legacy = client :> IJvLinkClient
        let first = legacy.CourseFile "9999999905240011"
        let second = legacy.CourseFile2("9999999905240011", "controlled.png")

        if code = 0 then
            Assert.Equal(Ok("controlled-course.png", "controlled explanation"), first)
            Assert.Equal(Ok(), second)
        elif code = -1 then
            Assert.Equal(Error(Xanthos.Core.InvalidInput "No matching data exists"), first)
            Assert.Equal(Error(Xanthos.Core.InvalidInput "No matching data exists"), second)
        else
            Assert.True(Result.isError first)
            Assert.True(Result.isError second)

    [<Fact>]
    let ``Missing COM registration remains a handled error after forced finalization in a child process`` () =
        let script =
            IO.Path.Combine(IO.Path.GetTempPath(), "Xanthos-Finalizer-" + Guid.NewGuid().ToString("N") + ".fsx")

        let assembly = typeof<ComJvLinkClient>.Assembly.Location.Replace("\"", "\"\"")

        let source =
            "#r @\""
            + assembly
            + "\"\n"
            + "open System\nopen Xanthos.Interop\n"
            + "let failActivation () =\n"
            + "    try\n        use client = new ComJvLinkClient(progId = \"Xanthos.Unregistered.FinalizerRegression\")\n        failwith \"unexpected activation\"\n"
            + "    with ComActivationException _ -> ()\n"
            + "for _ in 1 .. 32 do failActivation ()\n"
            + "GC.Collect()\nGC.WaitForPendingFinalizers()\nGC.Collect()\n"
            + "printfn \"FINALIZATION_COMPLETED\"\n"

        try
            IO.File.WriteAllText(script, source)
            let start = Diagnostics.ProcessStartInfo("dotnet")
            start.UseShellExecute <- false
            start.CreateNoWindow <- true
            start.RedirectStandardOutput <- true
            start.RedirectStandardError <- true

            for argument in [ "fsi"; "--exec"; script ] do
                start.ArgumentList.Add argument

            use child = Diagnostics.Process.Start start
            let output = child.StandardOutput.ReadToEndAsync()
            let errors = child.StandardError.ReadToEndAsync()

            if not (child.WaitForExit 60000) then
                child.Kill(true)
                failwith "Finalization subprocess did not finish"

            Assert.True(child.ExitCode = 0, output.Result + errors.Result)
            Assert.Contains("FINALIZATION_COMPLETED", output.Result)
        finally
            IO.File.Delete script

    let private connector (sink: JvLinkEventSink option ref) remove =
        { ComEventConnector.Connect =
            fun instance callback ->
                sink.Value <- Some callback

                Ok
                    { ComObject = instance
                      SourceIID = Guid.Empty
                      Dispids = [ 1..7 ]
                      Delegates = [ for _ in 1..7 -> Action<string>(ignore) :> Delegate ] }
          Disconnect = ComEventConnection.disconnectWith remove }

    [<Theory; InlineData(1000, 513, 513, 0); InlineData(2, 9, 2, 7)>]
    let ``Legacy notifications use only the service queue with observable overflow`` capacity sent expected dropped =
        let target = LifecycleDispatchTarget()
        let probe = OwnerProbe()
        let sink = ref None

        let client =
            new ComJvLinkClient(probe.Activation target, eventConnector = connector sink (fun _ _ -> ()))

        use service = new JvLinkService(client, config, eventQueueCapacity = capacity)
        use release = new ManualResetEventSlim()
        use received = new CountdownEvent(expected)
        let mutable overflows = 0
        let mutable callbackThread = 0

        use observed =
            service.WatchEvents.Subscribe(fun event ->
                match event with
                | Ok notification ->
                    callbackThread <- Environment.CurrentManagedThreadId
                    Assert.Equal("202609120511", notification.RawKey)

                    if received.CurrentCount = expected then
                        release.Wait()

                    received.Signal() |> ignore
                | Error(Xanthos.Core.EventQueueOverflow count) -> Interlocked.Add(&overflows, count) |> ignore
                | other -> failwithf "Unexpected delivery error: %A" other)

        target.WatchCode <- -1
        target.OnWatch <- fun () -> sink.Value.Value.JVEvtPay "failed-registration"
        Assert.True(service.StartWatchEvents() |> Result.isError)
        target.WatchCode <- 0

        target.OnWatch <-
            fun () ->
                for _ in 1..sent do
                    sink.Value.Value.JVEvtPay "202609120511"

        try
            service.StartWatchEvents() |> ok
        finally
            release.Set()

        Assert.True(received.Wait(5000), "Accepted notifications were lost below the service queue")
        service.StopWatchEvents() |> ok
        Assert.Equal(dropped, overflows)
        Assert.NotEqual(probe.Worker.ManagedThreadId, callbackThread)
        Assert.Equal(1, target.WatchCloses)
        received.Reset expected
        overflows <- 0
        service.StartWatchEvents() |> ok
        Assert.True(received.Wait(5000), "Restart lost notifications received during registration")
        service.StopWatchEvents() |> ok
        Assert.Equal(dropped, overflows)
        Assert.Equal(2, target.WatchCloses)

    [<Fact>]
    let ``Native STA reentry can request disconnect without waiting on its calling thread`` () =
        let target = LifecycleDispatchTarget()
        let probe = OwnerProbe()
        let client = new ComJvLinkClient(probe.Activation target)
        let session = new Session(client :> INativeJvLink)

        target.OnRead <-
            fun () ->
                Assert.Equal(Ok(), JvLink.disconnect session)
                Assert.Equal(0, probe.Releases)

        let read =
            Task.Factory.StartNew((fun () -> JvLink.read session), TaskCreationOptions.LongRunning)

        Assert.True(read.Wait(5000), "Reentrant disconnect blocked the owning STA")
        read.Result |> ok |> ignore
        JvLink.disconnect session |> ok
        Assert.Equal(1, probe.Releases)
        Assert.False(probe.Worker.IsAlive)

    [<Fact>]
    let ``Poisoned legacy service bounds shutdown and defers release to original STA`` () =
        let target = LifecycleDispatchTarget()
        let probe = OwnerProbe()
        use entered = new ManualResetEventSlim()
        use release = new ManualResetEventSlim()

        target.OnRead <-
            fun () ->
                entered.Set()
                release.Wait()

        let client = new ComJvLinkClient(probe.Activation target, useJvGets = false)

        let service =
            new JvLinkService(
                client,
                config,
                retryConfig =
                    { Timeout = TimeSpan.FromMilliseconds 100.
                      MaxRetries = 0 }
            )

        let read = Task.Run(fun () -> service.FetchPayloads openRequest)

        try
            wait entered
            Assert.True(read.Wait(5000))
            Assert.True(Result.isError read.Result)
            Assert.True(service.FetchPayloads openRequest |> Result.isError)
            let dispatcher = (client :> IComDispatchProvider).Dispatcher

            let pending =
                dispatcher.InvokeAsync("pending getter", fun () -> failwith "queued call executed")

            let timer = Diagnostics.Stopwatch.StartNew()
            let shutdown = Task.Run(fun () -> service.Dispose())

            Assert.True(
                shutdown.Wait(6000),
                "Poisoned Dispose exceeded five seconds plus one second scheduling tolerance"
            )

            Assert.True(timer.Elapsed < TimeSpan.FromSeconds 6.)
            Assert.Equal(0, probe.Releases)
            Assert.True(probe.Worker.IsAlive)

            Assert.Throws<ObjectDisposedException>(fun () -> pending.GetAwaiter().GetResult() |> ignore)
            |> ignore
        finally
            release.Set()

        Assert.True(probe.Worker.Join(5000))
        Assert.Equal(1, probe.Releases)
        Assert.Equal(1, target.Closes)
        service.Dispose()

    [<Fact>]
    let ``Normal STA shutdown waits for consent work to finish`` () =
        let dispatcher = new StaThreadDispatcher() :> IComDispatcher
        use entered = new ManualResetEventSlim()
        use release = new ManualResetEventSlim()

        let work =
            dispatcher.InvokeAsync(
                "consent",
                fun () ->
                    entered.Set()
                    release.Wait()
            )

        wait entered
        let stopped = Task.Run(fun () -> dispatcher.Dispose())

        try
            Assert.False(stopped.Wait(100), "Normal consent was interrupted")
        finally
            release.Set()

        Assert.True(Task.WaitAll([| work :> Task; stopped |], 5000))

    [<Theory;
      InlineData("current");
      InlineData("total");
      InlineData("timestamp");
      InlineData("version");
      InlineData("path");
      InlineData("key")>]
    let ``Legacy getters queue behind native read without returning Busy or defaults`` property =
        let target = LifecycleDispatchTarget()
        let probe = OwnerProbe()
        use entered = new ManualResetEventSlim()
        use release = new ManualResetEventSlim()
        use getterStarted = new ManualResetEventSlim()
        use owner = new ComJvLinkClient(probe.Activation target, useJvGets = false)
        let client = owner :> IJvLinkClient

        target.OnRead <-
            fun () ->
                entered.Set()
                release.Wait()

        let read = Task.Run(fun () -> client.Read())
        wait entered

        let getter =
            Task.Run(fun () ->
                getterStarted.Set()

                match property with
                | "current" -> Assert.Equal(Ok 3L, client.TryGetCurrentReadFileSize())
                | "total" -> Assert.Equal(Ok 7L, client.TryGetTotalReadFileSize())
                | "timestamp" ->
                    Assert.Equal(Ok(Some(DateTime(2026, 9, 12, 1, 2, 3))), client.TryGetCurrentFileTimestamp())
                | "version" -> Assert.Equal(Ok "0500-controlled", client.TryGetJVLinkVersion())
                | "path" -> Assert.Equal(Ok "controlled-path", client.TryGetSavePath())
                | _ -> Assert.Equal(Ok "controlled-key", client.TryGetServiceKey()))

        try
            wait getterStarted
            Assert.False(getter.Wait(100), "Getter did not queue behind read")
        finally
            release.Set()

        Assert.True(Task.WaitAll([| read :> Task; getter |], 5000))
        read.Result |> ok |> ignore

    [<Fact>]
    let ``Legacy native reentry returns Busy without deadlocking the STA`` () =
        let target = LifecycleDispatchTarget()
        let probe = OwnerProbe()
        use owner = new ComJvLinkClient(probe.Activation target, useJvGets = false)
        let client = owner :> IJvLinkClient

        target.OnRead <-
            fun () ->
                match client.TryGetJVLinkVersion() with
                | Error(Core.InvalidState _) -> ()
                | other -> failwithf "Expected native reentry rejection: %A" other

        client.Read() |> ok |> ignore

    [<Theory; InlineData(false); InlineData(true)>]
    let ``COM cleanup failure is typed explicitly and preserves body exception implicitly`` explicitCleanup =
        let target = LifecycleDispatchTarget(CloseCode = -503)
        let probe = OwnerProbe()
        let client = new ComJvLinkClient(probe.Activation target)
        (client :> IJvLinkClient).Open openRequest |> ok |> ignore
        let original = InvalidOperationException("original body")

        if explicitCleanup then
            let session = new Session(client :> INativeJvLink)

            match JvLink.disconnect session with
            | Error error ->
                Assert.Equal("JVClose", error.Api)
                Assert.Equal(Some -503, error.Code)
            | Ok() -> failwith "Cleanup failure lost"
        else
            let actual =
                Assert.Throws<InvalidOperationException>(fun () ->
                    use owned = client
                    raise original: unit)

            Assert.Same(original, actual)

        Assert.Equal(1, probe.Releases)
        Assert.False(probe.Worker.IsAlive)

    [<Fact>]
    let ``COM watch close retry skips successful native close and successfully removed DISPIDs`` () =
        let target = LifecycleDispatchTarget(WatchCloseCode = -1)
        let probe = OwnerProbe()
        let sink = ref None
        let attempts = ResizeArray<int>()
        let mutable failRemove = true

        let events =
            connector sink (fun id _ ->
                attempts.Add id

                if failRemove && id = 3 then
                    failwith "remove 3")

        use client = new ComJvLinkClient(probe.Activation target, eventConnector = events)
        use session = new Session(client :> INativeJvLink)
        use delivered = new ManualResetEventSlim()
        use subscription = JvLink.subscribe (fun _ -> delivered.Set()) session |> ok
        Assert.True(JvLink.unsubscribe subscription |> Result.isError)
        sink.Value.Value.JVEvtPay "202609120511"
        wait delivered
        Assert.Empty attempts
        target.WatchCloseCode <- 0
        Assert.True(JvLink.unsubscribe subscription |> Result.isError)
        Assert.Equal(Some "3", (JvLink.subscriptionError subscription).Value.Outputs.TryFind "failedDispids")
        failRemove <- false
        JvLink.unsubscribe subscription |> ok
        Assert.Equal(2, target.WatchCloses) // failed native close plus successful native close
        Assert.Equal<int>([ 1; 2; 3; 4; 5; 6; 7; 3 ], attempts)
        use restarted = JvLink.subscribe ignore session |> ok
        JvLink.unsubscribe restarted |> ok

    [<Fact>]
    let ``COM cancellation followed by disconnect closes detaches releases and exits once`` () =
        let target = LifecycleDispatchTarget()
        let probe = OwnerProbe()
        use entered = new ManualResetEventSlim()
        use release = new ManualResetEventSlim()
        use cancellation = new CancellationTokenSource()
        let removed = ConcurrentQueue<int>()
        let events = connector (ref None) (fun id _ -> removed.Enqueue id)
        let client = new ComJvLinkClient(probe.Activation target, eventConnector = events)
        use session = new Session(client :> INativeJvLink)

        use subscription =
            JvLink.subscribeWithOptions
                { SubscriptionOptions.Default with
                    CancellationToken = cancellation.Token }
                ignore
                session
            |> ok

        target.OnWatchClose <-
            fun () ->
                entered.Set()
                release.Wait()

        cancellation.Cancel()
        wait entered
        let shutdown = Task.Run(fun () -> JvLink.disconnect session)

        try
            Assert.False(shutdown.Wait(100))
        finally
            release.Set()

        Assert.True(shutdown.Wait(5000))
        shutdown.Result |> ok
        Assert.Equal(1, target.WatchCloses)
        Assert.Equal<int>([ 1..7 ], removed)
        Assert.Equal(1, probe.Releases)
        Assert.False(probe.Worker.IsAlive)

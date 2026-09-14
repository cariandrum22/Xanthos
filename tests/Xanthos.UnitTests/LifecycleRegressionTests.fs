namespace Xanthos.UnitTests

open System
open System.Threading
open System.Threading.Tasks
open Xunit
open Xanthos

type private LifecycleNative() =
    let mutable callback = ignore
    let mutable releases = 0
    let mutable stops = 0
    member val OnCall: unit -> unit = ignore with get, set
    member val OnStop: unit -> unit = ignore with get, set
    member val CloseResult: Result<unit, JvError> = Ok() with get, set
    member val CleanupResult: Result<unit, JvError> = Ok() with get, set
    member _.Releases = Volatile.Read(&releases)
    member _.Stops = Volatile.Read(&stops)

    member _.Emit() =
        callback
            { Kind = EventKind.Pay
              RawKey = "202609120511" }

    interface INativeJvLink with
        member this.Invoke(_, _, _) =
            this.OnCall()
            Ok(box 0)

        member _.Get _ = Ok(box "0500")
        member _.Put(_, _) = Ok()

        member _.Watch action =
            callback <- action
            Ok()

        member this.StopWatch() =
            Interlocked.Increment(&stops) |> ignore
            this.OnStop()
            this.CloseResult

        member _.Dispose() =
            Interlocked.Increment(&releases) |> ignore

    interface INativeCleanup with
        member this.Cleanup() =
            Interlocked.Increment(&releases) |> ignore
            this.CleanupResult

module LifecycleRegressionTests =
    let private ok =
        function
        | Ok value -> value
        | Error error -> failwithf "%A" error

    let private wait (signal: ManualResetEventSlim) =
        Assert.True(signal.Wait(5000), "Synchronization gate timed out")

    let private failure api =
        { Api = api
          Code = Some -503
          Kind = JvErrorKind.Sdk
          Outputs = Map.ofList [ "remaining", "3" ]
          Message = "Injected cleanup failure" }

    [<Fact; Trait("Category", "Contract")>]
    let ``Implicit cleanup preserves body exception while explicit disconnect retains typed error`` () =
        let error = failure "JVClose"
        let native = new LifecycleNative(CleanupResult = Error error)
        let original = InvalidOperationException("body-error")

        let actual =
            Assert.Throws<InvalidOperationException>(fun () ->
                use session = new Session(native)
                raise original: unit)

        Assert.Same(original, actual)
        Assert.Equal(1, native.Releases)
        let explicitNative = new LifecycleNative(CleanupResult = Error error)
        let session = new Session(explicitNative)
        Assert.Equal(Error error, JvLink.disconnect session)
        Assert.Equal(1, explicitNative.Releases)
        JvLink.disconnect session |> ok
        Assert.Equal(1, explicitNative.Releases)

    [<Theory; InlineData(false); InlineData(true); Trait("Category", "Contract")>]
    let ``Disconnect joins cancellation owned close and releases native exactly once`` implicitDispose =
        let native = new LifecycleNative()
        use session = new Session(native)
        use entered = new ManualResetEventSlim()
        use release = new ManualResetEventSlim()
        use disconnectStarted = new ManualResetEventSlim()
        use cancellation = new CancellationTokenSource()

        native.OnStop <-
            fun () ->
                entered.Set()
                release.Wait()

        use subscription =
            JvLink.subscribeWithOptions
                { SubscriptionOptions.Default with
                    CancellationToken = cancellation.Token }
                ignore
                session
            |> ok

        cancellation.Cancel()
        wait entered

        let shutdown =
            Task.Run(fun () ->
                disconnectStarted.Set()

                if implicitDispose then
                    (session :> IDisposable).Dispose()
                    Ok()
                else
                    JvLink.disconnect session)

        try
            wait disconnectStarted
            Assert.False(shutdown.Wait(100), "Disconnect returned before owned shutdown completed")
            Assert.Equal(0, native.Releases)
        finally
            release.Set()

        Assert.True(shutdown.Wait(5000))
        shutdown.Result |> ok
        Assert.Equal(1, native.Stops)
        Assert.Equal(1, native.Releases)

    [<Theory; InlineData(false); InlineData(true); Trait("Category", "Contract")>]
    let ``Rejected stop retains event delivery and exposes retryable failure`` nativeCloseFailure =
        let native = new LifecycleNative()
        use session = new Session(native)
        use entered = new ManualResetEventSlim()
        use release = new ManualResetEventSlim()
        use received = new ManualResetEventSlim()
        use subscription = JvLink.subscribe (fun _ -> received.Set()) session |> ok

        let running =
            if nativeCloseFailure then
                native.CloseResult <- Error(failure "JVWatchEventClose")
                Task.FromResult(Ok 0)
            else
                native.OnCall <-
                    fun () ->
                        entered.Set()
                        release.Wait()

                let task = Task.Run(fun () -> JvLink.status session)
                wait entered
                task

        try
            let result = JvLink.unsubscribe subscription
            Assert.True(Result.isError result)
            Assert.True((JvLink.subscriptionError subscription).IsSome)
            native.Emit()
            wait received

            if not nativeCloseFailure then
                Assert.Equal(0, native.Stops)
        finally
            release.Set()

        Assert.True(running.Wait(5000))
        native.CloseResult <- Ok()
        JvLink.unsubscribe subscription |> ok
        Assert.Equal(None, JvLink.subscriptionError subscription)
        use restarted = JvLink.subscribe ignore session |> ok
        Assert.Equal(1 + (if nativeCloseFailure then 1 else 0), native.Stops)

    [<Fact; Trait("Category", "Contract")>]
    let ``Detach retry removes only failed registrations`` () =
        let attempts = ResizeArray<int>()
        let registrations = [ for id in 1..7 -> id, ignore<int> ]

        let remaining, first =
            EventRegistration.detachRemaining
                (fun id _ ->
                    attempts.Add id

                    if id = 3 then
                        failwith "remove failed")
                registrations

        Assert.True(Result.isError first)
        Assert.Equal<int>([ 3 ], remaining |> List.map fst)

        let pending, second =
            EventRegistration.detachRemaining (fun id _ -> attempts.Add id) remaining

        second |> ok
        Assert.Empty pending
        Assert.Equal<int>([ 1; 2; 3; 4; 5; 6; 7; 3 ], attempts)

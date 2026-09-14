namespace Xanthos.Cli.E2E

open System
open System.Reflection
open System.Threading
open System.Threading.Tasks
open Xunit
open Xanthos.Core
open Xanthos.Interop
open Xanthos.Runtime

module private ConsentWorker =
    let start (call: unit -> 'T) =
        Task.Factory.StartNew<'T>(
            Func<'T>(call),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default
        )

/// Controlled SDK boundary for consent policy tests; never counted as real COM evidence.
type ConsentProxy() =
    inherit DispatchProxy()
    let stub = new JvLinkStub() :> IJvLinkClient
    let mutable openCalls = 0
    let mutable disposed = false

    member val OpenHandler: unit -> Result<JvOpenResult, ComError> =
        (fun () -> failwith "Configure consent result") with get, set

    member _.OpenCalls = openCalls
    member _.Disposed = disposed

    override this.Invoke(methodInfo: MethodInfo, args: obj[]) =
        match methodInfo.Name with
        | "Open" ->
            Interlocked.Increment(&openCalls) |> ignore
            box (this.OpenHandler())
        | "Dispose" ->
            disposed <- true
            stub.Dispose()
            null
        | _ -> methodInfo.Invoke(stub, args)

    interface IComDispatchProvider with
        member _.Dispatcher =
            { new IComDispatcher with
                member _.Invoke<'T>(_, call: unit -> 'T) = call ()

                member _.InvokeAsync<'T>(name, call: unit -> 'T) =
                    // Only Open models a blocking native dialog. Complete setup calls
                    // directly so their short timeout does not measure pool scheduling.
                    if name = "JVOpen" then
                        ConsentWorker.start call
                    else
                        Task.FromResult(call ())

                member _.Dispose() = () }

module ConsentTests =
    [<Theory; Trait("Category", "Contract")>]
    [<InlineData(true)>]
    [<InlineData(false)>]
    let ``Consent waits beyond normal timeout then follows the user choice`` (accepted: bool) =
        use entered = new ManualResetEventSlim(false)
        use choice = new ManualResetEventSlim(false)
        let client = DispatchProxy.Create<IJvLinkClient, ConsentProxy>()
        let proxy = box client :?> ConsentProxy

        proxy.OpenHandler <-
            fun () ->
                entered.Set()
                choice.Wait()

                if accepted then
                    Ok
                        { HasData = false
                          ReadCount = 0
                          DownloadCount = 0
                          LastFileTimestamp = None }
                else
                    match ErrorCodes.interpret "JVOpen" -305 with
                    | Error error -> Error error
                    | Ok() -> failwith "-305 must be an error"

        let config =
            JvLinkConfig.create "UNKNOWN" None None None
            |> Result.defaultWith (fun e -> failwithf "%A" e)

        let service =
            new JvLinkService(
                client,
                config,
                retryConfig =
                    { Timeout = TimeSpan.FromMilliseconds 50.
                      MaxRetries = 2 }
            )

        try
            let request =
                { Spec = "RACE"
                  FromTime = DateTime(2026, 9, 5)
                  Option = 1 }

            let operation = ConsentWorker.start (fun () -> service.FetchPayloads request)

            try
                if not (entered.Wait(TimeSpan.FromSeconds 5.)) then
                    if operation.IsCompleted then
                        failwithf "SDK open did not start; operation completed with %A" operation.Result
                    else
                        failwith "SDK open did not start; dedicated operation is still running"

                Assert.False(operation.Wait(TimeSpan.FromMilliseconds 200.), "User consent was interrupted by a timer")
            finally
                choice.Set()

            Assert.True(operation.Wait(TimeSpan.FromSeconds 5.), "Operation did not finish after user choice")

            match operation.Result, accepted with
            | Ok [], true -> ()
            | Error(InteropError(InvalidState message) as error), false ->
                Assert.Contains("-305", message)
                Assert.Contains("JVOpen", message)
                Assert.Equal(2, Xanthos.Cli.Execution.reportError "Download failed" error)
            | result, _ -> failwithf "Unexpected consent result: %A" result

            Assert.Equal(1, proxy.OpenCalls)
        finally
            choice.Set()
            (service :> IDisposable).Dispose()

        Assert.True(proxy.Disposed)

    [<Fact; Trait("Category", "Contract")>]
    let ``Explicit cancellation is retained while the native consent call completes`` () =
        use entered = new ManualResetEventSlim(false)
        use choice = new ManualResetEventSlim(false)
        use cancellation = new CancellationTokenSource()
        let client = DispatchProxy.Create<IJvLinkClient, ConsentProxy>()
        let proxy = box client :?> ConsentProxy

        proxy.OpenHandler <-
            fun () ->
                entered.Set()
                choice.Wait()

                Ok
                    { HasData = true
                      ReadCount = 1
                      DownloadCount = 0
                      LastFileTimestamp = None }

        let config =
            JvLinkConfig.create "UNKNOWN" None None None
            |> Result.defaultWith (fun e -> failwithf "%A" e)

        let service = new JvLinkService(client, config)

        try
            let request =
                { Spec = "RACE"
                  FromTime = DateTime(2026, 9, 5)
                  Option = 1 }

            let operation =
                ConsentWorker.start (fun () -> service.FetchPayloads(request, cancellationToken = cancellation.Token))

            try
                Assert.True(entered.Wait(TimeSpan.FromSeconds 5.))
                cancellation.Cancel()
            finally
                choice.Set()

            Assert.True(operation.Wait(TimeSpan.FromSeconds 5.))
            Assert.Equal(Error Cancelled, operation.Result)
            Assert.Equal(2, Xanthos.Cli.Execution.reportError "Download cancelled" Cancelled)
            Assert.Equal(1, proxy.OpenCalls)
        finally
            choice.Set()
            (service :> IDisposable).Dispose()

        Assert.True(proxy.Disposed)

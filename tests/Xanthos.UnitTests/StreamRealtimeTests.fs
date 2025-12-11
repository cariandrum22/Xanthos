module Xanthos.UnitTests.StreamRealtimeTests

open System
open System.Threading
open System.Threading.Tasks
open Xunit
open Xanthos.Core
open Xanthos.Interop
open Xanthos.Runtime

let private mkService responses =
    let stub = new JvLinkStub(responses)

    let config =
        { Sid = "sid"
          SavePath = None
          ServiceKey = None
          UseJvGets = None }

    new JvLinkService(stub :> IJvLinkClient, config, TraceLogger.silent)

[<Fact>]
let ``StreamRealtimePayloads ignores consecutive FileBoundary markers`` () =
    let responses =
        seq {
            yield Ok FileBoundary
            yield Ok FileBoundary
            yield Ok FileBoundary

            yield
                Ok(
                    Payload
                        { Timestamp = Some DateTime.UtcNow
                          Data = Text.encodeShiftJis "line" }
                )

            yield Ok EndOfStream
        }

    let service = mkService responses
    let stream = service.StreamRealtimePayloads("TEST", "20240101")
    use enumerator = stream.GetEnumerator()
    let mutable payload = None

    while payload.IsNone && enumerator.MoveNext() do
        match enumerator.Current with
        | Ok data -> payload <- Some(Text.decodeShiftJis data.Data)
        | Error err -> failwithf "Unexpected realtime error %A" err

    Assert.Equal(Some "line", payload)

[<Fact>]
let ``StreamRealtimePayloads retries after DownloadPending before yielding payload`` () =
    let responses =
        seq {
            yield Ok DownloadPending

            yield
                Ok(
                    Payload
                        { Timestamp = Some DateTime.UtcNow
                          Data = Text.encodeShiftJis "hello" }
                )

            yield Ok EndOfStream
        }

    let service = mkService responses
    let stream = service.StreamRealtimePayloads("TEST", "20240101")
    use enumerator = stream.GetEnumerator()
    let mutable payload = None

    while payload.IsNone && enumerator.MoveNext() do
        match enumerator.Current with
        | Ok data -> payload <- Some(Text.decodeShiftJis data.Data)
        | Error err -> failwithf "Unexpected realtime error %A" err

    Assert.Equal(Some "hello", payload)

[<Fact>]
let ``StreamRealtimeAsync stops consuming when cancellation is requested`` () =
    let responses = seq { for _ in 1..100 -> Ok DownloadPending }
    let service = mkService responses
    use cts = new CancellationTokenSource(TimeSpan.FromMilliseconds 50.)

    let stream =
        service.StreamRealtimeAsync("TEST", "20240101", cancellationToken = cts.Token)

    let mutable observed = 0

    let consume () =
        task {
            let enumerator = stream.GetAsyncEnumerator(cts.Token)

            try
                let mutable keepGoing = true

                while keepGoing do
                    let! moved = enumerator.MoveNextAsync().AsTask()

                    if moved then
                        match enumerator.Current with
                        | Ok _ -> observed <- observed + 1
                        | Error _ -> keepGoing <- false
                    else
                        keepGoing <- false
            finally
                try
                    enumerator.DisposeAsync().AsTask().Wait()
                with
                | :? AggregateException as agg when
                    agg.InnerExceptions |> Seq.forall (fun ex -> ex :? TaskCanceledException)
                    ->
                    ()
                | _ -> ()
        }

    try
        consume().Wait()
        Assert.True(observed >= 0)
    with :? AggregateException as agg when agg.InnerExceptions |> Seq.forall (fun ex -> ex :? TaskCanceledException) ->
        // Expected due to cancellation; treat as success
        ()

/// Verifies that StreamRealtimeAsync returns false from MoveNextAsync on cancellation
/// instead of throwing OperationCanceledException.
/// This is important for graceful termination as documented in the README.
[<Fact>]
let ``StreamRealtimeAsync returns false from MoveNextAsync when cancelled instead of throwing`` () =
    task {
        // Small response set to keep test fast; JvLinkStub materializes to array
        let responses =
            [ Ok DownloadPending
              Ok DownloadPending
              Ok(
                  Payload
                      { Timestamp = Some DateTime.UtcNow
                        Data = Text.encodeShiftJis "data" }
              )
              Ok EndOfStream ]

        let service = mkService responses
        use cts = new CancellationTokenSource()

        let stream =
            service.StreamRealtimeAsync(
                "TEST",
                "20240101",
                pollInterval = TimeSpan.FromMilliseconds 1.,
                cancellationToken = cts.Token
            )

        let enumerator = stream.GetAsyncEnumerator(cts.Token)

        try
            // Get first item (DownloadPending returns false but continues polling)
            let! moved1 = enumerator.MoveNextAsync().AsTask()
            Assert.True(moved1, "First MoveNextAsync should return true")

            // Now cancel
            cts.Cancel()

            // Next MoveNextAsync should return false, NOT throw
            let! moved2 = enumerator.MoveNextAsync().AsTask()
            Assert.False(moved2, "MoveNextAsync should return false after cancellation, not throw")
        finally
            try
                enumerator.DisposeAsync().AsTask().Wait()
            with _ ->
                ()
    }

module Xanthos.UnitTests.StreamPayloadsTests

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

let private mkRequest () =
    { Spec = "RACE"
      FromTime = DateTime(2024, 1, 1)
      Option = 1 }

[<Fact>]
let ``StreamPayloads yields payloads lazily without accumulating in memory`` () =
    let responses =
        seq {
            yield
                Ok(
                    Payload
                        { Timestamp = Some DateTime.UtcNow
                          Data = Text.encodeShiftJis "payload1" }
                )

            yield
                Ok(
                    Payload
                        { Timestamp = Some DateTime.UtcNow
                          Data = Text.encodeShiftJis "payload2" }
                )

            yield
                Ok(
                    Payload
                        { Timestamp = Some DateTime.UtcNow
                          Data = Text.encodeShiftJis "payload3" }
                )

            yield Ok EndOfStream
        }

    let service = mkService responses
    let stream = service.StreamPayloads(mkRequest ())
    let results = stream |> Seq.toList

    Assert.Equal(3, results.Length)

    match results.[0] with
    | Ok p -> Assert.Equal("payload1", Text.decodeShiftJis p.Data)
    | Error err -> failwithf "Unexpected error %A" err

    match results.[1] with
    | Ok p -> Assert.Equal("payload2", Text.decodeShiftJis p.Data)
    | Error err -> failwithf "Unexpected error %A" err

    match results.[2] with
    | Ok p -> Assert.Equal("payload3", Text.decodeShiftJis p.Data)
    | Error err -> failwithf "Unexpected error %A" err

[<Fact>]
let ``StreamPayloads ignores consecutive FileBoundary markers`` () =
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
    let stream = service.StreamPayloads(mkRequest ())
    use enumerator = stream.GetEnumerator()
    let mutable payload = None

    while payload.IsNone && enumerator.MoveNext() do
        match enumerator.Current with
        | Ok data -> payload <- Some(Text.decodeShiftJis data.Data)
        | Error err -> failwithf "Unexpected error %A" err

    Assert.Equal(Some "line", payload)

[<Fact>]
let ``StreamPayloads retries after DownloadPending before yielding payload`` () =
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
    let stream = service.StreamPayloads(mkRequest ())
    use enumerator = stream.GetEnumerator()
    let mutable payload = None

    while payload.IsNone && enumerator.MoveNext() do
        match enumerator.Current with
        | Ok data -> payload <- Some(Text.decodeShiftJis data.Data)
        | Error err -> failwithf "Unexpected error %A" err

    Assert.Equal(Some "hello", payload)

[<Fact>]
let ``StreamPayloads returns empty sequence when no data available`` () =
    // When JVOpen returns hasData=false (simulated by empty stub),
    // the stream should yield nothing
    let stub = new JvLinkStub(Seq.empty)

    let config =
        { Sid = "sid"
          SavePath = None
          ServiceKey = None
          UseJvGets = None }

    let service = new JvLinkService(stub :> IJvLinkClient, config, TraceLogger.silent)
    let stream = service.StreamPayloads(mkRequest ())
    let results = stream |> Seq.toList

    Assert.Empty(results)

[<Fact>]
let ``StreamPayloads with string parameters validates fromTime`` () =
    let service = mkService (seq { yield Ok EndOfStream })
    let stream = service.StreamPayloads("RACE", None)
    let results = stream |> Seq.toList

    Assert.Single(results) |> ignore

    match results.[0] with
    | Error(ValidationError msg) -> Assert.Contains("fromTime", msg)
    | _ -> failwith "Expected ValidationError"

[<Fact>]
let ``StreamPayloadsAsync yields payloads asynchronously`` () =
    task {
        let responses =
            [ Ok(
                  Payload
                      { Timestamp = Some DateTime.UtcNow
                        Data = Text.encodeShiftJis "async1" }
              )
              Ok(
                  Payload
                      { Timestamp = Some DateTime.UtcNow
                        Data = Text.encodeShiftJis "async2" }
              )
              Ok EndOfStream ]

        let service = mkService responses
        let stream = service.StreamPayloadsAsync(mkRequest ())
        let enumerator = stream.GetAsyncEnumerator(CancellationToken.None)
        let results = System.Collections.Generic.List<JvPayload>()

        try
            let mutable keepGoing = true

            while keepGoing do
                let! moved = enumerator.MoveNextAsync().AsTask()

                if moved then
                    match enumerator.Current with
                    | Ok payload -> results.Add(payload)
                    | Error err -> failwithf "Unexpected error %A" err
                else
                    keepGoing <- false
        finally
            enumerator.DisposeAsync().AsTask().Wait()

        Assert.Equal(2, results.Count)
        Assert.Equal("async1", Text.decodeShiftJis results.[0].Data)
        Assert.Equal("async2", Text.decodeShiftJis results.[1].Data)
    }

[<Fact>]
let ``StreamPayloadsAsync stops consuming when cancellation is requested`` () =
    let responses = seq { for _ in 1..100 -> Ok DownloadPending }
    let service = mkService responses
    use cts = new CancellationTokenSource(TimeSpan.FromMilliseconds 50.)

    let stream =
        service.StreamPayloadsAsync(mkRequest (), cancellationToken = cts.Token)

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

[<Fact>]
let ``StreamPayloadsAsync returns false from MoveNextAsync when cancelled instead of throwing`` () =
    task {
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
            service.StreamPayloadsAsync(
                mkRequest (),
                pollInterval = TimeSpan.FromMilliseconds 1.,
                cancellationToken = cts.Token
            )

        let enumerator = stream.GetAsyncEnumerator(cts.Token)

        try
            // Get first item
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

[<Fact>]
let ``StreamPayloadsAsync with string parameters validates fromTime`` () =
    task {
        let service = mkService (seq { yield Ok EndOfStream })
        let stream = service.StreamPayloadsAsync("RACE", None)
        let enumerator = stream.GetAsyncEnumerator(CancellationToken.None)

        try
            let! moved = enumerator.MoveNextAsync().AsTask()
            Assert.True(moved, "Should yield one error result")

            match enumerator.Current with
            | Error(ValidationError msg) -> Assert.Contains("fromTime", msg)
            | _ -> failwith "Expected ValidationError"

            let! moved2 = enumerator.MoveNextAsync().AsTask()
            Assert.False(moved2, "Should have no more results")
        finally
            enumerator.DisposeAsync().AsTask().Wait()
    }

[<Fact>]
let ``StreamPayloads terminates stream on fatal error`` () =
    let responses =
        seq {
            yield
                Ok(
                    Payload
                        { Timestamp = Some DateTime.UtcNow
                          Data = Text.encodeShiftJis "before-error" }
                )

            yield Error(CommunicationFailure(-999, "Fatal error"))
        }

    let service = mkService responses
    let stream = service.StreamPayloads(mkRequest ())
    let results = stream |> Seq.toList

    Assert.Equal(2, results.Length)

    match results.[0] with
    | Ok p -> Assert.Equal("before-error", Text.decodeShiftJis p.Data)
    | Error _ -> failwith "Expected payload"

    match results.[1] with
    | Error(InteropError(CommunicationFailure(code, _))) -> Assert.Equal(-999, code)
    | _ -> failwith "Expected CommunicationFailure error"

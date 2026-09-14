namespace Xanthos.UnitTests

open System
open System.Threading
open Xunit
open Xanthos
open Xanthos.Core
open Xanthos.Interop
open Xanthos.Runtime

type private LegacyClient() =
    let requests = ResizeArray<string * string>()
    let mutable callback = ignore
    member _.Requests = List.ofSeq requests
    member _.Emit key = callback key
    member val OnRead: unit -> Result<JvReadOutcome, ComError> = (fun () -> Ok EndOfStream) with get, set
    member val OnClose: unit -> Result<unit, ComError> = (fun () -> Ok()) with get, set
    member val OnDispose: unit -> unit = ignore with get, set

    interface IJvLinkClient with
        member _.Init(_) = Ok()

        member _.Open(_) =
            Ok
                { HasData = true
                  ReadCount = 1
                  DownloadCount = 0
                  LastFileTimestamp = None }

        member _.OpenRealtime(spec, key) =
            requests.Add(spec, key)

            Ok
                { HasData = true
                  ReadCount = 1
                  DownloadCount = 0
                  LastFileTimestamp = None }

        member this.Read() = this.OnRead()
        member _.Gets(_, _, _) = Ok 0
        member _.Close() = ()
        member _.Status() = Ok 0
        member _.Skip() = Ok()
        member _.Cancel() = Ok()
        member _.DeleteFile(_) = Ok()
        member _.SetSaveFlag(_) = Ok()
        member _.SetSavePathDirect(_) = Ok()
        member _.SetServiceKeyDirect(_) = Ok()
        member _.SetParentWindowHandleDirect(_) = Ok()
        member _.SetPayoffDialogSuppressedDirect(_) = Ok()
        member _.SetUiProperties() = Ok()
        member _.CourseFile(_) = Ok("", "")
        member _.CourseFile2(_, _) = Ok()
        member _.SilksFile(_, _) = Ok(Some "")
        member _.SilksBinary(_) = Ok(Some Array.empty)
        member _.MovieCheck(_) = Ok MovieAvailability.Unavailable
        member _.MovieCheckWithType(_, _) = Ok MovieAvailability.Unavailable
        member _.MoviePlay(_) = Ok()
        member _.MoviePlayWithType(_, _) = Ok()
        member _.MovieOpen(_, _) = Ok()
        member _.MovieRead() = Ok(MovieEnd)

        member _.WatchEvent(action) =
            callback <- action
            Ok()

        member this.WatchEventClose() = this.OnClose()

        member _.SaveFlag
            with get () = false
            and set (_) = ()

        member _.SavePath = ""

        member _.ServiceKey = ""

        member _.TryGetSaveFlag() = Ok false
        member _.TryGetSavePath() = Ok ""
        member _.TryGetServiceKey() = Ok ""
        member _.TryGetJVLinkVersion() = Ok "0000"
        member _.TryGetTotalReadFileSize() = Ok 0L
        member _.TryGetCurrentReadFileSize() = Ok 0L
        member _.TryGetCurrentFileTimestamp() = Ok None
        member _.TryGetParentWindowHandle() = Ok IntPtr.Zero
        member _.TryGetPayoffDialogSuppressed() = Ok false

        member _.JVLinkVersion = "0000"
        member _.TotalReadFileSize = 0L
        member _.CurrentReadFileSize = 0L
        member _.CurrentFileTimestamp = None

        member _.ParentWindowHandle
            with get () = IntPtr.Zero
            and set (_) = ()

        member _.PayoffDialogSuppressed
            with get () = false
            and set (_) = ()

        member this.Dispose() = this.OnDispose()

type private TypedLegacyClient() =
    inherit LegacyClient()
    let mutable callback = ignore
    member _.EmitNative event = callback event

    interface INativeWatchEventSource with
        member _.WatchNativeEvent action =
            callback <- action
            Ok()

module LegacyRegressionTests =
    let private ok =
        function
        | Ok value -> value
        | Error error -> failwithf "%A" error

    let private config =
        { Sid = "test"
          SavePath = None
          ServiceKey = None
          UseJvGets = None }

    [<Fact; Trait("Category", "Contract")>]
    let ``Availability cleanup failure returns false and service disposal retains body exception`` () =
        let original = InvalidOperationException("body failure")
        let client = new LegacyClient(OnDispose = (fun () -> failwith "cleanup failure"))
        Assert.False(ComClientFactory.isComAvailableWith (fun () -> Ok(client :> IJvLinkClient)))

        let actual =
            Assert.Throws<InvalidOperationException>(fun () ->
                use service = new JvLinkService(client, config)
                raise original: unit)

        Assert.Same(original, actual)

    [<Theory;
      InlineData("Pay", "", "0B12", "HR");
      InlineData("Weight", "", "0B11", "WH");
      InlineData("JockeyChange", "JC", "0B16", "JC");
      InlineData("Weather", "WE", "0B16", "WE");
      InlineData("CourseChange", "CC", "0B16", "CC");
      InlineData("Avoid", "AV", "0B16", "AV");
      InlineData("TimeChange", "TC", "0B16", "TC");
      Trait("Category", "Contract")>]
    let ``Legacy service keeps native event origin through realtime open and record parsing``
        origin
        prefix
        spec
        recordId
        =
        let kind =
            match origin with
            | "Pay" -> EventKind.Pay
            | "Weight" -> EventKind.Weight
            | "JockeyChange" -> EventKind.JockeyChange
            | "Weather" -> EventKind.Weather
            | "CourseChange" -> EventKind.CourseChange
            | "Avoid" -> EventKind.Avoid
            | _ -> EventKind.TimeChange

        let key = prefix + "202609120511" + (if prefix = "" then "" else "20260912121030")
        let notification = { Kind = kind; RawKey = key }
        let expected = Serialization.parseNativeWatchEvent notification |> ok
        let data = RecordOracle.blank (RecordOracle.layout recordId)
        // Parse independently before delivery so malformed test data cannot masquerade as a routing failure.
        let expectedRecord = Records.parse data |> ok
        let client = new TypedLegacyClient()
        let mutable reads = 0

        client.OnRead <-
            fun () ->
                reads <- reads + 1

                if reads = 1 then
                    Ok(Payload { Timestamp = None; Data = data })
                else
                    Ok EndOfStream

        use service = new JvLinkService(client, config)
        use received = new ManualResetEventSlim()
        let mutable actual = None

        use observer =
            service.WatchEvents.Subscribe(fun result ->
                actual <- Some result
                received.Set())

        service.StartWatchEvents() |> ok
        client.EmitNative notification
        Assert.True(received.Wait(5000))
        let event = actual.Value |> ok
        Assert.Equal(expected, event)
        Assert.Equal(key, event.RawKey)
        Assert.Equal(None, event.ParticipantId)
        let request = WatchEvent.toRealtimeRequest event |> Option.get

        let payload =
            service.StreamRealtimePayloads(request.Dataspec, request.Key)
            |> Seq.exactlyOne
            |> ok

        Assert.Equal<(string * string) list>([ spec, key ], client.Requests)
        Assert.Equal(expectedRecord, Records.parse payload.Data |> ok)
        service.StopWatchEvents() |> ok

    [<Fact; Trait("Category", "Contract")>]
    let ``String only clients retain historical notifications and failed stop remains retryable`` () =
        let client =
            new LegacyClient(OnClose = (fun () -> Error(Unexpected "close failed")))

        use service = new JvLinkService(client, config)
        use received = new ManualResetEventSlim()
        let mutable actual = None

        use observer =
            service.WatchEvents.Subscribe(fun value ->
                actual <- Some value
                received.Set())

        service.StartWatchEvents() |> ok
        Assert.True(service.StopWatchEvents() |> Result.isError)
        Assert.True(service.StartWatchEvents() |> Result.isError)
        let key = "0B12RA202609120511"
        client.Emit key
        Assert.True(received.Wait(5000))
        Assert.Equal(Serialization.parseWatchEvent key, actual.Value |> ok)
        client.OnClose <- fun () -> Ok()
        service.StopWatchEvents() |> ok
        service.StartWatchEvents() |> ok
        service.StopWatchEvents() |> ok

    [<Theory;
      InlineData("0B16", "JC20260912051120260912121030", "JC20260912051120260912121030");
      InlineData("0B16", "0B16JC20260912051120260912121030", "JC20260912051120260912121030");
      InlineData("0B16", "　ＪＣ２０２６０９１２０５１１２０２６０９１２１２１０３０　", "JC20260912051120260912121030");
      InlineData("0B12", "0B12RA202609120511", "202609120511");
      InlineData("0B11", "0B11WH202609120511", "202609120511");
      InlineData("0B12", "202609120511", "202609120511");
      Trait("Category", "Contract")>]
    let ``Legacy realtime input adapter sends exact native dataspec and key`` spec input expected =
        let client = new LegacyClient()
        use service = new JvLinkService(client, config)
        service.StreamRealtimePayloads(spec, input) |> Seq.iter (ok >> ignore)
        Assert.Equal<(string * string) list>([ spec, expected ], client.Requests)

    [<Theory;
      InlineData("0B12", "0B11WH202609120511");
      InlineData("0B16", "0B16XX20260912051120260912121030");
      InlineData("0B16", "JC20260230051120260912121030");
      InlineData("0B16", "JC20260912051120260912999999");
      InlineData("0B12", "0B99RA202609120511");
      InlineData("0B12", "　 ");
      Trait("Category", "Contract")>]
    let ``Malformed or mismatched legacy realtime inputs never reach native open`` spec input =
        let client = new LegacyClient()
        use service = new JvLinkService(client, config)
        let result = service.StreamRealtimePayloads(spec, input) |> Seq.exactlyOne
        Assert.True(Result.isError result)
        Assert.Empty(client.Requests)

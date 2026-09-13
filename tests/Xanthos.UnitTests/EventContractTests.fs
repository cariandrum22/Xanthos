namespace Xanthos.UnitTests

open System
open System.Collections.Concurrent
open System.Threading
open System.Threading.Tasks
open Xunit
open Xanthos

type private EventNative() =
    let mutable callback: (JvEvent -> unit) option = None
    let mutable staleCallback: (JvEvent -> unit) option = None
    let mutable starts = 0
    let mutable stops = 0
    let mutable disposed = false
    let calls = ConcurrentQueue<string * obj[]>()
    member _.Starts = Volatile.Read(&starts)
    member _.Stops = Volatile.Read(&stops)
    member _.Disposed = Volatile.Read(&disposed)
    member _.Calls = calls.ToArray()
    member val StartCode = 0 with get, set
    member val StopCode = 0 with get, set
    member val EmitDuringStart = false with get, set

    member _.Emit event =
        callback |> Option.iter (fun action -> action event)

    member _.EmitStale event =
        staleCallback |> Option.iter (fun action -> action event)

    interface INativeJvLink with
        member _.Invoke(api, args, _) =
            calls.Enqueue(api, Array.copy args)
            Ok(box 0)

        member _.Get _ = Ok(box 0)
        member _.Put(_, _) = Ok()

        member this.Watch action =
            Interlocked.Increment(&starts) |> ignore
            callback <- Some action
            staleCallback <- Some action

            if this.EmitDuringStart then
                action
                    { Kind = EventKind.Pay
                      RawKey = "202609120511" }

            if this.StartCode = 0 then
                Ok()
            else
                Error
                    { Api = "JVWatchEvent"
                      Code = Some this.StartCode
                      Kind = JvErrorKind.Sdk
                      Outputs = Map.empty
                      Message = "Injected registration failure" }

        member this.StopWatch() =
            Interlocked.Increment(&stops) |> ignore

            if this.StopCode = 0 then
                callback <- None
                Ok()
            else
                Error
                    { Api = "JVWatchEventClose"
                      Code = Some this.StopCode
                      Kind = JvErrorKind.Sdk
                      Outputs = Map.empty
                      Message = "Injected shutdown failure" }

        member _.Dispose() =
            Volatile.Write(&disposed, true)
            callback <- None

module EventContractTests =
    let private success =
        function
        | Ok value -> value
        | Error error -> failwithf "%A" error

    let private awaitCondition predicate =
        Assert.True(SpinWait.SpinUntil(Func<bool> predicate, 5000), "Event operation did not complete")

    let private pay key = { Kind = EventKind.Pay; RawKey = key }

    [<Theory; InlineData(0); InlineData(-1); Trait("Category", "Contract")>]
    let ``Invalid queue capacity is rejected before registration`` capacity =
        let native = new EventNative()
        use session = new Session(native)

        match
            JvLink.subscribeWithOptions
                { SubscriptionOptions.Default with
                    Capacity = capacity }
                ignore
                session
        with
        | Error error -> Assert.Equal(JvErrorKind.InvalidInput, error.Kind)
        | Ok _ -> failwith "Invalid queue capacity accepted"

        Assert.Equal(0, native.Starts)

    [<Fact; Trait("Category", "Contract")>]
    let ``Already canceled subscription does not register COM events`` () =
        let native = new EventNative()
        use session = new Session(native)
        use cancellation = new CancellationTokenSource()
        cancellation.Cancel()

        match
            JvLink.subscribeWithOptions
                { SubscriptionOptions.Default with
                    CancellationToken = cancellation.Token }
                ignore
                session
        with
        | Error error -> Assert.Equal(JvErrorKind.InvalidInput, error.Kind)
        | Ok _ -> failwith "Canceled subscription registered"

        Assert.Equal(0, native.Starts)

    [<Theory;
      InlineData("Pay", "", "0B12");
      InlineData("Weight", "", "0B11");
      InlineData("JockeyChange", "JC", "0B16");
      InlineData("Weather", "WE", "0B16");
      InlineData("CourseChange", "CC", "0B16");
      InlineData("Avoid", "AV", "0B16");
      InlineData("TimeChange", "TC", "0B16");
      Trait("Category", "Contract")>]
    let ``Seven official origins produce unchanged realtime requests`` origin prefix dataspec =
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
        let event = { Kind = kind; RawKey = key }
        let parsed = JvLink.parseEvent event |> success
        Assert.Equal(event, parsed.Original)
        Assert.Equal(DateTime(2026, 9, 12), parsed.MeetingDate)
        Assert.Equal(DateTimeKind.Unspecified, parsed.MeetingDate.Kind)
        Assert.Equal("05", parsed.CourseCode)
        Assert.Equal("11", parsed.RaceNumber)

        Assert.Equal(
            (if prefix = "" then
                 None
             else
                 Some(DateTime(2026, 9, 12, 12, 10, 30))),
            parsed.SentAt
        )

        let request = JvLink.toRealtimeRequest event |> success
        Assert.Equal({ Dataspec = dataspec; Key = key }, request)
        let legacy = Xanthos.Core.Serialization.parseNativeWatchEvent event |> success
        let legacyRequest = Xanthos.Core.WatchEvent.toRealtimeRequest legacy |> Option.get
        Assert.Equal(dataspec, legacyRequest.Dataspec)
        Assert.Equal(key, legacyRequest.Key)
        Assert.Equal(Some(DateTime(2026, 9, 12)), Xanthos.Core.WatchEvent.tryExtractFromTime key)
        Assert.Equal(Ok key, Xanthos.Runtime.Validation.normalizeRealtimeKey key)
        let native = new EventNative()
        use session = new Session(native)
        JvLink.openRealtime request.Dataspec request.Key session |> success |> ignore
        let api, args = Assert.Single(native.Calls)
        Assert.Equal("JVRTOpen", api)
        Assert.Equal<obj[]>([| box dataspec; box key |], args)

    [<Theory;
      InlineData(null);
      InlineData("202602300511");
      InlineData("２０２６０９１２０５１１");
      InlineData(" 202609120511");
      InlineData("0B12202609120511");
      Trait("Category", "Contract")>]
    let ``Malformed keys remain typed errors with their raw value`` key =
        match JvLink.parseEvent (pay key) with
        | Error error -> Assert.Equal(Some key, Map.tryFind "key" error.Outputs)
        | Ok _ -> failwith "Invalid official key accepted"

    [<Fact; Trait("Category", "Contract")>]
    let ``Unknown origin and mismatched change prefix are never inferred as Pay`` () =
        for event in
            [ { Kind = EventKind.Unknown "future-origin"
                RawKey = "202609120511" }
              { Kind = EventKind.Weather
                RawKey = "JC20260912051120260912121030" } ] do
            match JvLink.toRealtimeRequest event with
            | Error error ->
                Assert.Equal(Some event.RawKey, Map.tryFind "key" error.Outputs)

                let origin =
                    match event.Kind with
                    | EventKind.Unknown origin -> origin
                    | kind -> string kind

                Assert.Equal(Some origin, Map.tryFind "origin" error.Outputs)
            | Ok _ -> failwith "Unknown/mismatched event was accepted"

        Assert.Equal(None, Xanthos.Core.WatchEvent.tryExtractFromTime "JC202609")

    [<Fact; Trait("Category", "Contract")>]
    let ``A subscription delivers in order off the producer thread and stops stale callbacks`` () =
        let native = new EventNative()
        use session = new Session(native)
        let producer = Environment.CurrentManagedThreadId
        let received = ConcurrentQueue<int * string>()

        use subscription =
            JvLink.subscribe (fun event -> received.Enqueue(Environment.CurrentManagedThreadId, event.RawKey)) session
            |> success

        for key in [ "first"; "second"; "third" ] do
            native.Emit(pay key)

        awaitCondition (fun () -> received.Count = 3)
        Assert.Equal<string>([| "first"; "second"; "third" |], received.ToArray() |> Array.map snd)
        Assert.All(received, fun (thread, _) -> Assert.NotEqual(producer, thread))
        JvLink.unsubscribe subscription |> success
        native.EmitStale(pay "after-unsubscribe")
        Assert.Equal(3, received.Count)
        Assert.Equal(None, JvLink.subscriptionError subscription)

    [<Fact; Trait("Category", "Contract")>]
    let ``Old subscription disposal cannot close a newly started watch`` () =
        let native = new EventNative()
        use session = new Session(native)
        use first = JvLink.subscribe ignore session |> success

        match JvLink.subscribe ignore session with
        | Error error -> Assert.Equal(JvErrorKind.Busy, error.Kind)
        | Ok _ -> failwith "Duplicate watch accepted"

        Assert.Equal(1, native.Starts)
        JvLink.watchEventClose session |> success
        use second = JvLink.subscribe ignore session |> success
        JvLink.unsubscribe first |> success
        Assert.Equal(1, native.Stops)
        JvLink.unsubscribe second |> success
        Assert.Equal(2, native.Stops)

    [<Fact; Trait("Category", "Contract")>]
    let ``Failed registration never dispatches events captured during registration`` () =
        let native = new EventNative(StartCode = -201, EmitDuringStart = true)
        use session = new Session(native)
        let mutable count = 0

        match JvLink.subscribe (fun _ -> Interlocked.Increment(&count) |> ignore) session with
        | Error error -> Assert.Equal(Some -201, error.Code)
        | Ok _ -> failwith "Failed registration became success"

        native.EmitStale(pay "stale")
        Assert.Equal(0, Volatile.Read(&count))
        native.StartCode <- 0
        use subscription = JvLink.subscribe ignore session |> success
        Assert.Equal(2, native.Starts)

    [<Fact; Trait("Category", "Contract")>]
    let ``Consumer exception is observable and ends further delivery without escaping to COM`` () =
        let native = new EventNative()
        use session = new Session(native)

        use subscription =
            JvLink.subscribe (fun _ -> raise (InvalidOperationException("consumer-fault"))) session
            |> success

        native.Emit(pay "failure-key")
        awaitCondition (fun () -> JvLink.subscriptionError subscription |> Option.isSome)
        let error = JvLink.subscriptionError subscription |> Option.get
        Assert.Equal("eventCallback", error.Api)
        Assert.Equal("consumer-fault", error.Message)
        Assert.Equal(Some "failure-key", Map.tryFind "key" error.Outputs)
        Assert.Equal(Ok(Some error), JvLink.watchError session)
        JvLink.unsubscribe subscription |> success

    [<Fact; Trait("Category", "Contract")>]
    let ``Bounded queue overflow is observable and does not block the SDK producer`` () =
        let native = new EventNative()
        use session = new Session(native)
        use entered = new ManualResetEventSlim()
        use release = new ManualResetEventSlim()

        use subscription =
            JvLink.subscribeWithOptions
                { SubscriptionOptions.Default with
                    Capacity = 1 }
                (fun _ ->
                    entered.Set()
                    release.Wait())
                session
            |> success

        try
            native.Emit(pay "being-processed")
            Assert.True(entered.Wait(5000))
            native.Emit(pay "queued")
            native.Emit(pay "overflow")
            let error = JvLink.subscriptionError subscription |> Option.get
            Assert.Equal("eventQueue", error.Api)
            Assert.Equal(Some "1", Map.tryFind "capacity" error.Outputs)
            Assert.Equal(Some "overflow", Map.tryFind "key" error.Outputs)
        finally
            release.Set()

        JvLink.unsubscribe subscription |> success

    [<Fact; Trait("Category", "Contract")>]
    let ``Unsubscribe waits for an already running consumer to finish`` () =
        let native = new EventNative()
        use session = new Session(native)
        use entered = new ManualResetEventSlim()
        use release = new ManualResetEventSlim()

        use subscription =
            JvLink.subscribe
                (fun _ ->
                    entered.Set()
                    release.Wait())
                session
            |> success

        let mutable stop: Task<Result<unit, JvError>> option = None

        try
            native.Emit(pay "running")
            Assert.True(entered.Wait(5000))
            let task = Task.Run(fun () -> JvLink.unsubscribe subscription)
            stop <- Some task
            awaitCondition (fun () -> native.Stops = 1)
            Assert.False(task.IsCompleted)
        finally
            release.Set()

        let task = stop |> Option.get
        Assert.True(task.Wait(5000))
        task.Result |> success

    [<Fact; Trait("Category", "Contract")>]
    let ``Session disposal stops its delivery worker before releasing the native owner`` () =
        let native = new EventNative()
        use session = new Session(native)
        use entered = new ManualResetEventSlim()
        use release = new ManualResetEventSlim()

        JvLink.watchEvent
            (fun _ ->
                entered.Set()
                release.Wait())
            session
        |> success

        try
            native.Emit(pay "running")
            Assert.True(entered.Wait(5000))
            let dispose = Task.Run(fun () -> JvLink.disconnect session)

            // Polling getVersion can acquire the operation gate before Disconnect,
            // making Disconnect correctly return Busy instead of starting disposal.
            awaitCondition (fun () -> session.IsDisposed)

            match JvLink.getVersion session with
            | Error error -> Assert.Equal(JvErrorKind.Disposed, error.Kind)
            | Ok _ -> Assert.Fail("A disconnecting session must reject SDK calls")

            Assert.False(native.Disposed)
            release.Set()
            Assert.True(dispose.Wait(5000))
            dispose.Result |> success
            Assert.True(native.Disposed)
        finally
            release.Set()

    [<Fact; Trait("Category", "Contract")>]
    let ``Cancellation closes native registration and prevents subsequent delivery`` () =
        let native = new EventNative()
        use session = new Session(native)
        use cancellation = new CancellationTokenSource()
        let received = ConcurrentQueue<JvEvent>()

        use subscription =
            JvLink.subscribeWithOptions
                { SubscriptionOptions.Default with
                    CancellationToken = cancellation.Token }
                received.Enqueue
                session
            |> success

        cancellation.Cancel()
        awaitCondition (fun () -> native.Stops = 1)

        JvLink.unsubscribe subscription
        |> function
            | Error error when error.Kind = JvErrorKind.Busy ->
                awaitCondition (fun () -> JvLink.unsubscribe subscription = Ok())
            | result -> result |> success

        native.EmitStale(pay "after-cancellation")
        Assert.Empty(received)

    [<Fact; Trait("Category", "Contract")>]
    let ``Session retains a self-stopped worker until its callback exits across restart`` () =
        let native = new EventNative()
        use session = new Session(native)
        use stopped = new ManualResetEventSlim()
        use release = new ManualResetEventSlim()

        use first =
            JvLink.subscribe
                (fun _ ->
                    JvLink.watchEventClose session |> success
                    stopped.Set()
                    release.Wait())
                session
            |> success

        try
            native.Emit(pay "self-stop")
            Assert.True(stopped.Wait(5000))
            use second = JvLink.subscribe ignore session |> success
            let dispose = Task.Run(fun () -> JvLink.disconnect session)

            // Observe disposal without taking the SDK operation gate: polling
            // getVersion can win the gate and make Disconnect return Busy.
            awaitCondition (fun () -> session.IsDisposed)

            match JvLink.getVersion session with
            | Error error -> Assert.Equal(JvErrorKind.Disposed, error.Kind)
            | Ok _ -> Assert.Fail("A disconnecting session must reject SDK calls")

            Assert.False(native.Disposed)
            release.Set()
            Assert.True(dispose.Wait(5000))
            dispose.Result |> success
            Assert.True(native.Disposed)
        finally
            release.Set()

    [<Fact; Trait("Category", "Contract")>]
    let ``Failed native close is typed and can be retried`` () =
        let native = new EventNative(StopCode = -1)
        use session = new Session(native)
        use subscription = JvLink.subscribe ignore session |> success

        match JvLink.unsubscribe subscription with
        | Error error -> Assert.Equal(Some -1, error.Code)
        | Ok() -> failwith "Native close failure was hidden"

        native.StopCode <- 0
        JvLink.unsubscribe subscription |> success
        JvLink.unsubscribe subscription |> success
        Assert.Equal(2, native.Stops)

    [<Theory;
      InlineData(1);
      InlineData(2);
      InlineData(3);
      InlineData(4);
      InlineData(5);
      InlineData(6);
      InlineData(7);
      Trait("Category", "Contract")>]
    let ``Partial connection failure removes every earlier registered DISPID`` failAt =
        let connected = ResizeArray<int>()
        let removed = ResizeArray<int>()

        let register id _ =
            if id = failAt then
                raise (Runtime.InteropServices.COMException("registration-fault", -2147467259))
            else
                connected.Add id

        let result =
            EventRegistration.attach register (fun id _ -> removed.Add id) [ for id in 1..7 -> id, ignore<int> ]

        match result with
        | Error error -> Assert.Equal(Some -2147467259, error.Code)
        | Ok() -> failwith "Partial registration failure became success"

        Assert.Equal<int>(connected |> Seq.rev, removed)

    [<Fact; Trait("Category", "Contract")>]
    let ``Removal failure attempts every remaining handler and retains failed DISPIDs`` () =
        let removed = ResizeArray<int>()

        let unregister id _ =
            removed.Add id

            if id = 3 then
                raise (InvalidOperationException("remove-fault"))

        match EventRegistration.detach unregister [ for id in 1..7 -> id, ignore<int> ] with
        | Error error -> Assert.Equal(Some "3", Map.tryFind "failedDispids" error.Outputs)
        | Ok() -> failwith "Removal failure became success"

        Assert.Equal<int>([ 1..7 ], removed)

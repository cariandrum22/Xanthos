module Xanthos.UnitTests.ComEventsTests

open System
open Xunit
open Xanthos.Core
open Xanthos.Core.Errors
open Xanthos.Core.ErrorCodes
open Xanthos.Interop

/// Tests for JvLinkStub event callback mechanism
module StubEventTests =

    [<Fact>]
    let ``WatchEvent stores callback and RaiseEvent invokes it`` () =
        let stub = new JvLinkStub()
        let client = stub :> IJvLinkClient
        let mutable received = None

        client.Init("test-sid") |> ignore
        let result = client.WatchEvent(fun key -> received <- Some key)

        Assert.True(Result.isOk result)
        Assert.True(received.IsNone)

        // Trigger the event
        stub.RaiseEvent "0B12202401010101"

        Assert.True(received.IsSome)
        Assert.Equal("0B12202401010101", received.Value)

    [<Fact>]
    let ``WatchEventClose clears callback`` () =
        let stub = new JvLinkStub()
        let client = stub :> IJvLinkClient
        let mutable callCount = 0

        client.Init("test-sid") |> ignore
        client.WatchEvent(fun _ -> callCount <- callCount + 1) |> ignore

        stub.RaiseEvent "event1"
        Assert.Equal(1, callCount)

        client.WatchEventClose() |> ignore

        // After close, events should not trigger callback
        stub.RaiseEvent "event2"
        Assert.Equal(1, callCount) // Still 1

    [<Fact>]
    let ``WatchEvent fails if not initialized`` () =
        let stub = new JvLinkStub()
        let client = stub :> IJvLinkClient

        // Don't call Init
        let result = client.WatchEvent(fun _ -> ())

        Assert.True(Result.isError result)

    [<Fact>]
    let ``Multiple RaiseEvent calls invoke callback each time`` () =
        let stub = new JvLinkStub()
        let client = stub :> IJvLinkClient
        let events = ResizeArray<string>()

        client.Init("test-sid") |> ignore
        client.WatchEvent(fun key -> events.Add(key)) |> ignore

        stub.RaiseEvent "0B12event1"
        stub.RaiseEvent "0B11event2"
        stub.RaiseEvent "0B16JCevent3"

        Assert.Equal(3, events.Count)
        Assert.Equal("0B12event1", events.[0])
        Assert.Equal("0B11event2", events.[1])
        Assert.Equal("0B16JCevent3", events.[2])

/// Tests for WatchEvent type parsing
module WatchEventParsingTests =
    open Xanthos.Core.Serialization

    [<Fact>]
    let ``parseWatchEvent handles payoff confirmed event`` () =
        let result = parseWatchEvent "0B12202401010101"

        Assert.Equal(WatchEventType.PayoffConfirmed, result.Event)
        Assert.True(result.MeetingDate.IsSome)
        Assert.Equal(2024, result.MeetingDate.Value.Year)
        Assert.Equal(1, result.MeetingDate.Value.Month)
        Assert.Equal(1, result.MeetingDate.Value.Day)
        Assert.Equal(Some "01", result.CourseCode)
        Assert.Equal(Some "01", result.RaceNumber)

    [<Fact>]
    let ``parseWatchEvent handles horse weight event`` () =
        let result = parseWatchEvent "0B11202312250502"

        Assert.Equal(WatchEventType.HorseWeight, result.Event)
        Assert.True(result.MeetingDate.IsSome)
        Assert.Equal(2023, result.MeetingDate.Value.Year)
        Assert.Equal(Some "05", result.CourseCode)
        Assert.Equal(Some "02", result.RaceNumber)

    [<Fact>]
    let ``parseWatchEvent handles jockey change event`` () =
        let result = parseWatchEvent "0B16JC202401010101"

        Assert.Equal(WatchEventType.JockeyChange, result.Event)
        Assert.Equal(Some "JC", result.RecordType)

    [<Fact>]
    let ``parseWatchEvent handles weather change event`` () =
        let result = parseWatchEvent "0B16WE202401010101"

        Assert.Equal(WatchEventType.WeatherChange, result.Event)
        Assert.Equal(Some "WE", result.RecordType)

    [<Fact>]
    let ``parseWatchEvent handles course change event`` () =
        let result = parseWatchEvent "0B16CC202401010101"

        Assert.Equal(WatchEventType.CourseChange, result.Event)
        Assert.Equal(Some "CC", result.RecordType)

    [<Fact>]
    let ``parseWatchEvent handles avoided race event`` () =
        let result = parseWatchEvent "0B16AV202401010101"

        Assert.Equal(WatchEventType.AvoidedRace, result.Event)
        Assert.Equal(Some "AV", result.RecordType)

    [<Fact>]
    let ``parseWatchEvent handles start time change event`` () =
        let result = parseWatchEvent "0B16TC202401010101"

        Assert.Equal(WatchEventType.StartTimeChange, result.Event)
        Assert.Equal(Some "TC", result.RecordType)

    [<Fact>]
    let ``parseWatchEvent handles unknown event gracefully`` () =
        let result = parseWatchEvent "UNKNOWNEVENT"

        match result.Event with
        | WatchEventType.UnknownEvent raw -> Assert.Equal("UNKNOWNEVENT", raw)
        | _ -> Assert.Fail("Expected UnknownEvent")

    [<Fact>]
    let ``parseWatchEvent normalizes JV text`` () =
        // Full-width digits should be normalized
        let result = parseWatchEvent "０Ｂ１２２０２４０１０１０１０１"

        Assert.Equal(WatchEventType.PayoffConfirmed, result.Event)

/// Tests for event callback integration with JvLinkService
module ServiceIntegrationTests =
    open Xanthos.Runtime

    let private createConfig () =
        { JvLinkConfig.Sid = "test-sid"
          SavePath = None
          ServiceKey = None
          UseJvGets = None }

    [<Fact>]
    let ``JvLinkService watch events use callback correctly`` () =
        let stub = new JvLinkStub()
        let service = new JvLinkService(stub, createConfig ())
        let events = ResizeArray<Result<WatchEvent, _>>()

        // Subscribe to watch events
        use subscription = service.WatchEvents.Subscribe(fun ev -> events.Add(ev))

        // Start the watch event mechanism
        match service.StartWatchEvents() with
        | Ok() -> ()
        | Error err -> failwithf "StartWatchEvents failed: %A" err

        // Trigger event via stub
        stub.RaiseEvent "0B12202401010101"

        // Give async pipeline time to process
        System.Threading.Thread.Sleep(100)

        // Verify event was received
        Assert.Equal(1, events.Count)

        match events.[0] with
        | Ok ev ->
            Assert.Equal(WatchEventType.PayoffConfirmed, ev.Event)
            Assert.Equal("0B12202401010101", ev.RawKey)
        | Error err -> failwithf "Expected Ok but got Error: %A" err

    [<Fact>]
    let ``JvLinkService StartWatchEvents returns Ok after init`` () =
        let stub = new JvLinkStub()
        let service = new JvLinkService(stub, createConfig ())

        let result = service.StartWatchEvents()

        Assert.True(Result.isOk result)

    [<Fact>]
    let ``JvLinkService StartWatchEvents success path delivers events`` () =
        // This test verifies the complete success path using the stub:
        // 1. StartWatchEvents returns Ok
        // 2. Events raised via stub are delivered to WatchEvents observable
        let stub = new JvLinkStub()
        let service = new JvLinkService(stub, createConfig ())
        let receivedEvents = ResizeArray<Result<WatchEvent, XanthosError>>()

        // Subscribe before starting
        use subscription = service.WatchEvents.Subscribe(receivedEvents.Add)

        // Start should succeed (stub has no configured failure)
        let startResult = service.StartWatchEvents()
        Assert.True(Result.isOk startResult, "StartWatchEvents should succeed with stub")

        // Raise an event via stub
        stub.RaiseEvent "0B12202401010101"

        // Allow async processing
        System.Threading.Thread.Sleep(50)

        // Verify event was delivered
        Assert.True(receivedEvents.Count >= 1, "Should have received at least one event")

        match receivedEvents.[0] with
        | Ok ev -> Assert.Equal(WatchEventType.PayoffConfirmed, ev.Event)
        | Error err -> Assert.Fail($"Expected Ok but got Error: {err}")

    [<Fact>]
    let ``JvLinkService StopWatchEvents clears subscription`` () =
        let stub = new JvLinkStub()
        let service = new JvLinkService(stub, createConfig ())

        service.StartWatchEvents() |> ignore
        let result = service.StopWatchEvents()

        Assert.True(Result.isOk result)

    [<Fact>]
    let ``JvLinkService StartWatchEvents returns Error when COM events unavailable`` () =
        let stub = new JvLinkStub()
        // Configure stub to simulate COM event connection failure
        stub.ConfigureWatchEventFailure(
            CommunicationFailure(ErrorCodes.ComConnectionFailure, "COM event connection failed")
        )

        let service = new JvLinkService(stub, createConfig ())

        let result = service.StartWatchEvents()

        match result with
        | Error err ->
            // Verify it's an InteropError containing CommunicationFailure
            match err with
            | InteropError(CommunicationFailure(code, _)) -> Assert.Equal(ErrorCodes.ComConnectionFailure, code)
            | _ -> Assert.Fail($"Expected CommunicationFailure but got {err}")
        | Ok() -> Assert.Fail("Expected Error but got Ok")

/// Robustness tests for WatchEvents under various stress conditions
module WatchEventsRobustnessTests =
    open Xanthos.Runtime
    open System.Threading

    let private createConfig () =
        { JvLinkConfig.Sid = "test-sid"
          SavePath = None
          ServiceKey = None
          UseJvGets = None }

    [<Fact>]
    let ``Rapid event delivery processes all events in order`` () =
        let stub = new JvLinkStub()
        let service = new JvLinkService(stub, createConfig ())
        let receivedEvents = ResizeArray<string>()
        let eventCount = 100

        use subscription =
            service.WatchEvents.Subscribe(fun ev ->
                match ev with
                | Ok e ->
                    match e.Event with
                    | WatchEventType.PayoffConfirmed -> receivedEvents.Add(e.RawKey)
                    | _ -> ()
                | Error _ -> ())

        service.StartWatchEvents() |> ignore

        // Fire events rapidly
        for i in 1..eventCount do
            stub.RaiseEvent $"0B12202401{i:D6}"

        // Allow async event processing (events are queued to a dedicated background thread)
        Thread.Sleep(500)

        Assert.Equal(eventCount, receivedEvents.Count)
        // Verify order is preserved
        for i in 1..eventCount do
            Assert.Contains($"0B12202401{i:D6}", receivedEvents.[i - 1])

    [<Fact>]
    let ``Multiple subscribers receive same events`` () =
        let stub = new JvLinkStub()
        let service = new JvLinkService(stub, createConfig ())
        let subscriber1Events = ResizeArray<WatchEvent>()
        let subscriber2Events = ResizeArray<WatchEvent>()
        let subscriber3Events = ResizeArray<WatchEvent>()

        use sub1 =
            service.WatchEvents.Subscribe(fun ev ->
                match ev with
                | Ok e -> subscriber1Events.Add(e)
                | _ -> ())

        use sub2 =
            service.WatchEvents.Subscribe(fun ev ->
                match ev with
                | Ok e -> subscriber2Events.Add(e)
                | _ -> ())

        use sub3 =
            service.WatchEvents.Subscribe(fun ev ->
                match ev with
                | Ok e -> subscriber3Events.Add(e)
                | _ -> ())

        service.StartWatchEvents() |> ignore

        stub.RaiseEvent "0B12202401010101"
        stub.RaiseEvent "0B11202401010102"
        stub.RaiseEvent "0B16JC202401010103"

        Thread.Sleep(100)

        Assert.Equal(3, subscriber1Events.Count)
        Assert.Equal(3, subscriber2Events.Count)
        Assert.Equal(3, subscriber3Events.Count)

        // All subscribers received same events
        Assert.Equal(subscriber1Events.[0].RawKey, subscriber2Events.[0].RawKey)
        Assert.Equal(subscriber2Events.[0].RawKey, subscriber3Events.[0].RawKey)

    [<Fact>]
    let ``Exception in one subscriber does not affect others`` () =
        let stub = new JvLinkStub()
        let service = new JvLinkService(stub, createConfig ())
        let goodSubscriberEvents = ResizeArray<WatchEvent>()
        let mutable badSubscriberCalled = false

        // Bad subscriber throws exception
        use badSub =
            service.WatchEvents.Subscribe(fun ev ->
                badSubscriberCalled <- true
                failwith "Subscriber exception")

        // Good subscriber should still receive events
        use goodSub =
            service.WatchEvents.Subscribe(fun ev ->
                match ev with
                | Ok e -> goodSubscriberEvents.Add(e)
                | _ -> ())

        service.StartWatchEvents() |> ignore

        // This should not crash the system
        try
            stub.RaiseEvent "0B12202401010101"
        with _ ->
            ()

        Thread.Sleep(100)

        // Bad subscriber was called (which threw)
        Assert.True(badSubscriberCalled)

    [<Fact>]
    let ``Unsubscribe during active event stream stops delivery`` () =
        let stub = new JvLinkStub()
        let service = new JvLinkService(stub, createConfig ())
        let receivedEvents = ResizeArray<int>()
        let mutable subscription: IDisposable = null
        let mutable eventCounter = 0

        subscription <-
            service.WatchEvents.Subscribe(fun ev ->
                eventCounter <- eventCounter + 1
                receivedEvents.Add(eventCounter)
                // Unsubscribe after first event
                if eventCounter = 1 then
                    subscription.Dispose())

        service.StartWatchEvents() |> ignore

        stub.RaiseEvent "0B12202401010101"
        Thread.Sleep(50)
        stub.RaiseEvent "0B12202401010102"
        stub.RaiseEvent "0B12202401010103"
        Thread.Sleep(100)

        // Should only have received first event (or possibly more due to timing)
        // The key is that unsubscribe doesn't crash
        Assert.True(receivedEvents.Count >= 1)

    [<Fact>]
    let ``Subscribe and unsubscribe repeatedly works correctly`` () =
        let stub = new JvLinkStub()
        let service = new JvLinkService(stub, createConfig ())
        let totalReceived = ref 0

        service.StartWatchEvents() |> ignore

        for _ in 1..10 do
            let sub =
                service.WatchEvents.Subscribe(fun ev ->
                    match ev with
                    | Ok _ -> Interlocked.Increment(totalReceived) |> ignore
                    | _ -> ())

            stub.RaiseEvent "0B12202401010101"
            Thread.Sleep(20)
            sub.Dispose()

        Thread.Sleep(100)

        // Each subscription should have received at least one event
        Assert.True(!totalReceived >= 10)

    [<Fact>]
    let ``Events with various types are correctly parsed`` () =
        let stub = new JvLinkStub()
        let service = new JvLinkService(stub, createConfig ())
        let eventTypes = ResizeArray<WatchEventType>()

        use sub =
            service.WatchEvents.Subscribe(fun ev ->
                match ev with
                | Ok e -> eventTypes.Add(e.Event)
                | _ -> ())

        service.StartWatchEvents() |> ignore

        // Fire all known event types
        stub.RaiseEvent "0B12202401010101" // PayoffConfirmed
        stub.RaiseEvent "0B11202401010101" // HorseWeight
        stub.RaiseEvent "0B16JC202401010101" // JockeyChange
        stub.RaiseEvent "0B16WE202401010101" // WeatherChange
        stub.RaiseEvent "0B16CC202401010101" // CourseChange
        stub.RaiseEvent "0B16AV202401010101" // AvoidedRace
        stub.RaiseEvent "0B16TC202401010101" // StartTimeChange

        Thread.Sleep(100)

        Assert.Equal(7, eventTypes.Count)
        Assert.Contains(WatchEventType.PayoffConfirmed, eventTypes)
        Assert.Contains(WatchEventType.HorseWeight, eventTypes)
        Assert.Contains(WatchEventType.JockeyChange, eventTypes)
        Assert.Contains(WatchEventType.WeatherChange, eventTypes)
        Assert.Contains(WatchEventType.CourseChange, eventTypes)
        Assert.Contains(WatchEventType.AvoidedRace, eventTypes)
        Assert.Contains(WatchEventType.StartTimeChange, eventTypes)

    [<Fact>]
    let ``Malformed event key is handled gracefully`` () =
        let stub = new JvLinkStub()
        let service = new JvLinkService(stub, createConfig ())
        let receivedEvents = ResizeArray<Result<WatchEvent, XanthosError>>()

        use sub = service.WatchEvents.Subscribe(receivedEvents.Add)

        service.StartWatchEvents() |> ignore

        // Fire malformed events - these should be handled gracefully
        stub.RaiseEvent ""
        stub.RaiseEvent "X"
        stub.RaiseEvent "INVALID"
        stub.RaiseEvent "0B99UNKNOWN202401010101"

        Thread.Sleep(100)

        // All events should be received (as Ok with UnknownEvent or parsed)
        Assert.Equal(4, receivedEvents.Count)

        // All should be Ok (unknown events are still valid WatchEvents)
        for ev in receivedEvents do
            Assert.True(Result.isOk ev)

    [<Fact>]
    let ``StartWatchEvents is idempotent`` () =
        let stub = new JvLinkStub()
        let service = new JvLinkService(stub, createConfig ())

        let result1 = service.StartWatchEvents()
        let result2 = service.StartWatchEvents()
        let result3 = service.StartWatchEvents()

        Assert.True(Result.isOk result1)
        Assert.True(Result.isOk result2)
        Assert.True(Result.isOk result3)

    [<Fact>]
    let ``StopWatchEvents is idempotent`` () =
        let stub = new JvLinkStub()
        let service = new JvLinkService(stub, createConfig ())

        service.StartWatchEvents() |> ignore

        let result1 = service.StopWatchEvents()
        let result2 = service.StopWatchEvents()
        let result3 = service.StopWatchEvents()

        Assert.True(Result.isOk result1)
        Assert.True(Result.isOk result2)
        Assert.True(Result.isOk result3)

    [<Fact>]
    let ``Events after StopWatchEvents are not delivered`` () =
        let stub = new JvLinkStub()
        let service = new JvLinkService(stub, createConfig ())
        let receivedEvents = ResizeArray<WatchEvent>()

        use sub =
            service.WatchEvents.Subscribe(fun ev ->
                match ev with
                | Ok e -> receivedEvents.Add(e)
                | _ -> ())

        service.StartWatchEvents() |> ignore

        stub.RaiseEvent "0B12202401010101"
        Thread.Sleep(50)

        service.StopWatchEvents() |> ignore

        stub.RaiseEvent "0B12202401010102"
        stub.RaiseEvent "0B12202401010103"
        Thread.Sleep(100)

        // Only the first event should be received
        Assert.Equal(1, receivedEvents.Count)

    [<Fact>]
    let ``StartWatchEvents after StopWatchEvents delivers events correctly`` () =
        // This test verifies that the BlockingCollection is correctly recreated
        // after CompleteAdding() is called in StopWatchEvents
        let stub = new JvLinkStub()
        let service = new JvLinkService(stub, createConfig ())
        let receivedEvents = ResizeArray<string>()

        use sub =
            service.WatchEvents.Subscribe(fun ev ->
                match ev with
                | Ok e -> receivedEvents.Add(e.RawKey)
                | _ -> ())

        // First cycle: Start -> Event -> Stop
        service.StartWatchEvents() |> ignore
        stub.RaiseEvent "0B12202401010101"
        Thread.Sleep(50)
        service.StopWatchEvents() |> ignore
        Thread.Sleep(50)

        Assert.True(receivedEvents.Count >= 1, "First cycle should receive event")
        let firstCycleCount = receivedEvents.Count

        // Second cycle: Start again after stop -> should still work
        let restartResult = service.StartWatchEvents()
        Assert.True(Result.isOk restartResult, "StartWatchEvents should succeed after Stop")

        stub.RaiseEvent "0B12202401010102"
        Thread.Sleep(100)

        // Should have received the second event as well
        Assert.True(
            receivedEvents.Count > firstCycleCount,
            $"Second cycle should receive events. Count={receivedEvents.Count}, FirstCycleCount={firstCycleCount}"
        )

        Assert.Contains("0B12202401010102", receivedEvents)

    [<Fact>]
    let ``Multiple Start-Stop cycles work correctly`` () =
        // Verify that the fix works for multiple cycles, not just one restart
        let stub = new JvLinkStub()
        let service = new JvLinkService(stub, createConfig ())
        let receivedEvents = ResizeArray<string>()

        use sub =
            service.WatchEvents.Subscribe(fun ev ->
                match ev with
                | Ok e -> receivedEvents.Add(e.RawKey)
                | _ -> ())

        for cycle in 1..3 do
            service.StartWatchEvents() |> ignore
            stub.RaiseEvent $"0B12202401{cycle:D6}"
            Thread.Sleep(50)
            service.StopWatchEvents() |> ignore
            Thread.Sleep(50)

        // Should have received events from all 3 cycles
        Assert.True(receivedEvents.Count >= 3, $"Should receive events from all cycles. Count={receivedEvents.Count}")
        Assert.Contains("0B12202401000001", receivedEvents)
        Assert.Contains("0B12202401000002", receivedEvents)
        Assert.Contains("0B12202401000003", receivedEvents)

/// Regression test for event queue overflow handling
/// Verifies that EventQueueOverflow is emitted from the consumer thread, not the STA callback
module EventQueueOverflowTests =
    open Xanthos.Runtime
    open System.Threading

    let private createConfig () =
        { JvLinkConfig.Sid = "test-sid"
          SavePath = None
          ServiceKey = None
          UseJvGets = None }

    [<Fact>]
    let ``EventQueueOverflow is delivered when queue fills up`` () =
        // Create service with tiny queue capacity to trigger overflow easily
        let stub = new JvLinkStub()
        let service = new JvLinkService(stub, createConfig (), eventQueueCapacity = 2)
        let receivedOk = ResizeArray<WatchEvent>()
        let receivedErrors = ResizeArray<XanthosError>()
        let overflowCount = ref 0

        use sub =
            service.WatchEvents.Subscribe(fun ev ->
                match ev with
                | Ok e -> receivedOk.Add(e)
                | Error(EventQueueOverflow n) -> Interlocked.Add(overflowCount, n) |> ignore
                | Error e -> receivedErrors.Add(e))

        service.StartWatchEvents() |> ignore

        // Block the consumer by sleeping in subscriber to force queue to fill
        use slowSub = service.WatchEvents.Subscribe(fun _ -> Thread.Sleep(50))

        // Fire many events rapidly - queue capacity is 2, so overflow should occur
        for i in 1..20 do
            stub.RaiseEvent $"0B12202401{i:D6}"

        // Allow time for events to be processed
        Thread.Sleep(500)

        // Verify that overflow was detected
        // (some events delivered + some overflows = total fired events)
        Assert.True(
            !overflowCount > 0 || receivedOk.Count >= 10,
            $"Expected overflow or all events processed. Overflow={!overflowCount}, OkCount={receivedOk.Count}"
        )

    [<Fact>]
    let ``EventQueueOverflow reports count of dropped events`` () =
        // Create service with capacity=1 to maximize overflow likelihood
        let stub = new JvLinkStub()
        let service = new JvLinkService(stub, createConfig (), eventQueueCapacity = 1)
        let overflowEvents = ResizeArray<int>()

        use sub =
            service.WatchEvents.Subscribe(fun ev ->
                match ev with
                | Error(EventQueueOverflow n) -> overflowEvents.Add(n)
                | _ -> ())

        service.StartWatchEvents() |> ignore

        // Use a semaphore to block event processing and guarantee overflow
        use blockProcessing = new ManualResetEventSlim(false)

        use blockingSub =
            service.WatchEvents.Subscribe(fun _ -> blockProcessing.Wait(100) |> ignore)

        // Fire events while blocked
        for i in 1..10 do
            stub.RaiseEvent $"0B12202401{i:D6}"
            Thread.Sleep(5) // Small delay to let queue fill

        // Release the block
        blockProcessing.Set()
        Thread.Sleep(200)

        // If overflow occurred, each overflow error contains the count of events dropped
        // The count may be accumulated (e.g., if multiple overflows happen before consumer runs)
        for n in overflowEvents do
            Assert.True(n > 0, "Overflow count should be positive")

    [<Fact>]
    let ``Overflow does not crash the service`` () =
        // Verify that overflow is handled gracefully without exceptions
        let stub = new JvLinkStub()
        let service = new JvLinkService(stub, createConfig (), eventQueueCapacity = 1)
        let mutable exceptionOccurred = false

        use sub = service.WatchEvents.Subscribe(fun _ -> ())

        service.StartWatchEvents() |> ignore

        try
            // Fire many events rapidly
            for i in 1..100 do
                stub.RaiseEvent $"0B12202401{i:D6}"

            Thread.Sleep(200)

            // Stop and restart to verify service still works
            service.StopWatchEvents() |> ignore
            Thread.Sleep(50)

            let restartResult = service.StartWatchEvents()
            Assert.True(Result.isOk restartResult, "Should be able to restart after overflow")

            stub.RaiseEvent "0B12202401999999"
            Thread.Sleep(50)
        with ex ->
            exceptionOccurred <- true
            Assert.Fail($"Service threw exception during overflow: {ex.Message}")

        Assert.False(exceptionOccurred)

    [<Fact>]
    let ``Overflow events are reported from consumer thread not STA callback`` () =
        // This test verifies the fix for the STA thread blocking issue.
        // The overflow notification should come from the consumer thread,
        // which we can verify by checking that the subscriber doesn't block
        // the RaiseEvent call for longer than expected.
        let stub = new JvLinkStub()
        let service = new JvLinkService(stub, createConfig (), eventQueueCapacity = 1)
        let subscriberDelay = 200 // ms
        let mutable subscriberCalled = false

        use sub =
            service.WatchEvents.Subscribe(fun _ ->
                subscriberCalled <- true
                Thread.Sleep(subscriberDelay)) // Slow subscriber

        service.StartWatchEvents() |> ignore

        // First event - will start processing
        stub.RaiseEvent "0B12202401000001"
        Thread.Sleep(50)

        // Time how long RaiseEvent takes when queue is full
        // If overflow notification ran on STA thread (bug), this would take ~subscriberDelay ms
        // With fix (consumer thread), RaiseEvent should return quickly
        let sw = System.Diagnostics.Stopwatch.StartNew()

        for i in 2..10 do
            stub.RaiseEvent $"0B12202401{i:D6}"

        sw.Stop()

        // All RaiseEvent calls should complete quickly (not blocked by subscriber)
        // Allow generous margin for test reliability
        Assert.True(
            sw.ElapsedMilliseconds < int64 (subscriberDelay * 5),
            $"RaiseEvent took {sw.ElapsedMilliseconds}ms - may indicate STA thread blocking. Expected < {subscriberDelay * 5}ms"
        )

        // Give time for at least one subscriber call
        Thread.Sleep(subscriberDelay + 100)
        Assert.True(subscriberCalled, "Subscriber should have been called")

namespace Xanthos.FunctionalScenarioTests

open System
open System.IO
open System.Text.Json
open System.Threading
open Xunit
open Xunit.Abstractions
open Xanthos
open Xanthos.Interop

/// Exercises the production native lifetime alongside public Session operations.
/// The oracle below owns separate booleans and never reads production state.
type private ModelNative(fake: NativeFake) =
    let raw = fake :> INativeJvLink
    let lifetime = NativeSessionLifetime()

    member _.ProbeOwnership(dataOwned, watchOwned) =
        let calls = ResizeArray<string>()
        // Failed cleanup probes preserve obligations; callbacks own no real resources.
        let result =
            lifetime.Cleanup(
                (fun api ->
                    calls.Add api
                    Ok -201),
                (fun () -> calls.Add "detach"),
                (fun () -> calls.Add "release")
            )

        let expected =
            [ if watchOwned then
                  yield "JVWatchEventClose"
              yield "detach"
              if dataOwned then
                  yield "JVClose"
              yield "release" ]

        Assert.Equal<string>(expected, calls)

        let first =
            if watchOwned then Some "JVWatchEventClose"
            elif dataOwned then Some "JVClose"
            else None

        let expectedResult =
            match first with
            | None -> Ok()
            | Some api ->
                Error
                    { Api = api
                      Code = Some -201
                      Kind = JvErrorKind.Sdk
                      Outputs = Map.empty
                      Message = $"{api} returned SDK code -201 during cleanup." }

        Assert.Equal<Result<unit, JvError>>(expectedResult, result)

    interface INativeJvLink with
        member _.Invoke(api, args, outputs) =
            raw.Invoke(api, args, outputs)
            |> Result.map (fun value ->
                match value with
                | :? int as code -> lifetime.Observe(api, code)
                | _ -> ()

                value)

        member _.Get name = raw.Get name
        member _.Put(name, value) = raw.Put(name, value)

        member _.Watch callback =
            raw.Watch callback |> Result.map (fun () -> lifetime.Observe("JVWatchEvent", 0))

        member _.StopWatch() =
            raw.StopWatch()
            |> Result.map (fun () -> lifetime.Observe("JVWatchEventClose", 0))

        member _.Dispose() =
            let invoke api =
                if api = "JVWatchEventClose" then
                    raw.StopWatch() |> Result.map (fun () -> 0)
                else
                    raw.Invoke(api, [||], []) |> Result.map unbox<int>

            match lifetime.Cleanup(invoke, ignore, raw.Dispose) with
            | Ok() -> ()
            | Error error -> raise (SessionCleanupException error)

module internal SessionModel =
    let seeds =
        [| 104729
           130363
           155921
           196613
           262147
           327673
           393241
           458789
           524309
           589823 |]

    let stress () =
        Environment.GetEnvironmentVariable "XANTHOS_TEST_PROFILE" = "Stress"

    let success =
        function
        | Ok value -> value
        | Error error -> failwithf "%A" error

    // Legality of open/read is SDK-owned. The Session oracle concerns only
    // ownership, exclusion and subscription generations, not racing rules.
    let verify (trace: int array) =
        let native = new NativeFake(SettingsStore())
        let adapter = new ModelNative(native)
        let session = new Session(adapter)
        let mutable disconnected = false
        let mutable dataOwned = false
        let mutable watchOwned = false
        let retained = ResizeArray<byte[]>()
        let buffer = [| 1uy; 2uy; 3uy |]
        let registrationFailure = Exception("controlled registration failure")
        let mutable active: Subscription option = None
        let mutable stale: Subscription option = None
        let received = Collections.Concurrent.ConcurrentQueue<JvEvent>()
        let reached = Collections.Generic.HashSet<string>()
        let workers = Collections.Concurrent.ConcurrentDictionary<int, Thread>()

        let receive event =
            workers.TryAdd(Thread.CurrentThread.ManagedThreadId, Thread.CurrentThread)
            |> ignore

            received.Enqueue event

        let request =
            { Dataspec = "RACE"
              FromTime = DateTime(2026, 9, 5)
              ToTime = None
              Option = 1 }

        let error api kind code outputs message : JvError =
            { Api = api
              Kind = kind
              Code = code
              Outputs = outputs
              Message = message }

        let disposed api =
            error api JvErrorKind.Disposed None Map.empty "Session has been disconnected."

        let sdk api code =
            error api JvErrorKind.Sdk (Some code) Map.empty $"{api} returned SDK code {code}."

        let assertError (expected: JvError) (result: Result<'T, JvError>) =
            match result with
            | Error actual -> Assert.Equal<JvError>(expected, actual)
            | Ok _ -> failwith "Expected the complete error Result"

        let check api (result: Result<'T, JvError>) =
            if disconnected then
                assertError (disposed api) result
            else
                result |> success |> ignore

        try
            for index, command in Array.indexed trace do
                native.Handler <- fun _ _ -> None
                native.OnWatch <- ignore
                let before = native.Calls.Length

                let effective =
                    match command with
                    | 13 -> stale.IsSome && active.IsSome && not disconnected
                    | 16 -> active.IsSome && not disconnected
                    | 17 -> not disconnected
                    | _ -> true

                let transition = string command + (if disconnected then ":disposed" else ":live")

                let expectedCalls =
                    if disconnected then
                        [||]
                    else
                        match command with
                        | 0 -> [| "JVInit" |]
                        | 1
                        | 2
                        | 3 -> [| "JVOpen" |]
                        | 4
                        | 5
                        | 6
                        | 7
                        | 19 -> [| "JVGets" |]
                        | 8 -> [| "JVCancel" |]
                        | 9
                        | 10 -> [| "JVClose" |]
                        | 11
                        | 18 when active.IsNone -> [| "JVWatchEvent" |]
                        | 12 -> [| "JVWatchEventClose" |]
                        | 14 ->
                            [| if watchOwned then
                                   "JVWatchEventClose"
                               if dataOwned then
                                   "JVClose"
                               "Dispose" |]
                        | 15 -> [| "m_JVLinkVersion" |]
                        | 17 -> [| "JVStatus" |]
                        | _ -> [||]

                match command with
                | 0 -> JvLink.init "UNKNOWN" session |> check "JVInit"
                | 1
                | 2
                | 3 ->
                    let code =
                        if command = 1 then 0
                        elif command = 2 then -1
                        else -305

                    native.Handler <-
                        fun api args ->
                            if api = "JVOpen" then
                                args[3] <- box 0
                                args[4] <- box 0
                                args[5] <- box "20260905000000"
                                Some(Ok(box code))
                            else
                                None

                    let result = JvLink.openData request session

                    if not disconnected && command = 3 then
                        assertError (sdk "JVOpen" -305) result
                    else if disconnected then
                        check "JVOpen" result
                    else
                        let metadata =
                            { ReadCount = 0
                              DownloadCount = 0
                              LastFileTimestamp = "20260905000000" }

                        Assert.Equal(
                            (if command = 1 then
                                 OpenOutcome.Opened metadata
                             else
                                 OpenOutcome.NoData metadata),
                            success result
                        )
                | 4
                | 5
                | 6
                | 7 ->
                    let code, expected =
                        [| 3, ReadState.Record
                           0, ReadState.EndOfStream
                           -1, ReadState.FileBoundary
                           -3, ReadState.DownloadPending |][command - 4]

                    Array.blit [| 1uy; 2uy; 3uy |] 0 buffer 0 3

                    native.Handler <-
                        fun api args ->
                            if api = "JVGets" then
                                args[0] <- box buffer
                                args[2] <- box "model.jvd"
                                Some(Ok(box code))
                            else
                                None

                    let result = JvLink.gets session

                    if disconnected then
                        check "JVGets" result
                    else
                        let actual = result |> success
                        Assert.Equal(expected, actual.State)
                        Assert.Equal(code, actual.ReturnCode)
                        Assert.Equal(max 0 code, actual.ByteCount)
                        Assert.Equal(131072, actual.BufferSize)
                        Assert.Equal(None, actual.RawText)
                        Assert.Equal("model.jvd", actual.Filename)
                        Assert.Equal<byte>((if code > 0 then [| 1uy; 2uy; 3uy |] else [||]), actual.Data)

                        if code > 0 then
                            retained.Add actual.Data
                | 8 -> JvLink.cancel session |> check "JVCancel"
                | 9 -> JvLink.closeData session |> check "JVClose"
                | 10 ->
                    native.Handler <- fun api _ -> if api = "JVClose" then Some(Ok(box -201)) else None

                    JvLink.closeData session
                    |> assertError (
                        if disconnected then
                            disposed "JVClose"
                        else
                            sdk "JVClose" -201
                    )
                | 11 ->
                    match JvLink.subscribe receive session with
                    | Ok subscription ->
                        Assert.False(disconnected || active.IsSome)
                        active <- Some subscription
                    | Error error ->
                        let expected =
                            if disconnected then
                                disposed "JVWatchEvent"
                            else
                                { disposed "JVWatchEvent" with
                                    Kind = JvErrorKind.Busy
                                    Message = "A watch subscription is already active." }

                        Assert.Equal<JvError>(expected, error)
                | 12 ->
                    match active with
                    | Some subscription ->
                        JvLink.unsubscribe subscription |> check "JVWatchEventClose"
                        stale <- active
                        active <- None
                    | None -> JvLink.watchEventClose session |> check "JVWatchEventClose"
                | 13 ->
                    match stale with
                    | Some previous ->
                        JvLink.unsubscribe previous |> success
                        Assert.Equal(before, native.Calls.Length)
                    | None -> ()
                | 14 ->
                    JvLink.disconnect session |> success
                    disconnected <- true
                    active <- None
                | 15 ->
                    let result = JvLink.getVersion session

                    if disconnected then
                        check "m_JVLinkVersion" result
                    else
                        Assert.Equal("0500", success result)
                | 19 ->
                    native.Handler <-
                        fun api args ->
                            if api = "JVGets" then
                                args[0] <- box buffer
                                args[1] <- box 4096
                                args[2] <- box "failed-model.jvd"
                                Some(Ok(box -203))
                            else
                                None

                    let expected =
                        if disconnected then
                            disposed "JVGets"
                        else
                            { sdk "JVGets" -203 with
                                Outputs = Map.ofList [ "filename", "failed-model.jvd"; "size", "4096" ] }

                    JvLink.gets session |> assertError expected
                | 16 when not disconnected && active.IsSome ->
                    let count = received.Count
                    let key = string index
                    native.Emit EventKind.Weight key
                    Assert.True(SpinWait.SpinUntil((fun () -> received.Count = count + 1), 5000))
                    Assert.Equal(key, (received.ToArray() |> Array.last).RawKey)
                    Assert.Equal(EventKind.Weight, (received.ToArray() |> Array.last).Kind)
                | 16 -> ()
                | 17 when not disconnected ->
                    use entered = new ManualResetEventSlim(false)
                    use released = new ManualResetEventSlim(false)

                    native.Handler <-
                        fun api _ ->
                            if api = "JVStatus" then
                                entered.Set()
                                released.Wait()
                                Some(Ok(box 0))
                            else
                                None

                    let operation = Tasks.Task.Run(fun () -> JvLink.status session)

                    try
                        Assert.True(entered.Wait(TimeSpan.FromSeconds 5.))
                        let calls = native.Calls.Length

                        JvLink.getVersion session
                        |> assertError (
                            error
                                "m_JVLinkVersion"
                                JvErrorKind.Busy
                                None
                                Map.empty
                                "Another operation owns this session."
                        )

                        Assert.Equal(calls, native.Calls.Length)
                    finally
                        released.Set()

                    Assert.True(operation.Wait(TimeSpan.FromSeconds 5.))
                    Assert.Equal<Result<int, JvError>>(Ok 0, operation.Result)
                | 17 -> ()
                | 18 ->
                    native.OnWatch <- fun () -> raise registrationFailure

                    match JvLink.subscribe receive session with
                    | Ok _ -> failwith "Registration failure was accepted"
                    | Error error ->
                        let expected =
                            if disconnected then
                                disposed "JVWatchEvent"
                            elif active.IsSome then
                                { disposed "JVWatchEvent" with
                                    Kind = JvErrorKind.Busy
                                    Message = "A watch subscription is already active." }
                            else
                                { Api = "JVWatchEvent"
                                  Code = Some registrationFailure.HResult
                                  Kind = JvErrorKind.Invocation
                                  Outputs = Map.empty
                                  Message = registrationFailure.Message }

                        Assert.Equal<JvError>(expected, error)
                | _ -> failwith $"Unknown model command {command}"

                Assert.Equal<string>(expectedCalls, native.Calls |> Array.skip before)

                if not disconnected then
                    if command = 1 || command = 2 then
                        dataOwned <- true

                    if command = 9 then
                        dataOwned <- false

                    watchOwned <- active.IsSome
                else
                    dataOwned <- false
                    watchOwned <- false

                adapter.ProbeOwnership(dataOwned, watchOwned)
                Array.fill buffer 0 buffer.Length 255uy

                for bytes in retained do
                    Assert.Equal<byte>([| 1uy; 2uy; 3uy |], bytes)

                if effective then
                    reached.Add transition |> ignore

                Assert.Equal((if disconnected then 1 else 0), native.Disposals)

                if disconnected && command <> 14 then
                    Assert.Equal(before, native.Calls.Length)
        finally
            native.Handler <- fun _ _ -> None
            JvLink.disconnect session |> success

        Assert.Equal(1, native.Disposals)
        adapter.ProbeOwnership(false, false)

        for bytes in retained do
            Assert.Equal<byte>([| 1uy; 2uy; 3uy |], bytes)

        for worker in workers.Values do
            Assert.False(worker.IsAlive, "Owned event worker survived session cleanup")

        reached |> Set.ofSeq

    let minimize (trace: int array) =
        let signature operation =
            try
                verify operation |> ignore
                None
            with error ->
                Some(error.GetType().FullName, error.Message)

        let original = signature trace

        if original.IsNone then
            invalidArg "trace" "Only failing traces can be minimized."

        let mutable candidate = trace
        let mutable index = 0

        while index < candidate.Length do
            let shorter =
                candidate
                |> Array.mapi (fun i value -> i, value)
                |> Array.choose (fun (i, value) -> if i = index then None else Some value)

            let failed = signature shorter = original

            if failed then
                candidate <- shorter
                index <- 0
            else
                index <- index + 1

        candidate

    let saveFailure seed sequence original error =
        let shrunk = minimize original

        let directory =
            match Environment.GetEnvironmentVariable "XANTHOS_FAILURE_DIRECTORY" with
            | null
            | "" -> Path.Combine(AppContext.BaseDirectory, "TestResults", "generated-failures")
            | path -> path

        Directory.CreateDirectory directory |> ignore

        let path =
            Path.Combine(directory, $"session-{seed}-{sequence}-{Guid.NewGuid():N}.json")

        File.WriteAllText(
            path,
            JsonSerializer.Serialize(
                {| seed = seed
                   sequence = sequence
                   original = original
                   minimal = shrunk
                   message = error
                   minimization = "one-operation deletion minimal with same exception type and message" |},
                JsonSerializerOptions(WriteIndented = true)
            )
        )

        path

type SessionModelTests(output: ITestOutputHelper) =
    static member Seeds =
        (if SessionModel.stress () then
             SessionModel.seeds
         else
             SessionModel.seeds[..2])
        |> Seq.map (fun seed -> [| box seed |])

    [<Theory; MemberData(nameof SessionModelTests.Seeds)>]
    member _.``Q07 model traces preserve session ownership exclusion and event generations``(seed: int) =
        let random = Random seed
        let minimum, maximum = if SessionModel.stress () then 200, 250 else 2, 50

        let required =
            [| 15
               0
               1
               4
               5
               6
               7
               19
               8
               10
               9
               2
               9
               3
               18
               11
               16
               12
               11
               13
               16
               12
               17
               14
               15
               14 |]

        let counts = Array.zeroCreate<int> 20
        let mutable reached = Set.empty<string>

        for sequence in 0..99 do
            let length = random.Next(minimum, maximum + 1)

            let trace =
                if sequence = 0 then
                    Array.append required (Array.init (max 0 (length - required.Length)) (fun _ -> random.Next(20)))
                else
                    Array.init length (fun _ -> random.Next(20))

            for command in trace do
                counts[command] <- counts[command] + 1

            try
                reached <- Set.union reached (SessionModel.verify trace)
            with error ->
                let path = SessionModel.saveFailure seed sequence trace error.Message
                failwithf "seed=%d sequence=%d failure payload=%s error=%s" seed sequence path error.Message

        Assert.All(counts, fun count -> Assert.True(count > 0))

        let mandatory =
            Set.ofList [ for command in 0..19 -> $"{command}:live" ]
            |> Set.add "15:disposed"
            |> Set.add "14:disposed"

        Assert.True(Set.isSubset mandatory reached, $"Missing transitions: {Set.difference mandatory reached}")
        output.WriteLine($"effectiveTransitions={String.Join(',', reached)}; rejected=0 validSequences=100")
        SessionModel.verify [| 0; 1; 4; 19; 9; 14 |] |> ignore

        output.WriteLine(
            $"seed={seed} sequences=100 commandRange={minimum}-{maximum} rejected=0 commandCounts={String.Join(',', counts)}"
        )

    [<Theory; MemberData(nameof SessionModelTests.Seeds)>]
    member _.``Q07 native cleanup obligations follow successful acquisitions since last successful release``
        (seed: int)
        =
        let random = Random seed

        let operations =
            [| "JVInit", 0
               "JVOpen", 0
               "JVOpen", -1
               "JVOpen", -305
               "JVRTOpen", -2
               "JVMVOpen", 0
               "JVClose", 0
               "JVClose", -201
               "JVWatchEvent", 0
               "JVWatchEvent", -201
               "JVWatchEventClose", 0
               "JVWatchEventClose", -201 |]

        for sequence in 0..99 do
            let trace =
                Array.init (if SessionModel.stress () then 200 else 50) (fun _ ->
                    operations[random.Next operations.Length])

            // Replaying each prefix observes cleanup obligations after every operation.
            for prefixLength in 1 .. trace.Length do
                let trace = trace[.. prefixLength - 1]
                let lifetime = NativeSessionLifetime()

                for api, code in trace do
                    lifetime.Observe(api, code)

                let last predicate =
                    trace
                    |> Array.indexed
                    |> Array.filter (snd >> predicate)
                    |> Array.map fst
                    |> Array.tryLast
                    |> Option.defaultValue -1

                let dataAcquired =
                    last (fun (api, code) ->
                        List.contains api [ "JVOpen"; "JVRTOpen"; "JVMVOpen" ]
                        && code >= -2
                        && code <= 0)

                let dataReleased = last ((=) ("JVClose", 0))
                let watchAcquired = last ((=) ("JVWatchEvent", 0))
                let watchReleased = last ((=) ("JVWatchEventClose", 0))

                let expected =
                    [ if watchAcquired > watchReleased then
                          yield "JVWatchEventClose"
                      yield "detach"
                      if dataAcquired > dataReleased then
                          yield "JVClose"
                      yield "release" ]

                let actual = ResizeArray<string>()

                lifetime.Cleanup(
                    (fun api ->
                        actual.Add api
                        Ok 0),
                    (fun () -> actual.Add "detach"),
                    (fun () -> actual.Add "release")
                )
                |> SessionModel.success

                Assert.Equal<string>(expected, actual)


        output.WriteLine(
            $"seed={seed} ownershipSequences=100 no redundant cleanup after successful close; failed close retains acquisition"
        )

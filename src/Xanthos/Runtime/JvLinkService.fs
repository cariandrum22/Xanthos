namespace Xanthos.Runtime

open System
open System.Threading
open System.Threading.Tasks
open System.Collections.Generic
open System.Collections.Concurrent
open Xanthos.Core
open Xanthos.Core.Serialization
open Xanthos.Core.Errors
open Xanthos.Interop
open Xanthos.Runtime.ReadRetryPolicy

type ResultBuilder() =
    member _.Bind(m, f) = Result.bind f m
    member _.Return x = Ok x
    member _.ReturnFrom m = m

type ComRetryPolicy =
    { Timeout: TimeSpan
      MaxRetries: int
      Backoff: int -> TimeSpan }

type IWaitScheduler =
    abstract member Sleep: TimeSpan -> unit
    abstract member SleepWithCancellation: TimeSpan * CancellationToken -> unit
    abstract member Delay: TimeSpan * CancellationToken -> Task

type WaitScheduler =
    static member Default =
        { new IWaitScheduler with
            member _.Sleep span = Thread.Sleep span

            member _.SleepWithCancellation(span, token) =
                // Sleep in small chunks to allow prompt cancellation
                let chunkMs = 50
                let mutable remaining = int span.TotalMilliseconds

                while remaining > 0 && not token.IsCancellationRequested do
                    let sleepTime = min remaining chunkMs
                    Thread.Sleep sleepTime
                    remaining <- remaining - sleepTime

                token.ThrowIfCancellationRequested()

            member _.Delay(span, token) = Task.Delay(span, token) }

type PollingPolicy =
    { DownloadPendingDelay: TimeSpan
      DefaultPollInterval: TimeSpan }

/// <summary>
/// High-level service for interacting with JV-Link COM API.
/// </summary>
/// <remarks>
/// <para>
/// <b>Thread Safety:</b> This class is NOT thread-safe and enforces single-operation access
/// at runtime using a reentrancy guard. Concurrent calls to long-running methods
/// (FetchPayloads, FetchPayloadsWithBytes, StreamRealtimePayloads) will fail immediately
/// with an <c>InvalidState</c> error rather than causing undefined behavior.
/// </para>
/// <para>
/// If you need to use JvLinkService in a multi-threaded environment (e.g., ASP.NET Core with
/// concurrent HTTP requests), either:
/// <list type="bullet">
///   <item>Create a separate JvLinkService instance per request (scoped lifetime)</item>
///   <item>Use a dedicated background service with a queue for JV-Link operations</item>
/// </list>
/// </para>
/// </remarks>
type JvLinkService
    (
        client: IJvLinkClient,
        config: JvLinkConfig,
        ?logger: TraceLogger,
        ?waitScheduler: IWaitScheduler,
        ?pollingPolicy: PollingPolicy,
        ?retryConfig: ComRetryConfig,
        ?eventQueueCapacity: int
    ) =

    let logger = defaultArg logger TraceLogger.silent
    let scheduler = defaultArg waitScheduler WaitScheduler.Default

    let polling =
        defaultArg
            pollingPolicy
            { DownloadPendingDelay = TimeSpan.FromMilliseconds 500.
              DefaultPollInterval = TimeSpan.FromMilliseconds 500. }

    // Use provided retry config or default values
    let retryConf = defaultArg retryConfig ComRetryConfig.Default

    let result = ResultBuilder()

    // Mutable config to track runtime changes to SavePath/ServiceKey
    let mutable currentConfig = config

    let describeError = Errors.toString

    /// Reentrancy guard to prevent concurrent access to JvLinkService.
    /// SemaphoreSlim(1, 1) acts as a mutex - only one operation can proceed at a time.
    let operationLock = new SemaphoreSlim(1, 1)

    /// Guards an operation against concurrent access.
    /// Uses Wait(0) for immediate (non-blocking) acquisition attempt.
    /// If the semaphore cannot be acquired immediately, another operation is in progress
    /// and we return an error rather than blocking.
    let guardOperation (operationName: string) (f: unit -> Result<'a, XanthosError>) : Result<'a, XanthosError> =
        if not (operationLock.Wait(0)) then
            logger.Warn(
                $"Rejecting {operationName} - another operation is already in progress. "
                + "JvLinkService does not support concurrent access."
            )

            Error(
                InteropError(
                    InvalidState
                        $"Cannot start {operationName} - another operation is already in progress. JvLinkService does not support concurrent access. Create separate JvLinkService instances for concurrent operations."
                )
            )
        else
            try
                f ()
            finally
                operationLock.Release() |> ignore

    /// Guards a streaming operation against concurrent access.
    /// Acquires the lock at the start of enumeration and releases it when the sequence completes.
    /// If the lock cannot be acquired immediately, yields an error and terminates.
    let guardOperationSeq
        (operationName: string)
        (f: unit -> seq<Result<'a, XanthosError>>)
        : seq<Result<'a, XanthosError>> =
        seq {
            if not (operationLock.Wait(0)) then
                logger.Warn(
                    $"Rejecting {operationName} - another operation is already in progress. "
                    + "JvLinkService does not support concurrent access."
                )

                yield
                    Error(
                        InteropError(
                            InvalidState
                                $"Cannot start {operationName} - another operation is already in progress. JvLinkService does not support concurrent access. Create separate JvLinkService instances for concurrent operations."
                        )
                    )
            else
                try
                    yield! f ()
                finally
                    operationLock.Release() |> ignore
        }

    /// Attempts to acquire the operation lock without blocking.
    /// Returns true if acquired, false otherwise.
    let tryAcquireLock (operationName: string) : bool =
        if operationLock.Wait(0) then
            true
        else
            logger.Warn(
                $"Rejecting {operationName} - another operation is already in progress. "
                + "JvLinkService does not support concurrent access."
            )

            false

    /// Releases the operation lock. Safe to call even if lock was not held.
    let releaseLock () = operationLock.Release() |> ignore

    /// Creates a XanthosError for concurrent access rejection.
    let concurrentAccessError (operationName: string) : XanthosError =
        InteropError(
            InvalidState
                $"Cannot start {operationName} - another operation is already in progress. JvLinkService does not support concurrent access. Create separate JvLinkService instances for concurrent operations."
        )

    // Build retry policy from configurable values
    let defaultRetryPolicy =
        { Timeout = retryConf.Timeout
          MaxRetries = retryConf.MaxRetries
          Backoff = fun attempt -> TimeSpan.FromMilliseconds(250. * float (attempt + 1)) }

    /// Dispatcher exposed by IJvLinkClient (COM path only). Stub clients do not provide one.
    let comDispatcher =
        match client with
        | :? IComDispatchProvider as provider -> Some(provider.Dispatcher)
        | _ -> None

    /// Tracks whether this service instance has encountered an unrecoverable error (e.g., STA timeout).
    /// Once poisoned, all COM operations are rejected to prevent hanging on a blocked STA thread.
    let mutable poisoned = false
    let mutable poisonReason: string option = None

    /// Executes a COM call with timeout and retry control.
    /// Dispatches to STA thread if the COM client provides a dispatcher; otherwise runs via Task.Run (for stubs).
    /// Returns immediately with an error if the service is poisoned.
    ///
    /// IMPORTANT: This function blocks the calling thread while waiting for the COM operation to complete.
    /// Even though it uses Task.WhenAny internally for timeout control, the .Result property is used
    /// to synchronously wait for completion. The *Async API methods (StreamPayloadsAsync, StreamRealtimeAsync)
    /// provide IAsyncEnumerable semantics with cancellation between iterations, but individual COM calls
    /// within each iteration still block.
    ///
    /// Note: This function does not accept a CancellationToken because COM calls cannot be interrupted
    /// mid-execution. The internal timeout provides the only mechanism to abort a hung COM call, which
    /// results in the service becoming poisoned. Callers should check cancellation between COM calls
    /// rather than expecting cancellation to interrupt an in-progress COM operation.
    let executeCom policy name (call: unit -> Result<'a, ComError>) : Result<'a, ComError> =
        // Fail fast if poisoned
        if poisoned then
            let reason = defaultArg poisonReason "unknown"
            logger.Warn($"Rejecting {name} - service is poisoned. Reason: {reason}")
            Error(CommunicationFailure(-999, $"Service is poisoned - {reason}"))
        else

            let rec loop attemptNo totalDelayMs =
                let work () =
                    try
                        call ()
                    with ex ->
                        logger.Error($"JV-Link call {name} threw {ex.GetType().Name}: {ex.Message}")
                        Error(Unexpected ex.Message)

                let task: Task<Result<'a, ComError>> =
                    match comDispatcher with
                    | Some dispatcher -> dispatcher.InvokeAsync(name, work)
                    | None -> Task.Run<Result<'a, ComError>>(fun () -> work ())

                let timeoutTask = Task.Delay(policy.Timeout)
                let completedTask = Task.WhenAny(task, timeoutTask).Result

                if obj.ReferenceEquals(completedTask, task) then
                    let result =
                        try
                            task.Result
                        with ex ->
                            logger.Error(
                                $"JV-Link call {name} result retrieval failed: {ex.GetType().Name} {ex.Message}"
                            )

                            Error(Unexpected ex.Message)

                    match result with
                    | Ok value -> Ok value
                    | Error(CommunicationFailure(code, message) as err) when attemptNo < policy.MaxRetries ->
                        logger.Warn(
                            $"JV-Link call {name} failed with code {code}: {message}. Retrying ({attemptNo + 2}/{policy.MaxRetries + 1})."
                        )

                        let delay = policy.Backoff attemptNo
                        scheduler.Sleep delay
                        loop (attemptNo + 1) (totalDelayMs + int delay.TotalMilliseconds)
                    | Error err ->
                        if attemptNo > 0 then
                            logger.Error(
                                $"JV-Link call {name} failed after {attemptNo + 1} attempt(s), cumulative retry delay={totalDelayMs} ms. Last error={describeError (InteropError err)}"
                            )

                        Error err
                else
                    // CRITICAL: The original COM call is still running on the STA thread.
                    // All COM calls (including Cancel/Close) go through the same STA dispatcher
                    // via Dispatcher.Invoke. If the original call is truly hung, any subsequent
                    // calls will queue behind it and never execute.
                    //
                    // There is NO way to recover a hung STA thread without terminating it,
                    // which would leave COM resources in an undefined state.
                    //
                    // The ONLY safe recovery is to dispose the entire JvLinkService/client
                    // and create a new instance.
                    let reason =
                        $"JV-Link call {name} timed out after {policy.Timeout.TotalMilliseconds} ms"

                    poisoned <- true
                    poisonReason <- Some reason

                    logger.Error(
                        $"{reason}. The STA thread is blocked. This JvLinkService instance is now unusable - "
                        + "dispose it and create a new instance to recover."
                    )

                    Error(
                        CommunicationFailure(
                            -999,
                            $"Timeout after {int policy.Timeout.TotalMilliseconds} ms - service must be recreated"
                        )
                    )

            loop 0 0

    let runCom name (call: unit -> Result<'a, ComError>) : Result<'a, XanthosError> =
        executeCom defaultRetryPolicy name call |> Errors.mapComError

    let runComAsync name (call: unit -> Result<'a, ComError>) : Task<Result<'a, XanthosError>> =
        Task.FromResult(runCom name call)

    /// Closes the JV-Link session safely.
    /// If the service is poisoned (due to a prior timeout), this does nothing
    /// to avoid hanging on the blocked STA thread.
    let closeQuietly () =
        if poisoned then
            let reason = defaultArg poisonReason "unknown"

            logger.Warn(
                "Skipping JVClose - service is poisoned due to prior timeout. "
                + $"Reason: {reason}. "
                + "Dispose this service and create a new instance."
            )
        else
            try
                client.Close()
            with ex ->
                logger.Warn($"Failed to close JV-Link session gracefully: {ex.Message}")

    /// Retry policy for read/skip operations.
    /// Uses configurable timeout for hung detection but no retries (handled by readAll's retry logic).
    let readRetryPolicy =
        { Timeout = retryConf.Timeout
          MaxRetries = 0
          Backoff = fun _ -> TimeSpan.Zero }

    /// Executes JVRead with timeout protection.
    let readWithTimeout () : Result<JvReadOutcome, ComError> =
        executeCom readRetryPolicy "JVRead" (fun () -> client.Read())

    /// Executes JVSkip with timeout protection.
    let skipWithTimeout () : Result<unit, ComError> =
        executeCom readRetryPolicy "JVSkip" (fun () -> client.Skip())

    /// Performs JV-Link initialisation with timeout protection.
    /// Uses executeCom to ensure all COM calls (Init, SetSavePathDirect, SetServiceKeyDirect) are protected.
    let initialize () : Result<unit, XanthosError> =
        // Step 1: JVInit
        let initResult =
            executeCom defaultRetryPolicy "JVInit" (fun () -> client.Init currentConfig.Sid)

        match initResult |> Errors.mapComError with
        | Error e -> Error e
        | Ok() ->
            // Step 2: Apply SavePath if specified
            let savePathResult =
                match currentConfig.SavePath with
                | Some path ->
                    let result =
                        executeCom defaultRetryPolicy "SetSavePathDirect" (fun () -> client.SetSavePathDirect path)

                    match result |> Errors.mapComError with
                    | Error e -> Error e
                    | Ok() ->
                        // Also update the cached property for consistency
                        try
                            client.SavePath <- path
                        with _ ->
                            ()

                        Ok()
                | None -> Ok()

            match savePathResult with
            | Error e -> Error e
            | Ok() ->
                // Step 3: Apply ServiceKey if specified
                match currentConfig.ServiceKey with
                | Some key ->
                    let result =
                        executeCom defaultRetryPolicy "SetServiceKeyDirect" (fun () -> client.SetServiceKeyDirect key)

                    match result |> Errors.mapComError with
                    | Error e -> Error e
                    | Ok() ->
                        // Also update the cached property for consistency
                        try
                            client.ServiceKey <- key
                        with _ ->
                            ()

                        Ok()
                | None -> Ok()

    let openSessionFor request =
        match runCom "JVOpen" (fun () -> client.Open request) with
        | Ok openResult ->
            if openResult.HasData then
                logger.Info(
                    $"JV-Link opened dataspec {request.Spec} (files={openResult.ReadCount}, downloads={openResult.DownloadCount}, lastTimestamp={openResult.LastFileTimestamp})."
                )
            else
                logger.Info($"JV-Link opened dataspec {request.Spec} (no matching data).")

            Ok openResult.HasData
        | Error err ->
            logger.Error($"JV-Link open failed for dataspec {request.Spec}: {describeError err}")
            Error err

    let openRealtimeSession spec key =
        match runCom "JVRTOpen" (fun () -> client.OpenRealtime(spec, key)) with
        | Ok openResult ->
            if openResult.HasData then
                logger.Info($"JV-Link opened realtime dataspec {spec} with key '{key}'.")
            else
                logger.Info($"JV-Link opened realtime dataspec {spec} with key '{key}' (no matching data).")

            Ok openResult.HasData
        | Error err ->
            logger.Error($"JV-Link realtime open failed for dataspec {spec} key '{key}': {describeError err}")
            Error err

    /// Determines whether a COM error is recoverable (i.e., we can retry and/or skip to continue reading).
    let isRecoverableReadError (err: ComError) =
        match err with
        | CommunicationFailure(code, _) ->
            // -402: Downloaded file has invalid size (file corruption)
            // -403: Downloaded file cannot be opened
            // -404: Downloaded file is corrupted
            code = -402 || code = -403 || code = -404
        | _ -> false

    /// Maximum number of retries for recoverable read errors before skipping.
    let readRetryMax = 2

    /// Backoff delay between read retries.
    let readRetryBackoff attempt =
        TimeSpan.FromMilliseconds(500. * float (attempt + 1))

    let rec readAll (token: CancellationToken) acc (count: int64) =
        /// Attempts to read with retries for recoverable errors.
        let rec tryRead retryAttempt =
            token.ThrowIfCancellationRequested()

            match readWithTimeout () with
            | Error err when isRecoverableReadError err && retryAttempt < readRetryMax ->
                // Retry with backoff for recoverable errors
                let delay = readRetryBackoff retryAttempt

                logger.Warn(
                    $"JV-Link read error (attempt {retryAttempt + 1}/{readRetryMax + 1}): {describeError (InteropError err)}. Retrying in {delay.TotalMilliseconds} ms."
                )

                scheduler.SleepWithCancellation(delay, token)
                tryRead (retryAttempt + 1)
            | other -> other

        match tryRead 0 with
        | Error err when isRecoverableReadError err ->
            // Max retries exhausted - skip and continue with next file
            logger.Warn(
                $"JV-Link read error after {readRetryMax + 1} attempts: {describeError (InteropError err)}. Skipping current file."
            )

            match skipWithTimeout () with
            | Ok() ->
                logger.Debug("JV-Link skip succeeded; continuing with next file.")
                readAll token acc count
            | Error skipErr ->
                // Skip failed - return error to signal incomplete data to caller.
                // This prevents callers from silently missing data without knowing about the skip failure.
                logger.Error(
                    $"JV-Link skip failed: {describeError (InteropError skipErr)}. Discarding {List.length acc} partial payloads."
                )

                Error(InteropError skipErr)
        | Error err ->
            // Fatal error - always return Error to signal incomplete data to caller.
            // This prevents callers from silently missing data without knowing about the error.
            logger.Error(
                $"JV-Link read failed: {describeError (InteropError err)}. Discarding {List.length acc} partial payloads."
            )

            Error(InteropError err)
        | Ok EndOfStream ->
            logger.Info($"JV-Link returned end-of-stream after {List.length acc} payloads.")
            Ok(List.rev acc, count)
        | Ok FileBoundary ->
            match ReadRetryPolicy.defaultPolicy FileBoundary with
            | Skip ->
                logger.Debug("JV-Link reported a file boundary (-1); continuing with the next file.")
                readAll token acc count
            | RetryAfter delay ->
                logger.Debug($"JV-Link reported file boundary; retrying after {delay.TotalMilliseconds} ms.")
                scheduler.SleepWithCancellation(delay, token)
                readAll token acc count
            | Abort -> Error(InteropError(InvalidState "JV-Link reported a file boundary and retry policy aborted."))
        | Ok DownloadPending ->
            match ReadRetryPolicy.defaultPolicy DownloadPending with
            | RetryAfter delay ->
                logger.Info(
                    $"JV-Link is still downloading files (code=-3). Waiting {delay.TotalMilliseconds} ms before retrying JVRead."
                )

                scheduler.SleepWithCancellation(delay, token)
                readAll token acc count
            | Skip ->
                logger.Debug("Retry policy instructed to skip download pending state.")
                readAll token acc count
            | Abort ->
                Error(InteropError(CommunicationFailure(-3, "JV-Link download pending state aborted by policy.")))
        | Ok(Payload payload) ->
            if payload.Data.Length = 0 then
                logger.Debug("JV-Link returned zero-length payload; skipping.")
                readAll token acc count
            else
                logger.Debug($"JV-Link returned payload of {payload.Data.Length} bytes.")
                readAll token (payload :: acc) (count + int64 payload.Data.Length)

    let watchEventStream = Event<Result<WatchEvent, XanthosError>>()
    /// Atomic flag for watch state:
    /// 0 = stopped (not watching)
    /// 1 = starting (transitioning, COM subscription pending)
    /// 2 = running (fully started, COM subscription active)
    /// Uses Interlocked for thread-safe state transitions.
    let watchingFlag = ref 0

    [<Literal>]
    let WatchState_Stopped = 0

    [<Literal>]
    let WatchState_Starting = 1

    [<Literal>]
    let WatchState_Running = 2

    let mutable disposed = false

    // Event dispatcher: uses a dedicated background thread with a FIFO queue to avoid
    // blocking the STA message pump while preserving event order.
    // Note: BlockingCollection cannot be reused after CompleteAdding(), so we create
    // a new instance each time StartWatchEvents is called.
    // BoundedCapacity limits memory growth if consumer is slow; events are dropped with notification when full.
    let eventQueueCapacity = defaultArg eventQueueCapacity 10_000
    let mutable eventQueue = new BlockingCollection<string>(eventQueueCapacity)
    let mutable eventConsumerThread: Thread option = None
    /// Counter for overflow events that need to be reported by the consumer thread.
    /// This avoids triggering the event stream directly from the STA thread callback.
    let overflowCount = ref 0

    /// Processes events from the queue in order on a dedicated background thread.
    let startEventConsumer () =
        // Reset overflow counter from any previous session
        System.Threading.Interlocked.Exchange(overflowCount, 0) |> ignore

        // Create a fresh queue - BlockingCollection cannot be reused after CompleteAdding()
        if eventQueue.IsAddingCompleted then
            eventQueue.Dispose()
            eventQueue <- new BlockingCollection<string>(eventQueueCapacity)

        let currentQueue = eventQueue // Capture for closure

        let consumer =
            Thread(fun () ->
                try
                    // GetConsumingEnumerable blocks until items are available or collection is completed
                    for key in currentQueue.GetConsumingEnumerable() do
                        // Check for overflow events that accumulated while we were blocked
                        // Exchange resets the counter and returns the accumulated count
                        let overflowEvents = System.Threading.Interlocked.Exchange(overflowCount, 0)

                        if overflowEvents > 0 then
                            try
                                watchEventStream.Trigger(Error(EventQueueOverflow overflowEvents))
                            with subscriberEx ->
                                logger.Error(
                                    $"WatchEvent subscriber threw exception on overflow: {subscriberEx.Message}"
                                )

                        // Parse the event in a protected block - parsing errors become Error results
                        let parseResult =
                            try
                                Ok(Serialization.parseWatchEvent key)
                            with parseEx ->
                                logger.Error($"WatchEvent key parsing failed: {parseEx.Message}")
                                Error(InteropError(Unexpected parseEx.Message))

                        // Trigger subscribers in a separate protected block
                        // Subscriber exceptions are logged but NOT propagated
                        try
                            match parseResult with
                            | Ok parsed -> watchEventStream.Trigger(Ok parsed)
                            | Error err -> watchEventStream.Trigger(Error err)
                        with subscriberEx ->
                            logger.Error($"WatchEvent subscriber threw exception: {subscriberEx.Message}")
                with
                | :? InvalidOperationException -> () // Collection was completed
                | ex -> logger.Error($"WatchEvent consumer thread failed: {ex.Message}"))

        consumer.IsBackground <- true
        consumer.Name <- "Xanthos-WatchEventConsumer"
        consumer.Start()
        eventConsumerThread <- Some consumer

    /// Stops the event consumer thread by completing the queue.
    let stopEventConsumer () =
        // Complete adding to signal the consumer to exit
        if not eventQueue.IsAddingCompleted then
            eventQueue.CompleteAdding()

        // Wait for consumer to finish (with timeout to avoid hanging)
        match eventConsumerThread with
        | Some t when t.IsAlive -> t.Join(TimeSpan.FromSeconds 2.) |> ignore
        | _ -> ()

        eventConsumerThread <- None

    let withDownloadSession openAction work emptyResult =
        match initialize () with
        | Error err -> Error err
        | Ok() ->
            match openAction () with
            | Error err -> Error err
            | Ok hasData ->
                // JVOpen/JVRTOpen opens a session regardless of whether data exists.
                // We MUST call JVClose after a successful open, even on exception.
                try
                    try
                        if hasData then
                            work ()
                        else
                            // No data available - return empty result without reading
                            Ok emptyResult
                    with :? OperationCanceledException ->
                        // Convert OCE to Cancelled error to maintain the API contract
                        // of always returning Result<'T, XanthosError>
                        Error Cancelled
                finally
                    closeQuietly ()

    let withInitialisation work =
        result {
            do! initialize ()
            return! work ()
        }

    /// <summary>
    /// Combines guardOperation with withInitialisation to ensure non-reentrant access.
    /// This prevents withInitialisation callers from queuing on the STA dispatcher
    /// when a long-running operation (FetchPayloads, streaming) is in progress.
    /// According to JV-Link spec, APIs like JVMVCheck/JVMVPlay cannot be used during JVOpen/JVRTOpen/JVMVOpen.
    /// </summary>
    let guardedWithInitialisation operationName work =
        guardOperation operationName (fun () -> withInitialisation work)

    /// <summary>
    /// Fetches all available payloads for the specified request, returning them as a materialised list.
    /// </summary>
    member _.FetchPayloads
        (request: JvOpenRequest, ?cancellationToken: CancellationToken)
        : Result<JvPayload list, XanthosError> =
        guardOperation "FetchPayloads" (fun () ->
            let token = defaultArg cancellationToken CancellationToken.None

            withDownloadSession (fun () -> openSessionFor request) (fun () -> readAll token [] 0L) ([], 0L)
            |> Result.map fst)

    /// <summary>
    /// Fetches payloads along with the total byte count transferred during the request.
    /// </summary>
    member _.FetchPayloadsWithBytes
        (request: JvOpenRequest, ?cancellationToken: CancellationToken)
        : Result<JvPayload list * int64, XanthosError> =
        guardOperation "FetchPayloadsWithBytes" (fun () ->
            let token = defaultArg cancellationToken CancellationToken.None
            withDownloadSession (fun () -> openSessionFor request) (fun () -> readAll token [] 0L) ([], 0L))

    member this.FetchPayloads
        (spec: string, fromTime: DateTime option, ?openOption: int, ?cancellationToken: CancellationToken)
        =
        match Validation.normalizeDataspec spec with
        | Error err -> Error err
        | Ok normalizedSpec ->
            match fromTime with
            | None -> Error(ValidationError "JVOpen requires fromTime (yyyyMMddHHmmss).")
            | Some ts ->
                let optionValue = defaultArg openOption 1

                this.FetchPayloads(
                    { Spec = normalizedSpec
                      FromTime = ts
                      Option = optionValue },
                    ?cancellationToken = cancellationToken
                )

    member this.FetchPayloadsWithBytes
        (spec: string, fromTime: DateTime option, ?openOption: int, ?cancellationToken: CancellationToken)
        =
        match Validation.normalizeDataspec spec with
        | Error err -> Error err
        | Ok normalizedSpec ->
            match fromTime with
            | None -> Error(ValidationError "JVOpen requires fromTime (yyyyMMddHHmmss).")
            | Some ts ->
                let optionValue = defaultArg openOption 1

                this.FetchPayloadsWithBytes(
                    { Spec = normalizedSpec
                      FromTime = ts
                      Option = optionValue },
                    ?cancellationToken = cancellationToken
                )

    member this.FetchPayloadsWithSize(request: JvOpenRequest, ?cancellationToken: CancellationToken) =
        this.FetchPayloadsWithBytes(request, ?cancellationToken = cancellationToken)

    member this.FetchPayloadsWithSize
        (spec: string, fromTime: DateTime option, ?openOption: int, ?cancellationToken: CancellationToken)
        =
        this.FetchPayloadsWithBytes(spec, fromTime, ?openOption = openOption, ?cancellationToken = cancellationToken)

    /// <summary>
    /// Lazily streams payloads from a JV-Link session (`JVOpen`) until EndOfStream.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unlike <see cref="FetchPayloads"/>, this method yields payloads one at a time without
    /// accumulating them in memory. Use this for large data requests where memory is a concern.
    /// </para>
    /// <para>
    /// Each element in the sequence is a <c>Result&lt;JvPayload, XanthosError&gt;</c>. Callers should
    /// check each result and may choose to stop enumeration early on errors.
    /// </para>
    /// </remarks>
    /// <param name="request">The JV-Link open request specifying dataspec and time range.</param>
    member _.StreamPayloads(request: JvOpenRequest) : seq<Result<JvPayload, XanthosError>> =
        guardOperationSeq "StreamPayloads" (fun () ->
            seq {
                match initialize () with
                | Error err -> yield Error err
                | Ok() ->
                    match openSessionFor request with
                    | Error err -> yield Error err
                    | Ok hasData ->
                        // Session is now open - MUST close in finally block
                        try
                            if hasData then
                                // Retry helper for recoverable errors (same logic as readAll)
                                let rec tryReadWithRetry retryAttempt =
                                    match readWithTimeout () with
                                    | Error err when isRecoverableReadError err && retryAttempt < readRetryMax ->
                                        let delay = readRetryBackoff retryAttempt

                                        logger.Warn(
                                            $"JV-Link stream read error (attempt {retryAttempt + 1}/{readRetryMax + 1}): {describeError (InteropError err)}. Retrying in {delay.TotalMilliseconds} ms."
                                        )

                                        scheduler.Sleep(delay)
                                        tryReadWithRetry (retryAttempt + 1)
                                    | other -> other

                                let rec loop () =
                                    seq {
                                        match tryReadWithRetry 0 with
                                        | Ok EndOfStream -> () // Terminate on EndOfStream
                                        | Ok FileBoundary -> yield! loop ()
                                        | Ok DownloadPending ->
                                            scheduler.Sleep(polling.DownloadPendingDelay)
                                            yield! loop ()
                                        | Ok(Payload payload) ->
                                            yield Ok payload
                                            yield! loop ()
                                        | Error err when isRecoverableReadError err ->
                                            // Max retries exhausted - skip and continue
                                            logger.Warn(
                                                $"JV-Link stream read error after {readRetryMax + 1} attempts: {describeError (InteropError err)}. Skipping current file."
                                            )

                                            match skipWithTimeout () with
                                            | Ok() ->
                                                logger.Debug("JV-Link skip succeeded; continuing stream.")
                                                yield! loop ()
                                            | Error skipErr ->
                                                logger.Error(
                                                    $"JV-Link skip failed: {describeError (InteropError skipErr)}. Terminating stream."
                                                )

                                                yield Error(InteropError skipErr)
                                        | Error err ->
                                            // Fatal error - terminate stream
                                            logger.Error(
                                                $"JV-Link stream read failed: {describeError (InteropError err)}. Terminating stream."
                                            )

                                            yield Error(InteropError err)
                                    }

                                yield! loop ()
                        // else: no data available, yield nothing but still close
                        finally
                            closeQuietly ()
            })

    /// <summary>
    /// Lazily streams payloads from a JV-Link session (`JVOpen`) until EndOfStream.
    /// </summary>
    member this.StreamPayloads
        (spec: string, fromTime: DateTime option, ?openOption: int)
        : seq<Result<JvPayload, XanthosError>> =
        match Validation.normalizeDataspec spec with
        | Error err -> seq { yield Error err }
        | Ok normalizedSpec ->
            match fromTime with
            | None -> seq { yield Error(ValidationError "JVOpen requires fromTime (yyyyMMddHHmmss).") }
            | Some ts ->
                let optionValue = defaultArg openOption 1

                this.StreamPayloads(
                    { Spec = normalizedSpec
                      FromTime = ts
                      Option = optionValue }
                )

    /// <summary>
    /// Asynchronously streams payloads from a JV-Link session (`JVOpen`), with cancellation support.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unlike <see cref="FetchPayloads"/>, this method yields payloads one at a time without
    /// accumulating them in memory. Use this for large data requests where memory is a concern.
    /// </para>
    /// <para>
    /// <b>Cancellation behavior:</b> The cancellation token is checked between COM operations
    /// and affects polling delays. However, individual COM calls (JVRead, etc.) cannot be
    /// interrupted mid-execution due to COM threading constraints.
    /// </para>
    /// </remarks>
    /// <param name="request">The JV-Link open request specifying dataspec and time range.</param>
    /// <param name="pollInterval">Interval between polls when waiting for downloads (default: 500ms)</param>
    /// <param name="cancellationToken">Token to request cancellation of the stream</param>
    member _.StreamPayloadsAsync
        (request: JvOpenRequest, ?pollInterval: TimeSpan, ?cancellationToken: CancellationToken)
        : IAsyncEnumerable<Result<JvPayload, XanthosError>> =
        let pollDelay = defaultArg pollInterval polling.DefaultPollInterval
        let externalToken = defaultArg cancellationToken CancellationToken.None

        { new IAsyncEnumerable<Result<JvPayload, XanthosError>> with
            member _.GetAsyncEnumerator(ct) =
                let linkedCts = CancellationTokenSource.CreateLinkedTokenSource(externalToken, ct)
                let token = linkedCts.Token
                let mutable current = Unchecked.defaultof<Result<JvPayload, XanthosError>>
                let mutable opened = false
                let mutable finished = false
                let mutable lockHeld = false

                let closeSession () =
                    if opened then
                        opened <- false
                        closeQuietly ()

                { new IAsyncEnumerator<Result<JvPayload, XanthosError>> with
                    member _.Current = current

                    member _.DisposeAsync() =
                        closeSession ()
                        linkedCts.Dispose()

                        if lockHeld then
                            lockHeld <- false
                            releaseLock ()

                        ValueTask.CompletedTask

                    member _.MoveNextAsync() =
                        let moveNext () =
                            task {
                                if finished then
                                    return false
                                else
                                    let mutable setupError: XanthosError option = None

                                    if not opened then
                                        // Acquire reentrancy guard before any COM operations
                                        if not lockHeld then
                                            if tryAcquireLock "StreamPayloadsAsync" then
                                                lockHeld <- true
                                            else
                                                setupError <- Some(concurrentAccessError "StreamPayloadsAsync")

                                        if setupError.IsNone then
                                            match initialize () with
                                            | Error err -> setupError <- Some err
                                            | Ok() ->
                                                match openSessionFor request with
                                                | Ok hasData ->
                                                    opened <- true

                                                    if not hasData then
                                                        finished <- true
                                                | Error err -> setupError <- Some err

                                    match setupError with
                                    | Some err ->
                                        current <- Error err
                                        finished <- true
                                        return true
                                    | None ->
                                        if finished then
                                            closeSession ()
                                            return false
                                        else
                                            let mutable delivered = false
                                            let mutable continuePolling = true
                                            let mutable cancelled = false

                                            let tryReadWithRetryAsync () =
                                                task {
                                                    let mutable result = readWithTimeout ()
                                                    let mutable retryAttempt = 0

                                                    let shouldRetry () =
                                                        match result with
                                                        | Error err when
                                                            isRecoverableReadError err && retryAttempt < readRetryMax
                                                            ->
                                                            true
                                                        | _ -> false

                                                    while shouldRetry () do
                                                        let delay = readRetryBackoff retryAttempt

                                                        let errForLog =
                                                            match result with
                                                            | Error e -> e
                                                            | _ -> Unexpected ""

                                                        let errMsg = describeError (InteropError errForLog)

                                                        logger.Warn(
                                                            $"JV-Link async stream read error (attempt {retryAttempt + 1}/{readRetryMax + 1}): {errMsg}. Retrying in {delay.TotalMilliseconds} ms."
                                                        )

                                                        do! scheduler.Delay(delay, token)
                                                        retryAttempt <- retryAttempt + 1
                                                        result <- readWithTimeout ()

                                                    return result
                                                }

                                            try
                                                while not delivered && continuePolling && not cancelled do
                                                    if token.IsCancellationRequested then
                                                        cancelled <- true
                                                    else
                                                        let! readResult = tryReadWithRetryAsync ()

                                                        match readResult with
                                                        | Ok(Payload payload) ->
                                                            current <- Ok payload
                                                            delivered <- true
                                                        | Ok FileBoundary -> ()
                                                        | Ok DownloadPending -> do! scheduler.Delay(pollDelay, token)
                                                        | Ok EndOfStream ->
                                                            finished <- true
                                                            continuePolling <- false
                                                            closeSession ()
                                                        | Error err when isRecoverableReadError err ->
                                                            logger.Warn(
                                                                $"JV-Link async stream read error after {readRetryMax + 1} attempts: {describeError (InteropError err)}. Skipping current file."
                                                            )

                                                            match skipWithTimeout () with
                                                            | Ok() ->
                                                                logger.Debug(
                                                                    "JV-Link skip succeeded; continuing async stream."
                                                                )
                                                            | Error skipErr ->
                                                                logger.Error(
                                                                    $"JV-Link skip failed: {describeError (InteropError skipErr)}. Terminating async stream."
                                                                )

                                                                current <- Error(InteropError skipErr)
                                                                finished <- true
                                                                delivered <- true
                                                                continuePolling <- false
                                                                closeSession ()
                                                        | Error err ->
                                                            logger.Error(
                                                                $"JV-Link async stream read failed: {describeError (InteropError err)}. Terminating async stream."
                                                            )

                                                            current <- Error(InteropError err)
                                                            finished <- true
                                                            delivered <- true
                                                            continuePolling <- false
                                                            closeSession ()
                                            with :? OperationCanceledException ->
                                                cancelled <- true

                                            if cancelled then
                                                closeSession ()
                                                return false
                                            else
                                                return delivered
                            }

                        ValueTask<bool>(moveNext ()) } }

    /// <summary>
    /// Asynchronously streams payloads from a JV-Link session (`JVOpen`), with cancellation support.
    /// </summary>
    member this.StreamPayloadsAsync
        (
            spec: string,
            fromTime: DateTime option,
            ?openOption: int,
            ?pollInterval: TimeSpan,
            ?cancellationToken: CancellationToken
        ) : IAsyncEnumerable<Result<JvPayload, XanthosError>> =
        // Helper to create an async enumerable that yields a single error
        let singleError (err: XanthosError) =
            { new IAsyncEnumerable<Result<JvPayload, XanthosError>> with
                member _.GetAsyncEnumerator(_ct) =
                    let mutable yielded = false

                    { new IAsyncEnumerator<Result<JvPayload, XanthosError>> with
                        member _.Current = Error err
                        member _.DisposeAsync() = ValueTask.CompletedTask

                        member _.MoveNextAsync() =
                            if yielded then
                                ValueTask<bool>(false)
                            else
                                yielded <- true
                                ValueTask<bool>(true) } }

        match Validation.normalizeDataspec spec with
        | Error err -> singleError err
        | Ok normalizedSpec ->
            match fromTime with
            | None -> singleError (ValidationError "JVOpen requires fromTime (yyyyMMddHHmmss).")
            | Some ts ->
                let optionValue = defaultArg openOption 1

                this.StreamPayloadsAsync(
                    { Spec = normalizedSpec
                      FromTime = ts
                      Option = optionValue },
                    ?pollInterval = pollInterval,
                    ?cancellationToken = cancellationToken
                )

    /// <summary>
    /// Lazily streams payloads from a real-time session (`JVRTOpen`) until EndOfStream.
    /// For continuous polling, use `StreamRealtimeAsync` which polls until cancelled.
    /// </summary>
    /// <param name="spec">Data spec ID (4 characters, e.g., "0B12", "0B11", "0B16")</param>
    /// <param name="key">Request key: "YYYYMMDDJJKKHHRR" (race), "YYYYMMDD" (daily), or WatchEvent parameter</param>
    member _.StreamRealtimePayloads(spec: string, key: string) : seq<Result<JvPayload, XanthosError>> =
        guardOperationSeq "StreamRealtimePayloads" (fun () ->
            seq {
                match Validation.normalizeDataspec spec, Validation.normalizeRealtimeKey key with
                | Error err, _ -> yield Error err
                | _, Error err -> yield Error err
                | Ok normalizedSpec, Ok normalizedKey ->
                    match initialize () with
                    | Error err -> yield Error err
                    | Ok() ->
                        match openRealtimeSession normalizedSpec normalizedKey with
                        | Error err -> yield Error err
                        | Ok hasData ->
                            // Session is now open - MUST close in finally block
                            try
                                if hasData then
                                    // Retry helper for recoverable errors (same logic as readAll)
                                    let rec tryReadWithRetry retryAttempt =
                                        match readWithTimeout () with
                                        | Error err when isRecoverableReadError err && retryAttempt < readRetryMax ->
                                            let delay = readRetryBackoff retryAttempt

                                            logger.Warn(
                                                $"JV-Link stream read error (attempt {retryAttempt + 1}/{readRetryMax + 1}): {describeError (InteropError err)}. Retrying in {delay.TotalMilliseconds} ms."
                                            )

                                            scheduler.Sleep(delay)
                                            tryReadWithRetry (retryAttempt + 1)
                                        | other -> other

                                    let rec loop () =
                                        seq {
                                            match tryReadWithRetry 0 with
                                            | Ok EndOfStream -> () // Terminate on EndOfStream
                                            | Ok FileBoundary -> yield! loop ()
                                            | Ok DownloadPending ->
                                                scheduler.Sleep(polling.DownloadPendingDelay)
                                                yield! loop ()
                                            | Ok(Payload payload) ->
                                                yield Ok payload
                                                yield! loop ()
                                            | Error err when isRecoverableReadError err ->
                                                // Max retries exhausted - skip and continue
                                                logger.Warn(
                                                    $"JV-Link stream read error after {readRetryMax + 1} attempts: {describeError (InteropError err)}. Skipping current file."
                                                )

                                                match skipWithTimeout () with
                                                | Ok() ->
                                                    logger.Debug("JV-Link skip succeeded; continuing stream.")
                                                    yield! loop ()
                                                | Error skipErr ->
                                                    logger.Error(
                                                        $"JV-Link skip failed: {describeError (InteropError skipErr)}. Terminating stream."
                                                    )

                                                    yield Error(InteropError skipErr)
                                            | Error err ->
                                                // Fatal error - terminate stream
                                                logger.Error(
                                                    $"JV-Link stream read failed: {describeError (InteropError err)}. Terminating stream."
                                                )

                                                yield Error(InteropError err)
                                        }

                                    yield! loop ()
                            // else: no data available, yield nothing but still close
                            finally
                                closeQuietly ()
            })

    /// <summary>
    /// Asynchronously streams payloads from a real-time session (`JVRTOpen`), polling until cancelled.
    /// </summary>
    /// <param name="spec">Data spec ID (4 characters, e.g., "0B12", "0B11", "0B16")</param>
    /// <param name="key">Request key: "YYYYMMDDJJKKHHRR" (race), "YYYYMMDD" (daily), or WatchEvent parameter</param>
    /// <param name="pollInterval">Interval between polls when no data is available (default: 100ms)</param>
    /// <param name="cancellationToken">Token to request cancellation of the stream</param>
    /// <remarks>
    /// <para>
    /// <b>Cancellation behavior:</b> The cancellation token is checked between COM operations
    /// and affects polling delays. However, individual COM calls (JVRead, etc.) cannot be
    /// interrupted mid-execution due to COM threading constraints. If a COM call is in progress
    /// when cancellation is requested, the operation will complete (or timeout) before the
    /// cancellation takes effect. This ensures COM resources remain in a consistent state.
    /// </para>
    /// <para>
    /// For immediate responsiveness to cancellation, consider using shorter poll intervals.
    /// The service's internal timeout (configured via retry policy) provides an upper bound
    /// on how long a hung COM call can block before the service becomes poisoned.
    /// </para>
    /// </remarks>
    member _.StreamRealtimeAsync
        (spec: string, key: string, ?pollInterval: TimeSpan, ?cancellationToken: CancellationToken)
        : IAsyncEnumerable<Result<JvPayload, XanthosError>> =
        let pollDelay = defaultArg pollInterval polling.DefaultPollInterval
        let externalToken = defaultArg cancellationToken CancellationToken.None

        // Helper to return a single error as an async enumerable
        let singleError (err: XanthosError) =
            { new IAsyncEnumerable<Result<JvPayload, XanthosError>> with
                member _.GetAsyncEnumerator(_) =
                    let mutable yielded = false

                    { new IAsyncEnumerator<Result<JvPayload, XanthosError>> with
                        member _.Current = Error err
                        member _.DisposeAsync() = ValueTask.CompletedTask

                        member _.MoveNextAsync() =
                            if yielded then
                                ValueTask<bool>(false)
                            else
                                yielded <- true
                                ValueTask<bool>(true) } }

        match Validation.normalizeDataspec spec, Validation.normalizeRealtimeKey key with
        | Error err, _ -> singleError err
        | _, Error err -> singleError err
        | Ok normalizedSpec, Ok normalizedKey ->

            { new IAsyncEnumerable<Result<JvPayload, XanthosError>> with
                member _.GetAsyncEnumerator(ct) =
                    let linkedCts = CancellationTokenSource.CreateLinkedTokenSource(externalToken, ct)
                    let token = linkedCts.Token
                    let mutable current = Unchecked.defaultof<Result<JvPayload, XanthosError>>
                    let mutable opened = false
                    let mutable finished = false
                    let mutable lockHeld = false

                    let closeSession () =
                        if opened then
                            opened <- false
                            closeQuietly ()

                    { new IAsyncEnumerator<Result<JvPayload, XanthosError>> with
                        member _.Current = current

                        member _.DisposeAsync() =
                            closeSession ()
                            linkedCts.Dispose()

                            if lockHeld then
                                lockHeld <- false
                                releaseLock ()

                            ValueTask.CompletedTask

                        member _.MoveNextAsync() =
                            let moveNext () =
                                task {
                                    if finished then
                                        return false
                                    else
                                        let mutable setupError: XanthosError option = None

                                        if not opened then
                                            // Acquire reentrancy guard before any COM operations
                                            if not lockHeld then
                                                if tryAcquireLock "StreamRealtimeAsync" then
                                                    lockHeld <- true
                                                else
                                                    setupError <- Some(concurrentAccessError "StreamRealtimeAsync")

                                            if setupError.IsNone then
                                                // Check initialize() first; only call openRealtimeSession if init succeeds
                                                match initialize () with
                                                | Error err -> setupError <- Some err
                                                | Ok() ->
                                                    match openRealtimeSession normalizedSpec normalizedKey with
                                                    | Ok hasData ->
                                                        // Session is opened - mark as opened for cleanup
                                                        opened <- true

                                                        if not hasData then
                                                            // No data available - signal end of stream
                                                            finished <- true
                                                    | Error err -> setupError <- Some err

                                        match setupError with
                                        | Some err ->
                                            current <- Error err
                                            finished <- true
                                            return true
                                        | None ->
                                            // Check if finished was set during setup (e.g., hasData = false)
                                            if finished then
                                                // No data available - close session and return false
                                                closeSession ()
                                                return false
                                            else
                                                let mutable delivered = false
                                                let mutable continuePolling = true
                                                let mutable cancelled = false

                                                // Helper for retrying recoverable errors (async version)
                                                let tryReadWithRetryAsync () =
                                                    task {
                                                        let mutable result = readWithTimeout ()
                                                        let mutable retryAttempt = 0

                                                        let shouldRetry () =
                                                            match result with
                                                            | Error err when
                                                                isRecoverableReadError err
                                                                && retryAttempt < readRetryMax
                                                                ->
                                                                true
                                                            | _ -> false

                                                        while shouldRetry () do
                                                            let delay = readRetryBackoff retryAttempt

                                                            let errForLog =
                                                                match result with
                                                                | Error e -> e
                                                                | _ -> Unexpected ""

                                                            let errMsg = describeError (InteropError errForLog)

                                                            logger.Warn(
                                                                $"JV-Link async stream read error (attempt {retryAttempt + 1}/{readRetryMax + 1}): {errMsg}. Retrying in {delay.TotalMilliseconds} ms."
                                                            )

                                                            do! scheduler.Delay(delay, token)
                                                            retryAttempt <- retryAttempt + 1
                                                            result <- readWithTimeout ()

                                                        return result
                                                    }

                                                try
                                                    while not delivered && continuePolling && not cancelled do
                                                        // Check cancellation without throwing - graceful termination per README
                                                        if token.IsCancellationRequested then
                                                            cancelled <- true
                                                        else
                                                            let! readResult = tryReadWithRetryAsync ()

                                                            match readResult with
                                                            | Ok(Payload payload) ->
                                                                current <- Ok payload
                                                                delivered <- true
                                                            | Ok FileBoundary -> ()
                                                            | Ok DownloadPending ->
                                                                do! scheduler.Delay(pollDelay, token)
                                                            | Ok EndOfStream ->
                                                                // EndOfStream signals no more data - terminate stream (same as sync version)
                                                                finished <- true
                                                                continuePolling <- false
                                                                closeSession ()
                                                            | Error err when isRecoverableReadError err ->
                                                                // Max retries exhausted - skip and continue
                                                                logger.Warn(
                                                                    $"JV-Link async stream read error after {readRetryMax + 1} attempts: {describeError (InteropError err)}. Skipping current file."
                                                                )

                                                                match skipWithTimeout () with
                                                                | Ok() ->
                                                                    logger.Debug(
                                                                        "JV-Link skip succeeded; continuing async stream."
                                                                    )
                                                                | Error skipErr ->
                                                                    logger.Error(
                                                                        $"JV-Link skip failed: {describeError (InteropError skipErr)}. Terminating async stream."
                                                                    )

                                                                    current <- Error(InteropError skipErr)
                                                                    finished <- true
                                                                    delivered <- true
                                                                    continuePolling <- false
                                                                    closeSession ()
                                                            | Error err ->
                                                                // Fatal error - terminate stream
                                                                logger.Error(
                                                                    $"JV-Link async stream read failed: {describeError (InteropError err)}. Terminating async stream."
                                                                )

                                                                current <- Error(InteropError err)
                                                                finished <- true
                                                                delivered <- true
                                                                continuePolling <- false
                                                                closeSession ()
                                                with :? OperationCanceledException ->
                                                    // Swallow cancellation exception and return false for graceful termination
                                                    cancelled <- true

                                                if cancelled then
                                                    // Graceful cancellation: close session and return false
                                                    closeSession ()
                                                    return false
                                                else
                                                    return delivered
                                }

                            ValueTask<bool>(moveNext ()) } }

    /// <summary>
    /// Backwards-compatible alias for <see cref="StreamRealtimeAsync" />.
    /// </summary>
    member this.StreamRealtimePayloadsAsync
        (spec: string, key: string, ?pollInterval: TimeSpan, ?cancellationToken: CancellationToken)
        =
        this.StreamRealtimeAsync(spec, key, ?pollInterval = pollInterval, ?cancellationToken = cancellationToken)

    /// <summary>
    /// Retrieves the number of files whose download has completed for the current session.
    /// Note: This requires an active JVOpen/JVRTOpen session - do not re-initialize.
    /// </summary>
    member _.GetStatus() : Result<int, XanthosError> =
        runCom "JVStatus" (fun () -> client.Status())

    /// <summary>
    /// Skips the current download file and advances the JV-Link cursor.
    /// Note: This requires an active JVOpen/JVRTOpen session - do not re-initialize.
    /// </summary>
    member _.SkipCurrentFile() : Result<unit, XanthosError> =
        runCom "JVSkip" (fun () -> client.Skip())

    /// <summary>
    /// Requests JV-Link to cancel the active download thread.
    /// Note: This requires an active JVOpen/JVRTOpen session - do not re-initialize.
    /// </summary>
    member _.CancelDownload() : Result<unit, XanthosError> =
        runCom "JVCancel" (fun () -> client.Cancel())

    /// <summary>
    /// Deletes a previously downloaded file from the JV-Link save path.
    /// </summary>
    /// <remarks>
    /// The filename is passed directly to JVFiledelete without text normalization
    /// to preserve full-width characters that may be present in valid file paths.
    /// </remarks>
    member _.DeleteFile filename : Result<unit, XanthosError> =
        guardedWithInitialisation "DeleteFile" (fun () -> runCom "JVFiledelete" (fun () -> client.DeleteFile filename))

    /// <summary>
    /// Checks whether a race movie is currently available via `JVMVCheck`.
    /// </summary>
    member _.CheckMovieAvailability(key: string) : Result<MovieAvailability, XanthosError> =
        guardedWithInitialisation "CheckMovieAvailability" (fun () ->
            runCom "JVMVCheck" (fun () -> client.MovieCheck(Text.normalizeJvText key)))

    /// <summary>
    /// Checks whether a movie is available for the specified movietype via `JVMVCheckWithType`.
    /// </summary>
    member _.CheckMovieAvailability(movieType: MovieType, key: string) : Result<MovieAvailability, XanthosError> =
        guardedWithInitialisation "CheckMovieAvailability" (fun () ->
            runCom "JVMVCheckWithType" (fun () ->
                client.MovieCheckWithType(MovieType.toCode movieType, Text.normalizeJvText key)))

    /// <summary>
    /// Requests movie playback via `JVMVPlay`.
    /// </summary>
    member _.PlayMovie(key: string) : Result<unit, XanthosError> =
        guardedWithInitialisation "PlayMovie" (fun () ->
            runCom "JVMVPlay" (fun () -> client.MoviePlay(Text.normalizeJvText key)))

    /// <summary>
    /// Requests movie playback for a specified movietype via `JVMVPlayWithType`.
    /// </summary>
    member _.PlayMovie(movieType: MovieType, key: string) : Result<unit, XanthosError> =
        guardedWithInitialisation "PlayMovie" (fun () ->
            runCom "JVMVPlayWithType" (fun () ->
                client.MoviePlayWithType(MovieType.toCode movieType, Text.normalizeJvText key)))

    /// <summary>
    /// Returns whether JV-Link is configured to persist downloads to disk.
    /// </summary>
    /// <returns>Ok(bool) on success, or Error if the property cannot be read.</returns>
    member _.GetSaveDownloadsEnabled() : Result<bool, XanthosError> =
        client.TryGetSaveFlag() |> Errors.mapComError

    /// <summary>
    /// Updates the JV-Link save flag (persist downloads to disk).
    /// </summary>
    member _.SetSaveDownloadsEnabled(enabled: bool) : Result<unit, XanthosError> =
        guardedWithInitialisation "SetSaveDownloadsEnabled" (fun () ->
            match runCom "JVSetSaveFlag" (fun () -> client.SetSaveFlag enabled) with
            | Ok() ->
                try
                    client.SaveFlag <- enabled
                    Ok()
                with _ ->
                    Ok()
            | Error err -> Error err)

    /// <summary>
    /// Retrieves the configured JV-Link save path.
    /// </summary>
    /// <returns>Ok(string) on success, or Error if the property cannot be read.</returns>
    member _.GetSavePath() : Result<string, XanthosError> =
        client.TryGetSavePath() |> Errors.mapComError

    /// <summary>
    /// Sets the JV-Link save path.
    /// </summary>
    member _.SetSavePath(path: string) : Result<unit, XanthosError> =
        // Do NOT normalize user input - paths may contain fullwidth characters intentionally
        // (e.g., "Ｄ:\競馬\保存先" is valid on Windows)
        let trimmed = if isNull path then "" else path.Trim()

        guardedWithInitialisation "SetSavePath" (fun () ->
            match runCom "JVSetSavePath" (fun () -> client.SetSavePathDirect trimmed) with
            | Ok() ->
                try
                    client.SavePath <- trimmed
                with _ ->
                    ()
                // Update currentConfig so next initialization uses the new value
                currentConfig <-
                    { currentConfig with
                        SavePath = Some trimmed }

                Ok()
            | Error err -> Error err)

    /// <summary>
    /// Gets the JV-Link service key currently configured.
    /// </summary>
    /// <returns>Ok(string) on success, or Error if the property cannot be read.</returns>
    member _.GetServiceKey() : Result<string, XanthosError> =
        client.TryGetServiceKey() |> Errors.mapComError

    /// <summary>
    /// Sets the JV-Link service key.
    /// </summary>
    member _.SetServiceKey(key: string) : Result<unit, XanthosError> =
        // Do NOT normalize user input - service keys may contain fullwidth characters intentionally
        let trimmed = if isNull key then "" else key.Trim()

        guardedWithInitialisation "SetServiceKey" (fun () ->
            match runCom "JVSetServiceKey" (fun () -> client.SetServiceKeyDirect trimmed) with
            | Ok() ->
                try
                    client.ServiceKey <- trimmed
                with _ ->
                    ()
                // Update currentConfig so next initialization uses the new value
                currentConfig <-
                    { currentConfig with
                        ServiceKey = Some trimmed }

                Ok()
            | Error err -> Error err)

    /// <summary>
    /// Retrieves the JV-Link version string reported by the COM layer.
    /// </summary>
    member _.GetJVLinkVersion() : Result<string, XanthosError> =
        client.TryGetJVLinkVersion() |> Errors.mapComError

    /// <summary>
    /// Returns the total remaining download size in KB (kilobytes) as reported by JV-Link.
    /// </summary>
    /// <remarks>JV-Link returns <c>m_TotalReadFilesize</c> in KB. Use <see cref="GetTotalReadFileSizeBytes"/> for bytes.</remarks>
    member _.GetTotalReadFileSize() : Result<int64, XanthosError> =
        client.TryGetTotalReadFileSize() |> Errors.mapComError

    /// <summary>
    /// Returns the total remaining download size in bytes.
    /// </summary>
    /// <remarks>Converts JV-Link's <c>m_TotalReadFilesize</c> (KB) to bytes by multiplying by 1024.</remarks>
    member _.GetTotalReadFileSizeBytes() : Result<int64, XanthosError> =
        client.TryGetTotalReadFileSize()
        |> Result.map (fun kb -> kb * 1024L)
        |> Errors.mapComError

    /// <summary>
    /// Returns the size of the currently read file as reported by JV-Link.
    /// </summary>
    member _.GetCurrentReadFileSize() : Result<int64, XanthosError> =
        client.TryGetCurrentReadFileSize() |> Errors.mapComError

    /// <summary>
    /// Returns the timestamp of the file currently being read, if reported by JV-Link.
    /// </summary>
    member _.GetCurrentFileTimestamp() : Result<DateTime option, XanthosError> =
        client.TryGetCurrentFileTimestamp() |> Errors.mapComError

    /// <summary>
    /// Retrieves the parent window handle configured for JV-Link dialogs.
    /// </summary>
    member _.GetParentWindowHandle() : Result<IntPtr, XanthosError> =
        client.TryGetParentWindowHandle() |> Errors.mapComError

    /// <summary>
    /// Sets the parent window handle used for JV-Link dialogs.
    /// </summary>
    member _.SetParentWindowHandle(handle: IntPtr) : Result<unit, XanthosError> =
        client.SetParentWindowHandleDirect(handle) |> Errors.mapComError

    /// <summary>
    /// Returns whether payoff dialogs are suppressed within JV-Link.
    /// </summary>
    member _.GetPayoffDialogSuppressed() : Result<bool, XanthosError> =
        client.TryGetPayoffDialogSuppressed() |> Errors.mapComError

    /// <summary>
    /// Configures payoff dialog suppression within JV-Link.
    /// </summary>
    member _.SetPayoffDialogSuppressed(suppressed: bool) : Result<unit, XanthosError> =
        client.SetPayoffDialogSuppressedDirect(suppressed) |> Errors.mapComError

    /// <summary>
    /// Displays the JV-Link configuration dialog via `JVSetUIProperties`.
    /// </summary>
    member _.ShowConfigurationDialog() : Result<unit, XanthosError> =
        guardedWithInitialisation "ShowConfigurationDialog" (fun () ->
            runCom "JVSetUIProperties" (fun () -> client.SetUiProperties()))

    /// <summary>
    /// Retrieves a course diagram (file path + explanation) using `JVCourseFile`.
    /// </summary>
    /// <remarks>
    /// This utility function does not open a JV-Link session (JVOpen/JVRTOpen),
    /// so it does not close any session. It can be safely used while other sessions are active.
    /// </remarks>
    member _.GetCourseDiagram(key: string) : Result<CourseDiagram, XanthosError> =
        let normalized = Text.normalizeJvText key

        guardedWithInitialisation "GetCourseDiagram" (fun () ->
            runCom "JVCourseFile" (fun () -> client.CourseFile normalized)
            |> Result.map (fun (path, explanation) ->
                { FilePath = path
                  Explanation =
                    if String.IsNullOrWhiteSpace explanation then
                        None
                    else
                        Some explanation }))

    /// <summary>
    /// Saves a course diagram to the specified filepath using `JVCourseFile2`.
    /// </summary>
    /// <remarks>
    /// This utility function does not open a JV-Link session (JVOpen/JVRTOpen),
    /// so it does not close any session. It can be safely used while other sessions are active.
    /// The filepath must be a full path to a file in an existing directory.
    /// </remarks>
    member _.GetCourseDiagramBasic(key: string, filepath: string) : Result<CourseDiagram, XanthosError> =
        let normalized = Text.normalizeJvText key

        guardedWithInitialisation "GetCourseDiagramBasic" (fun () ->
            runCom "JVCourseFile2" (fun () -> client.CourseFile2(normalized, filepath))
            |> Result.map (fun () ->
                { FilePath = filepath
                  Explanation = None }))

    /// <summary>
    /// Generates a silks (勝負服) bitmap on disk using <c>JVFukuFile</c> and returns its location.
    /// </summary>
    /// <remarks>
    /// This utility function does not open a JV-Link session (JVOpen/JVRTOpen),
    /// so it does not close any session. It can be safely used while other sessions are active.
    /// </remarks>
    member _.GenerateSilksFile(pattern: string, outputPath: string) : Result<SilksImage, XanthosError> =
        let normalizedPattern = Text.normalizeJvText pattern

        guardedWithInitialisation "GenerateSilksFile" (fun () ->
            runCom "JVFukuFile" (fun () -> client.SilksFile(normalizedPattern, outputPath))
            |> Result.map (fun pathOpt ->
                // pathOpt is None for "No Image" case (pattern not found)
                { FilePath = pathOpt; Data = None }))

    /// <summary>
    /// Retrieves a silks (勝負服) bitmap as binary data using <c>JVFuku</c>.
    /// </summary>
    /// <remarks>
    /// This utility function does not open a JV-Link session (JVOpen/JVRTOpen),
    /// so it does not close any session. It can be safely used while other sessions are active.
    /// </remarks>
    member _.GetSilksBinary(pattern: string) : Result<SilksImage, XanthosError> =
        let normalizedPattern = Text.normalizeJvText pattern

        guardedWithInitialisation "GetSilksBinary" (fun () ->
            runCom "JVFuku" (fun () -> client.SilksBinary normalizedPattern)
            |> Result.map (fun dataOpt ->
                // dataOpt is None for "No Image" case (pattern not found)
                { FilePath = None; Data = dataOpt }))

    /// <summary>
    /// Fetches workout video listings via `JVMVOpen`/`JVMVRead` for the specified movietype.
    /// </summary>
    member _.FetchWorkoutVideos
        (movieType: MovieType, searchKey: string)
        : Result<WorkoutVideoListing list, XanthosError> =
        if not (MovieType.isWorkoutType movieType) then
            Error(Unsupported "JVMVOpen currently supports workout movie types (11, 12, 13).")
        else
            guardedWithInitialisation "FetchWorkoutVideos" (fun () ->
                let mutable opened = false

                let rec readAll acc =
                    match runCom "JVMVRead" (fun () -> client.MovieRead()) with
                    | Error err -> Error err
                    | Ok MovieEnd -> Ok(List.rev acc)
                    | Ok(MovieRecord listing) -> readAll (listing :: acc)

                let outcome =
                    result {
                        do!
                            runCom "JVMVOpen" (fun () ->
                                client.MovieOpen(MovieType.toCode movieType, Text.normalizeJvText searchKey))

                        opened <- true
                        return! readAll []
                    }

                if opened then
                    closeQuietly ()

                outcome)

    /// <summary>
    /// Observable sequence of watch events (`JVWatchEvent` callbacks).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Events are delivered as <c>Result&lt;WatchEvent, XanthosError&gt;</c>. Normally, subscribers
    /// receive <c>Ok(event)</c> values. If the internal event queue reaches capacity (due to slow
    /// consumer processing), the stream emits <c>Error(EventQueueOverflow n)</c> to notify
    /// subscribers of potential event loss. This allows subscribers to detect backpressure.
    /// </para>
    /// <para>
    /// The queue capacity defaults to 10,000 events and can be configured via the
    /// <c>eventQueueCapacity</c> constructor parameter.
    /// </para>
    /// </remarks>
    member _.WatchEvents: IObservable<Result<WatchEvent, XanthosError>> =
        watchEventStream.Publish

    /// <summary>
    /// Starts JV-Link watch event notifications.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Events are queued to a dedicated background thread to avoid blocking the COM STA thread
    /// while preserving FIFO event order. Subscribers should still be lightweight; for heavy
    /// processing, offload to a Task or use Reactive Extensions operators like ObserveOn.
    /// </para>
    /// <para>
    /// If events arrive faster than the subscriber can process them and the queue fills up,
    /// incoming events are dropped and an <c>EventQueueOverflow</c> error is emitted on the
    /// <see cref="WatchEvents"/> stream. The queue capacity can be configured via the
    /// <c>eventQueueCapacity</c> constructor parameter (default: 10,000).
    /// </para>
    /// </remarks>
    member _.StartWatchEvents() : Result<unit, XanthosError> =
        // Atomically try to transition from Stopped (0) to Starting (1).
        // If already Starting (1) or Running (2), return immediately (idempotent).
        let previousState =
            System.Threading.Interlocked.CompareExchange(watchingFlag, WatchState_Starting, WatchState_Stopped)

        if previousState <> WatchState_Stopped then
            // Already in Starting or Running state - nothing to do
            Ok()
        else
            // WatchEvent does NOT acquire operationLock - it can run concurrently with
            // data fetching operations. Only the subscription/unsubscription is protected
            // by the atomic 'watchingFlag' to prevent double-subscription.
            let callback key =
                // Enqueue immediately - non-blocking TryAdd to avoid blocking the STA message pump.
                // The consumer thread will process events in FIFO order.
                // If the queue is full (bounded capacity reached), notify subscribers via Error.
                try
                    if not (eventQueue.TryAdd(key)) then
                        // Queue is full - consumer is too slow
                        // Notify subscribers so they can detect backpressure/event loss
                        logger.Warn(
                            $"WatchEvent queue full (capacity: {eventQueueCapacity}) - event discarded. Consider increasing consumer throughput or eventQueueCapacity."
                        )

                        // Signal overflow to be reported by consumer thread
                        // (avoid triggering event stream from STA callback)
                        System.Threading.Interlocked.Increment(overflowCount) |> ignore
                with
                | :? ObjectDisposedException ->
                    // Queue was disposed (service disposed)
                    // Must be caught before InvalidOperationException (parent class)
                    logger.Warn("WatchEvent received after dispose - event discarded.")
                | :? InvalidOperationException ->
                    // Queue was completed (service shutting down)
                    logger.Warn("WatchEvent received after shutdown - event discarded.")

            let subscriptionResult =
                result {
                    do! initialize ()
                    do! runCom "JVWatchEvent" (fun () -> client.WatchEvent callback)
                }

            match subscriptionResult with
            | Ok() ->
                // Start consumer thread ONLY after COM subscription succeeds
                // to avoid thread leak on initialization/subscription failure
                startEventConsumer ()
                // Try to transition from Starting (1) to Running (2).
                // If StopWatchEvents was called during Starting phase, the flag will already
                // be Stopped (0) and we should clean up instead of forcing Running.
                let transitionResult =
                    System.Threading.Interlocked.CompareExchange(watchingFlag, WatchState_Running, WatchState_Starting)

                if transitionResult = WatchState_Starting then
                    // Successful transition - no cancellation occurred
                    logger.Info("JV-Link watch events started.")
                    Ok()
                else
                    // StopWatchEvents was called during Starting phase - clean up
                    // The COM subscription was established, so we must close it
                    logger.Info("JV-Link watch events cancelled during startup - cleaning up.")
                    runCom "JVWatchEventClose" (fun () -> client.WatchEventClose()) |> ignore
                    stopEventConsumer ()
                    Ok()
            | Error err ->
                // Reset flag to Stopped (0) on failure so caller can retry
                System.Threading.Interlocked.Exchange(watchingFlag, WatchState_Stopped)
                |> ignore

                Error err

    /// <summary>
    /// Stops JV-Link watch event notifications.
    /// </summary>
    member _.StopWatchEvents() : Result<unit, XanthosError> =
        // Atomically set flag to Stopped (0) and check what state we were in.
        // Only call WatchEventClose if we were in Running (2) state, because that's
        // when the COM subscription is actually active. If we were in Starting (1),
        // the COM subscription hasn't been established yet.
        let previousState =
            System.Threading.Interlocked.Exchange(watchingFlag, WatchState_Stopped)

        match previousState with
        | s when s = WatchState_Stopped ->
            // Already stopped - nothing to do (idempotent)
            Ok()
        | s when s = WatchState_Starting ->
            // Was starting but COM subscription not yet active - just reset flag
            // Don't call WatchEventClose since WatchEvent hasn't completed yet
            logger.Info("JV-Link watch events cancelled before subscription completed.")
            Ok()
        | _ ->
            // Was Running (2) - COM subscription is active, close it
            // Close COM subscription FIRST to stop callbacks, THEN stop consumer thread.
            // This order ensures no callbacks arrive after the queue is completed.
            // Note: watchingFlag already reset to 0 by Exchange above, allowing
            // StartWatchEvents to reconnect even if COM close fails.
            let comResult = runCom "JVWatchEventClose" (fun () -> client.WatchEventClose())

            // Now stop the consumer thread (drains remaining events that were queued)
            stopEventConsumer ()

            match comResult with
            | Ok() ->
                logger.Info("JV-Link watch events stopped.")
                Ok()
            | Error err ->
                logger.Error($"JV-Link watch event stop failed: {describeError err}")
                Error err

    // -------------------------------------------------------------------------
    // Typed Record Parsing API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Fetches payloads and parses them into strongly typed domain records.
    /// Fails fast on the first parse error.
    /// </summary>
    /// <param name="request">The JV-Link open request specifying dataspec and time range.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A list of parsed records or the first error encountered.</returns>
    member this.FetchTypedRecords
        (request: JvOpenRequest, ?cancellationToken: CancellationToken)
        : Result<ParsedRecord list, XanthosError> =
        result {
            let! payloads = this.FetchPayloads(request, ?cancellationToken = cancellationToken)
            return! PayloadParser.parsePayloads payloads
        }

    /// <summary>
    /// Fetches payloads and parses them, collecting both successes and failures.
    /// </summary>
    /// <param name="request">The JV-Link open request specifying dataspec and time range.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A tuple of successfully parsed records and failed payloads with their errors.</returns>
    member this.FetchTypedRecordsCollectErrors
        (request: JvOpenRequest, ?cancellationToken: CancellationToken)
        : Result<ParsedRecord list * (JvPayload * XanthosError) list, XanthosError> =
        result {
            let! payloads = this.FetchPayloads(request, ?cancellationToken = cancellationToken)
            return PayloadParser.tryParsePayloads payloads
        }

    /// <summary>
    /// Fetches payloads and parses them into strongly typed domain records.
    /// </summary>
    /// <param name="spec">The dataspec string (e.g., "RACE" for race data).</param>
    /// <param name="fromTime">Start time for data retrieval (required by JVOpen).</param>
    /// <param name="openOption">Optional open option (default: 1 for standard, 2 for setup without dialog).</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    member this.FetchTypedRecords
        (spec: string, fromTime: DateTime option, ?openOption: int, ?cancellationToken: CancellationToken)
        =
        match fromTime with
        | None -> Error(ValidationError "JVOpen requires fromTime (yyyyMMddHHmmss).")
        | Some ts ->
            let optionValue = defaultArg openOption 1

            this.FetchTypedRecords(
                { Spec = spec
                  FromTime = ts
                  Option = optionValue },
                ?cancellationToken = cancellationToken
            )

    /// <summary>
    /// Parses a single JV payload into a typed domain record.
    /// </summary>
    /// <param name="payload">The payload to parse.</param>
    /// <returns>The parsed record or an error.</returns>
    static member ParsePayload(payload: JvPayload) : Result<ParsedRecord, XanthosError> =
        PayloadParser.parsePayload payload

    /// <summary>
    /// Parses multiple JV payloads into typed domain records.
    /// </summary>
    /// <param name="payloads">The payloads to parse.</param>
    /// <returns>The list of parsed records or the first error encountered.</returns>
    static member ParsePayloads(payloads: JvPayload list) : Result<ParsedRecord list, XanthosError> =
        PayloadParser.parsePayloads payloads

    /// <summary>
    /// Parses multiple JV payloads, collecting both successes and failures.
    /// </summary>
    /// <param name="payloads">The payloads to parse.</param>
    /// <returns>A tuple of successfully parsed records and failed payloads with their errors.</returns>
    static member TryParsePayloads(payloads: JvPayload list) : ParsedRecord list * (JvPayload * XanthosError) list =
        PayloadParser.tryParsePayloads payloads

    /// <summary>
    /// Extracts the 2-character record type identifier from payload data.
    /// </summary>
    /// <param name="data">Raw payload bytes.</param>
    /// <returns>The record type identifier (e.g., "TK", "RA", "SE").</returns>
    static member GetRecordTypeId(data: byte[]) : string = PayloadParser.getRecordTypeId data

    // -------------------------------------------------------------------------
    // IDisposable
    // -------------------------------------------------------------------------

    /// <summary>
    /// Releases all resources used by the JvLinkService, including the underlying COM client.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method stops any active watch event monitoring and disposes the injected
    /// <see cref="IJvLinkClient"/>. After calling Dispose, the service instance should
    /// not be used.
    /// </para>
    /// <para>
    /// The <c>JvLinkService</c> takes ownership of the client passed to its constructor.
    /// Callers should use the <c>use</c> keyword or explicitly call <c>Dispose()</c> to
    /// ensure proper cleanup of COM resources.
    /// </para>
    /// </remarks>
    interface IDisposable with
        member this.Dispose() =
            if not disposed then
                disposed <- true
                // Stop watch events if Starting (1) or Running (2) (ignore result as we're disposing)
                if
                    System.Threading.Interlocked.CompareExchange(watchingFlag, WatchState_Stopped, WatchState_Stopped)
                    <> WatchState_Stopped
                then
                    this.StopWatchEvents() |> ignore

                // Dispose the event queue
                eventQueue.Dispose()

                // Dispose the reentrancy guard semaphore
                operationLock.Dispose()

                // Dispose the client if it implements IDisposable
                // CRITICAL: When poisoned (due to STA timeout), the STA thread is blocked.
                // Calling client.Dispose() would try to dispatch through the same blocked thread,
                // causing this Dispose to hang forever.
                if poisoned then
                    let reason = defaultArg poisonReason "unknown"

                    logger.Warn(
                        "Skipping client disposal - service is poisoned due to STA timeout. "
                        + $"Reason: {reason}. "
                        + "COM resources may not be properly released. "
                        + "Process termination will clean up any leaked resources."
                    )
                    // Best effort: Try to forcefully dispose the dispatcher without going through STA
                    // This will terminate the STA thread and unblock any pending calls
                    match comDispatcher with
                    | Some dispatcher ->
                        try
                            (dispatcher :> IDisposable).Dispose()
                            logger.Info("STA dispatcher forcefully disposed after poisoned state.")
                        with ex ->
                            logger.Warn($"Failed to forcefully dispose STA dispatcher: {ex.Message}")
                    | None -> ()
                else
                    match box client with
                    | :? IDisposable as d -> d.Dispose()
                    | _ -> ()

    /// <summary>
    /// Releases all resources used by the JvLinkService.
    /// </summary>
    member this.Dispose() = (this :> IDisposable).Dispose()

    // -------------------------------------------------------------------------
    // Static Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Performs a lightweight COM activation check to confirm whether JV-Link is available on the current host.
    /// </summary>
    static member IsComAvailable() = ComClientFactory.isComAvailable ()

    /// <summary>
    /// Routes low-level COM diagnostics (emitted by <c>ComJvLinkClient</c>) to the supplied sink.
    /// </summary>
    static member ConfigureDiagnosticsSink(sink: string -> unit) = Diagnostics.register sink

    /// <summary>
    /// Clears the COM diagnostic sink so traces fall back to stdout.
    /// </summary>
    static member ClearDiagnosticsSink() = Diagnostics.clear ()

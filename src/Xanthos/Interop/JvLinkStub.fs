namespace Xanthos.Interop

open System
open System.Collections.Concurrent
open Xanthos.Core

/// <summary>
/// In-memory JV-Link emulator suitable for deterministic unit and property tests.
/// </summary>
type JvLinkStub(responses: seq<Result<JvReadOutcome, ComError>>, ?totalSize: int64, ?initialTimestamp: DateTime) =

    let queue =
        ConcurrentQueue<Result<JvReadOutcome, ComError>>(responses |> Seq.toArray)

    let realtimeQueue = ConcurrentQueue<Result<JvReadOutcome, ComError>>()
    let mutable savePath = ""
    let mutable serviceKey = ""
    let mutable totalReadFileSize = defaultArg totalSize 0L
    let mutable currentTimestamp = initialTimestamp
    let mutable currentReadFileSize = 0L
    let mutable saveFlag = false
    let mutable jvLinkVersion = "0000"
    let mutable parentWindowHandle = IntPtr.Zero
    let mutable payoffDialogSuppressed = false
    let mutable lastServiceKey = None
    let mutable initialized = false
    let mutable openSession = false
    let initialPayloadCount = queue.Count
    let mutable completedPayloads = 0
    let mutable realtimeSession = false
    let mutable eventCallback: (string -> unit) option = None

    let mutable courseFileResponse: (string * string) option =
        Some("C:\\tmp\\course.gif", "Sample course")

    let mutable courseFile2Response: string option = Some "C:\\tmp\\course.gif"

    let mutable silksFileResponder: (string * string -> Result<string option, ComError>) =
        fun (_, outputPath) -> Ok(Some outputPath)

    let mutable silksBinaryResponder: (string -> Result<byte[] option, ComError>) =
        fun _ -> Ok(Some Array.empty)

    let mutable uiDialogInvocations = 0
    let movieListQueue = ConcurrentQueue<Result<MovieReadOutcome, ComError>>()

    let mutable movieAvailabilityResponder: (string -> Result<MovieAvailability, ComError>) =
        fun _ -> Ok MovieAvailability.Unavailable

    let mutable movieAvailabilityWithTypeResponder: (string * string -> Result<MovieAvailability, ComError>) =
        fun _ -> Ok MovieAvailability.Unavailable

    let mutable moviePlayResponder: (string * string option -> Result<unit, ComError>) =
        fun _ -> Ok()

    let mutable watchEventError: ComError option = None

    new() = new JvLinkStub(Seq.empty)

    /// Configures WatchEvent to fail with the specified error.
    /// Use to simulate COM event connection failures in tests.
    member _.ConfigureWatchEventFailure(error: ComError) = watchEventError <- Some error

    /// Clears any configured WatchEvent failure.
    member _.ClearWatchEventFailure() = watchEventError <- None

    /// Enqueues an additional response to be returned on subsequent `Read` calls.
    member _.Enqueue(response: Result<JvReadOutcome, ComError>) =
        queue.Enqueue(response)
        ()

    /// Adjusts the synthetic `TotalReadFileSize` that the stub will expose.
    member _.ConfigureTotalReadFileSize(bytes: int64) = totalReadFileSize <- bytes

    interface IJvLinkClient with
        /// <inheritdoc />
        member _.Init(sid) =
            if String.IsNullOrWhiteSpace(sid) then
                Error(InvalidInput "sid must be a non-empty string.")
            else
                initialized <- true
                Ok()

        /// <inheritdoc />
        member _.Open(request: JvOpenRequest) =
            // Some unit tests may call Open without prior Init; stub should be permissive.
            if not initialized then
                initialized <- true

            if String.IsNullOrWhiteSpace(request.Spec) then
                Error(InvalidInput "dataspec must be provided.")
            else
                openSession <- true
                realtimeSession <- false
                // Stub returns realistic metadata for testing
                Ok
                    { HasData = true
                      ReadCount = initialPayloadCount
                      DownloadCount = 0
                      LastFileTimestamp = None }

        /// <inheritdoc />
        member _.OpenRealtime(spec, _) =
            // Permit OpenRealtime without prior Init in stub to ease testing.
            if not initialized then
                initialized <- true

            if String.IsNullOrWhiteSpace(spec) then
                Error(InvalidInput "dataspec must be provided.")
            else
                openSession <- true
                realtimeSession <- true

                while not queue.IsEmpty do
                    let mutable item = Unchecked.defaultof<_>

                    if queue.TryDequeue(&item) then
                        realtimeQueue.Enqueue(item)

                Ok
                    { HasData = true
                      ReadCount = 0
                      DownloadCount = 0
                      LastFileTimestamp = None }

        /// <inheritdoc />
        member _.Read() =
            if not openSession then
                Error(InvalidState "Read was invoked before Open.")
            else
                let tryDequeue (source: ConcurrentQueue<_>) =
                    let mutable item = Unchecked.defaultof<_>
                    if source.TryDequeue(&item) then Some item else None

                let sourceQueue = if realtimeSession then realtimeQueue else queue

                match tryDequeue sourceQueue with
                | Some(Ok(Payload payload as outcome)) ->
                    currentTimestamp <- payload.Timestamp
                    completedPayloads <- completedPayloads + 1
                    Ok outcome
                | Some(Ok outcome) -> Ok outcome
                | Some(Error err) -> Error err
                | None -> Ok EndOfStream

        /// <inheritdoc />
        member _.Gets(buffer, bufferSize, filename) =
            if not openSession then
                Error(InvalidState "Gets was invoked before Open.")
            else
                let tryDequeue (source: ConcurrentQueue<_>) =
                    let mutable item = Unchecked.defaultof<_>
                    if source.TryDequeue(&item) then Some item else None

                let sourceQueue = if realtimeSession then realtimeQueue else queue

                match tryDequeue sourceQueue with
                | Some(Ok(Payload payload)) ->
                    currentTimestamp <- payload.Timestamp
                    completedPayloads <- completedPayloads + 1
                    let text = Text.decodeShiftJis payload.Data
                    buffer <- text
                    filename <- "stubfile.jvd"
                    Ok payload.Data.Length
                | Some(Ok FileBoundary) -> Ok -1
                | Some(Ok DownloadPending) -> Ok -3
                | Some(Ok EndOfStream) -> Ok 0
                | Some(Error err) -> Error err
                | None -> Ok 0

        /// <inheritdoc />
        member _.Close() =
            openSession <- false
            realtimeSession <- false

        /// <inheritdoc />
        member _.Status() =
            // For testing, report completed count even if session is closed.
            if not initialized then
                // Auto-initialise for permissive behaviour in tests
                initialized <- true

            if not openSession then
                Ok completedPayloads
            else
                Ok completedPayloads

        /// <inheritdoc />
        member _.Skip() =
            if not openSession then
                Error(InvalidState "Skip requested before opening a dataspec.")
            else
                match queue.TryDequeue() with
                | true, Ok(Payload payload) ->
                    completedPayloads <- completedPayloads + 1
                    currentTimestamp <- payload.Timestamp
                    Ok()
                | true, Ok _ -> Ok()
                | true, Error err -> Error err
                | false, _ -> Ok()

        /// <inheritdoc />
        member _.Cancel() =
            openSession <- false
            realtimeSession <- false
            Ok()

        /// <inheritdoc />
        member _.DeleteFile _ = Ok()

        /// <inheritdoc />
        member _.WatchEvent(callback) =
            if not initialized then
                Error NotInitialized
            else
                match watchEventError with
                | Some err -> Error err
                | None ->
                    eventCallback <- Some callback
                    Ok()

        /// <inheritdoc />
        member _.WatchEventClose() =
            eventCallback <- None
            Ok()

        /// <inheritdoc />
        member _.SetUiProperties() =
            uiDialogInvocations <- uiDialogInvocations + 1
            Ok()

        /// <inheritdoc />
        member _.SetSaveFlag enabled =
            saveFlag <- enabled
            Ok()

        /// <inheritdoc />
        member _.SetServiceKeyDirect key =
            serviceKey <- key
            lastServiceKey <- Some key
            Ok()

        /// <inheritdoc />
        member _.SetSavePathDirect path =
            savePath <- path
            Ok()

        /// <inheritdoc />
        member _.SetParentWindowHandleDirect handle =
            parentWindowHandle <- handle
            Ok()

        /// <inheritdoc />
        member _.SetPayoffDialogSuppressedDirect suppressed =
            payoffDialogSuppressed <- suppressed
            Ok()

        /// <inheritdoc />
        member _.CourseFile key =
            match courseFileResponse with
            | Some response -> Ok response
            | None -> Ok($"C:\\Stub\\course_{key}.gif", $"Stub course diagram for {key}")

        /// <inheritdoc />
        member _.CourseFile2(key, filepath) =
            // JVCourseFile2 saves to the specified filepath - in stub mode, just return success
            Ok()

        /// <inheritdoc />
        member _.SilksFile(pattern, outputPath) =
            silksFileResponder (pattern, outputPath)

        /// <inheritdoc />
        member _.SilksBinary(pattern) = silksBinaryResponder pattern

        /// <inheritdoc />
        member _.MovieCheck key = movieAvailabilityResponder key

        /// <inheritdoc />
        member _.MovieCheckWithType(movieType, key) =
            movieAvailabilityWithTypeResponder (movieType, key)

        /// <inheritdoc />
        member _.MoviePlay key = moviePlayResponder (key, None)

        /// <inheritdoc />
        member _.MoviePlayWithType(movieType, key) =
            moviePlayResponder (key, Some movieType)

        /// <inheritdoc />
        member _.MovieOpen(_, _) = Ok()

        /// <inheritdoc />
        member _.MovieRead() =
            match movieListQueue.TryDequeue() with
            | true, outcome -> outcome
            | false, _ -> Ok MovieEnd

        /// <inheritdoc />
        member _.SaveFlag
            with get () = saveFlag
            and set value = saveFlag <- value

        /// <inheritdoc />
        member _.SavePath = savePath

        /// <inheritdoc />
        member _.ServiceKey = serviceKey

        /// <inheritdoc />
        member _.TryGetSaveFlag() = Ok saveFlag

        /// <inheritdoc />
        member _.TryGetSavePath() = Ok savePath

        /// <inheritdoc />
        member _.TryGetServiceKey() = Ok serviceKey

        /// <inheritdoc />
        member _.TryGetJVLinkVersion() = Ok jvLinkVersion

        /// <inheritdoc />
        member _.TryGetTotalReadFileSize() = Ok totalReadFileSize

        /// <inheritdoc />
        member _.TryGetCurrentReadFileSize() = Ok currentReadFileSize

        /// <inheritdoc />
        member _.TryGetCurrentFileTimestamp() = Ok currentTimestamp

        /// <inheritdoc />
        member _.TryGetParentWindowHandle() = Ok parentWindowHandle

        /// <inheritdoc />
        member _.TryGetPayoffDialogSuppressed() = Ok payoffDialogSuppressed

        /// <inheritdoc />
        member _.JVLinkVersion = jvLinkVersion

        /// <inheritdoc />
        member _.TotalReadFileSize = totalReadFileSize

        /// <inheritdoc />
        member _.CurrentReadFileSize = currentReadFileSize

        /// <inheritdoc />
        member _.CurrentFileTimestamp = currentTimestamp

        /// <inheritdoc />
        member _.ParentWindowHandle
            with get () = parentWindowHandle
            and set value = parentWindowHandle <- value

        /// <inheritdoc />
        member _.PayoffDialogSuppressed
            with get () = payoffDialogSuppressed
            and set value = payoffDialogSuppressed <- value

        /// <inheritdoc />
        member _.Dispose() = () // Stub has no resources to dispose

    /// Helper to initialise the stub from raw payload bytes.
    static member FromPayloads(payloads: seq<byte[]>) =
        payloads
        |> Seq.map (fun data -> Ok(Payload { Timestamp = None; Data = data }))
        |> fun x -> new JvLinkStub(x)

    /// Helper to trigger a watch event callback during tests.
    member _.RaiseEvent key =
        eventCallback |> Option.iter (fun cb -> cb key)

    member _.ConfigureCourseFileResponse(path: string, explanation: string) =
        courseFileResponse <- Some(path, explanation)
        courseFile2Response <- Some path

    member _.UseSilksFileResponder(responder: string * string -> Result<string option, ComError>) =
        silksFileResponder <- responder

    member _.UseSilksBinaryResponder(responder: string -> Result<byte[] option, ComError>) =
        silksBinaryResponder <- responder

    member _.ConfigureSaveFlag(flag: bool) = saveFlag <- flag

    member _.ConfigureJVLinkVersion(version: string) = jvLinkVersion <- version

    member _.ConfigureCurrentReadFileSize(size: int64) = currentReadFileSize <- size

    member _.ConfigureParentWindow(handle: IntPtr) = parentWindowHandle <- handle

    member _.ConfigurePayoffDialogSuppressed(flag: bool) = payoffDialogSuppressed <- flag

    member _.UseMovieAvailabilityResponder(responder: string -> Result<MovieAvailability, ComError>) =
        movieAvailabilityResponder <- responder

    member _.UseMovieAvailabilityWithTypeResponder(responder: string * string -> Result<MovieAvailability, ComError>) =
        movieAvailabilityWithTypeResponder <- responder

    member _.UseMoviePlayResponder(responder: string * string option -> Result<unit, ComError>) =
        moviePlayResponder <- responder

    member _.EnqueueMovieReadOutcome(outcome: Result<MovieReadOutcome, ComError>) = movieListQueue.Enqueue outcome

    member _.UiDialogInvocationCount = uiDialogInvocations

    member _.EnqueueRealtime(response: Result<JvReadOutcome, ComError>) = realtimeQueue.Enqueue response

    member _.LastServiceKey = lastServiceKey
    member _.CurrentSavePath = savePath

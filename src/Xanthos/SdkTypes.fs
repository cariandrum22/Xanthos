namespace Xanthos

open System

/// Original SDK code or HRESULT is retained independently of explanatory text.
[<RequireQualifiedAccess>]
type JvErrorKind =
    | Sdk
    | Invocation
    | InvalidInput
    | Disposed
    | Busy

/// Retains the failing API and original code; explanatory text is not a transport diagnosis.
/// SDK -413 can occur independently of Xanthos and does not imply retryability or an observed HTTP status.
/// See docs/sdk-known-limitations.md (SDK-COM-413).
type JvError =
    {
        Api: string
        Code: int option
        Kind: JvErrorKind
        /// Meaningful native out-values returned with a failure (for example, a damaged filename).
        Outputs: Map<string, string>
        Message: string
    }

/// JV-Link timestamps are local Japanese race-service times, with no implicit UTC conversion.
type OpenRequest =
    { Dataspec: string
      FromTime: DateTime
      ToTime: DateTime option
      Option: int }

type OpenMetadata =
    { ReadCount: int
      DownloadCount: int
      LastFileTimestamp: string }

[<RequireQualifiedAccess>]
type OpenOutcome =
    | Opened of OpenMetadata
    | NoData of OpenMetadata

/// JVRTOpen has no file-count or timestamp out-parameters.
[<RequireQualifiedAccess>]
type RealtimeOpenOutcome =
    | Opened
    | NoData

[<RequireQualifiedAccess>]
type ReadState =
    | Record
    | FileBoundary
    | DownloadPending
    | EndOfStream

/// Owned raw bytes and all read out-values; decoding is an explicit separate operation.
type ReadResult =
    { State: ReadState
      Data: byte[]
      RawText: string option
      Filename: string
      ByteCount: int
      BufferSize: int
      ReturnCode: int }

[<RequireQualifiedAccess>]
type ImageState =
    | Available
    | NoImage

type ImageResult<'T> =
    { State: ImageState
      Value: 'T
      ReturnCode: int }

type CourseImage =
    { Filepath: string
      Explanation: string }

[<RequireQualifiedAccess>]
type VideoAvailability =
    | Available
    | Unpublished
    | Missing
    | Unknown of int

[<RequireQualifiedAccess>]
type VideoOpenOutcome =
    | Opened
    | NoData

[<RequireQualifiedAccess>]
type VideoReadOutcome =
    | Record of text: string * byteCount: int * bufferSize: int
    | EndOfStream of text: string * bufferSize: int
    | DownloadPending of text: string * bufferSize: int

[<RequireQualifiedAccess>]
type EventKind =
    | Pay
    | JockeyChange
    | Weather
    | CourseChange
    | Avoid
    | TimeChange
    | Weight
    | Unknown of string

type JvEvent = { Kind: EventKind; RawKey: string }

type RealtimeRequest = { Dataspec: string; Key: string }

type ParsedEvent =
    { Original: JvEvent
      MeetingDate: DateTime
      CourseCode: string
      RaceNumber: string
      SentAt: DateTime option
      Request: RealtimeRequest }

type SubscriptionOptions =
    { Capacity: int
      CancellationToken: Threading.CancellationToken }

    static member Default =
        { Capacity = 256
          CancellationToken = Threading.CancellationToken.None }

type FileSizeKilobytes = { RawKilobytes: int; Bytes: int64 }

/// The functional API always connects to COM. Test doubles are injected only through internal test boundaries.
type ConnectionOptions =
    { ProgId: string }

    static member Default = { ProgId = "JVDTLab.JVLink" }

/// Implementation-only boundary: boxed values never escape the public function signatures.
type internal INativeJvLink =
    inherit IDisposable
    abstract Invoke: api: string * arguments: obj[] * byRefIndices: int list -> Result<obj, JvError>
    abstract Get: property: string -> Result<obj, JvError>
    abstract Put: property: string * value: obj -> Result<unit, JvError>
    abstract Watch: callback: (JvEvent -> unit) -> Result<unit, JvError>
    abstract StopWatch: unit -> Result<unit, JvError>

exception internal SessionCleanupException of JvError

/// Explicit cleanup reports failures without throwing from IDisposable during stack unwinding.
type internal INativeCleanup =
    abstract Cleanup: unit -> Result<unit, JvError>

/// Identifies synchronous reentry on the native owner even when the caller waits on another thread.
type internal INativeExecutionContext =
    abstract IsCurrentThread: bool

module internal CleanupFailure =
    let report (error: JvError) =
        try
            System.Diagnostics.Trace.TraceError($"{error.Api}: {error.Code}: {error.Message}")
        with _ ->
            () // A diagnostic listener must not replace the exception being unwound.

/// Owned event registration. Public functions expose shutdown and asynchronous delivery errors.
type Subscription internal (stop: unit -> Result<unit, JvError>, error: unit -> JvError option) =
    let mutable state = 0
    let mutable stopError: JvError option = None

    let mutable cancellation =
        Unchecked.defaultof<Threading.CancellationTokenRegistration>

    member internal _.Error =
        error () |> Option.orElseWith (fun () -> Threading.Volatile.Read(&stopError))

    member internal _.SetCancellation registration =
        cancellation <- registration

        if Threading.Volatile.Read(&state) = 2 then
            registration.Dispose()

    member internal _.Stop() =
        match Threading.Interlocked.CompareExchange(&state, 1, 0) with
        | 2 -> Ok()
        | 1 ->
            Error
                { Api = "unsubscribe"
                  Code = None
                  Kind = JvErrorKind.Busy
                  Outputs = Map.empty
                  Message = "Subscription shutdown is already in progress." }
        | _ ->
            let result =
                try
                    stop ()
                with
                | SessionCleanupException error -> Error error
                | ex ->
                    Error
                        { Api = "unsubscribe"
                          Code = Some ex.HResult
                          Kind = JvErrorKind.Invocation
                          Outputs = Map.empty
                          Message = ex.Message }

            match result with
            | Ok() ->
                Threading.Volatile.Write(&stopError, None)
                cancellation.Dispose()
                Threading.Volatile.Write(&state, 2)
            | Error error ->
                Threading.Volatile.Write(&stopError, Some error)
                Threading.Volatile.Write(&state, 0)

            result

    member this.Dispose() =
        match this.Stop() with
        | Ok() -> ()
        | Error error -> CleanupFailure.report error

    interface IDisposable with
        member this.Dispose() = this.Dispose()

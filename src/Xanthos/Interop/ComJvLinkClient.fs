namespace Xanthos.Interop

open System
open System.Reflection
open System.Runtime.InteropServices
open Xanthos.Core
open Xanthos.Core.Errors
open Xanthos.Core.ErrorCodes
open Xanthos.Interop.ComInterop

/// Exception raised when COM activation fails.
exception ComActivationException of ComFault

#if WINDOWS
module internal JvLinkLocale =
    [<DllImport("kernel32.dll", SetLastError = true)>]
    extern bool SetThreadLocale(uint32 locale)

    let initialize () =
        // JV-Link converts Japanese BSTR values through the native thread locale.
        // Configure only the owned STA, leaving the caller and OS settings intact.
        let culture = Globalization.CultureInfo.GetCultureInfo("ja-JP")
        Globalization.CultureInfo.CurrentCulture <- culture
        Globalization.CultureInfo.CurrentUICulture <- culture

        if not (SetThreadLocale(uint32 culture.LCID)) then
            raise (ComponentModel.Win32Exception(Marshal.GetLastWin32Error()))

/// Reflection-based JV-Link COM client implementation that avoids static COM references.
/// Implements IDisposable to properly release COM resources and prevent RCW leaks.
type internal ComClientActivation =
    { Resolve: string -> Type
      Create: Type -> obj
      Release: obj -> unit }

    static member Default =
        { Resolve = fun id -> Type.GetTypeFromProgID(id, throwOnError = false)
          Create = fun nativeType -> Activator.CreateInstance nativeType
          Release = fun instance -> Marshal.FinalReleaseComObject(instance) |> ignore }

type ComJvLinkClient
    internal (activation: ComClientActivation, ?useJvGets: bool, ?progId: string, ?eventConnector: ComEventConnector) as this
    =
    // A failed constructor has no complete ownership graph for finalization.
    // Enable the finalizer only after every field has been initialized.
    do GC.SuppressFinalize this

    let useJvGetsOverride = useJvGets
    let eventConnector = defaultArg eventConnector ComEventConnector.Default
    // Note: The ProgID is "JVDTLab.JVLink" (not "JVDTLabLib.JVLink")
    // JVDTLabLib is the type library name used in VB6 references
    let progId = defaultArg progId "JVDTLab.JVLink"
    let jvType = activation.Resolve progId

    do
        if isNull jvType then
            Diagnostics.emit
                $"COM activation failed: ProgID '{progId}' is not registered for this {IntPtr.Size * 8}-bit process."

            raise (
                ComActivationException
                    { Reason = ComFaultReason.ActivationFailure
                      Details = $"ProgID '{progId}' not registered."
                      Exception = None }
            )

    let dispatcher: IComDispatcher =
        new StaThreadDispatcher("JV-Link STA Dispatcher") :> IComDispatcher

    // Event sink for COM event delivery (instance-based, not global)
    let eventSink = JvLinkEventSink()
    let mutable eventSubscription: EventSubscription option = None
    let mutable disposeState = 0
    let lifetime = NativeSessionLifetime()

    let comObj =
        try
            dispatcher.Invoke(
                "JVLink.Activate",
                fun () ->
                    JvLinkLocale.initialize ()
                    let instance = activation.Create jvType
                    Diagnostics.emit $"COM activation succeeded for ProgID '{progId}' ({IntPtr.Size * 8}-bit)."
                    instance
            )
        with ex ->
            (dispatcher :> IDisposable).Dispose()
            reraise ()

    let invokeOnSta name work =
        if System.Threading.Volatile.Read(&disposeState) <> 0 then
            raise (ObjectDisposedException("ComJvLinkClient"))

        dispatcher.Invoke(name, work)

    let resultOnSta name work =
        try
            invokeOnSta name work
        with :? ObjectDisposedException ->
            Error(InvalidState $"{name}: JV-Link client has been disposed.")

    let nativeError api (ex: exn) : Xanthos.JvError =
        let rec cause (error: exn) =
            match error with
            | :? TargetInvocationException when not (isNull error.InnerException) -> cause error.InnerException
            | _ -> error

        let actual = cause ex

        match actual with
        | Xanthos.SessionCleanupException error -> error
        | _ ->
            { Api = api
              Code = Some actual.HResult
              Kind =
                if actual :? ObjectDisposedException then
                    Xanthos.JvErrorKind.Disposed
                else
                    Xanthos.JvErrorKind.Invocation
              Outputs = Map.empty
              Message = actual.Message }

    let nativeInvoke name flags (args: obj[]) byRefIndices =
        try
            invokeOnSta name (fun () ->
                let modifiers =
                    if List.isEmpty byRefIndices then
                        null
                    else
                        let mutable modifier = ParameterModifier(args.Length)

                        for index in byRefIndices do
                            modifier.[index] <- true

                        [| modifier |]

                Diagnostics.emit $"CALL {name} args={args.Length}"

                let value =
                    jvType.InvokeMember(name, flags, null, comObj, args, modifiers, null, null)

                match value with
                | :? int as code when flags = BindingFlags.InvokeMethod ->
                    lifetime.Observe(name, code)
                    Diagnostics.emit $"OK {name} code={code}"
                | _ -> ()

                Ok value)
        with ex ->
            Error(nativeError name ex)

    // The legacy interface is an adapter over the same operations as the functional API.
    // Its narrower historical return types remain lossy; use JvLink for all raw outputs.
    let legacySession =
        lazy
            (new Xanthos.Session(
                this :> Xanthos.INativeJvLink,
                dispatch = (fun work -> dispatcher.Invoke("legacy", work))
            ))

    let legacy result =
        result
        |> Result.mapError (fun (error: Xanthos.JvError) ->
            match error.Kind, error.Code with
            | Xanthos.JvErrorKind.Sdk, Some code ->
                match ErrorCodes.interpret error.Api code with
                | Error mapped -> mapped
                | Ok() -> Unexpected error.Message
            | Xanthos.JvErrorKind.Invocation, Some code -> CommunicationFailure(code, error.Message)
            | Xanthos.JvErrorKind.InvalidInput, _ -> InvalidInput error.Message
            | _ -> InvalidState error.Message)

    let run operation = operation legacySession.Value |> legacy

    let timestamp () =
        Xanthos.SdkOperations.getCurrentFileTimestamp legacySession.Value
        |> Result.map (fun raw ->
            match
                DateTime.TryParseExact(
                    raw,
                    "yyyyMMddHHmmss",
                    Globalization.CultureInfo.InvariantCulture,
                    Globalization.DateTimeStyles.None
                )
            with
            | true, parsed -> Some parsed
            | _ -> None)
        |> legacy

    let mapOpen =
        function
        | Xanthos.OpenOutcome.Opened metadata ->
            { HasData = true
              ReadCount = metadata.ReadCount
              DownloadCount = metadata.DownloadCount
              LastFileTimestamp =
                if metadata.LastFileTimestamp = "" then
                    None
                else
                    Some metadata.LastFileTimestamp }
        | Xanthos.OpenOutcome.NoData metadata ->
            { HasData = false
              ReadCount = metadata.ReadCount
              DownloadCount = metadata.DownloadCount
              LastFileTimestamp =
                if metadata.LastFileTimestamp = "" then
                    None
                else
                    Some metadata.LastFileTimestamp }

    let videoAvailability =
        function
        | Xanthos.VideoAvailability.Available -> Ok MovieAvailability.Available
        | Xanthos.VideoAvailability.Unpublished -> Ok MovieAvailability.Unavailable
        | Xanthos.VideoAvailability.Missing -> Ok MovieAvailability.NotFound
        | Xanthos.VideoAvailability.Unknown code ->
            Error(InvalidState $"Legacy MovieAvailability cannot represent SDK code {code}; use JvLink.movieCheck.")

    let useGets () =
        let isTrue (value: string) =
            not (String.IsNullOrWhiteSpace value)
            && not (List.contains (value.Trim().ToLowerInvariant()) [ "0"; "false"; "no"; "off" ])

        match useJvGetsOverride with
        | Some value -> value
        | None ->
            let read = Environment.GetEnvironmentVariable "XANTHOS_USE_JVREAD"
            let gets = Environment.GetEnvironmentVariable "XANTHOS_USE_JVGETS"

            if not (isNull read) then not (isTrue read)
            elif not (isNull gets) then isTrue gets
            else true

    do GC.ReRegisterForFinalize this

    new(?useJvGets: bool, ?progId: string) =
        new ComJvLinkClient(ComClientActivation.Default, ?useJvGets = useJvGets, ?progId = progId)

    interface IJvLinkClient with
        member _.Init sid = run (Xanthos.SdkOperations.init sid)

        member _.Open request =
            run (
                Xanthos.SdkOperations.openData
                    { Dataspec = request.Spec
                      FromTime = request.FromTime
                      ToTime = None
                      Option = request.Option }
            )
            |> Result.map mapOpen

        member _.OpenRealtime(spec, key) =
            run (Xanthos.SdkOperations.openRealtime spec key)
            |> Result.map (fun outcome ->
                // The legacy result requires counts that JVRTOpen does not supply.
                { HasData = outcome = Xanthos.RealtimeOpenOutcome.Opened
                  ReadCount = 0
                  DownloadCount = 0
                  LastFileTimestamp = None })

        member _.Read() =
            run (
                if useGets () then
                    Xanthos.SdkOperations.gets
                else
                    Xanthos.SdkOperations.read
            )
            |> Result.map (fun result ->
                match result.State with
                | Xanthos.ReadState.Record ->
                    Payload
                        { Data = result.Data
                          Timestamp = timestamp () |> Result.defaultValue None }
                | Xanthos.ReadState.FileBoundary -> FileBoundary
                | Xanthos.ReadState.DownloadPending -> DownloadPending
                | Xanthos.ReadState.EndOfStream -> EndOfStream)

        member _.Gets(buffer: byref<string>, bufferSize: int, filename: byref<string>) =
            match run (Xanthos.SdkOperations.getsWithCapacity bufferSize) with
            | Error error -> Error error
            | Ok result ->
                buffer <- Text.decodeShiftJis result.Data
                filename <- result.Filename
                Ok result.ReturnCode

        member _.Close() =
            run Xanthos.SdkOperations.closeData |> ignore

        member _.Status() = run Xanthos.SdkOperations.status
        member _.Skip() = run Xanthos.SdkOperations.skip
        member _.Cancel() = run Xanthos.SdkOperations.cancel

        member _.DeleteFile filename =
            run (Xanthos.SdkOperations.deleteFile filename)

        member _.WatchEvent callback =
            run (Xanthos.SdkOperations.watchEvent (fun event -> callback event.RawKey))

        member _.WatchEventClose() =
            run Xanthos.SdkOperations.watchEventClose

        member _.SetUiProperties() = run Xanthos.SdkOperations.configureUi

        member _.SetSaveFlag enabled =
            run (Xanthos.SdkOperations.setSaveFlag enabled)

        member _.SetServiceKeyDirect key =
            run (Xanthos.SdkOperations.setServiceKey key)

        member _.SetSavePathDirect path =
            run (Xanthos.SdkOperations.setSavePath path)

        member _.SetParentWindowHandleDirect handle =
            run (Xanthos.SdkOperations.setParentWindowHandle handle)

        member _.SetPayoffDialogSuppressedDirect _ =
            Error(InvalidState "m_payflag is read-only; use JVSetUIProperties.")

        member _.CourseFile key =
            run (Xanthos.SdkOperations.courseFile key)
            |> Result.bind (fun image ->
                if image.State = Xanthos.ImageState.Available then
                    Ok(image.Value.Filepath, image.Value.Explanation)
                else
                    Error(InvalidInput "No matching data exists"))

        member _.CourseFile2(key, path) =
            run (Xanthos.SdkOperations.courseFile2 key path)
            |> Result.bind (fun image ->
                if image.State = Xanthos.ImageState.Available then
                    Ok()
                else
                    Error(InvalidInput "No matching data exists"))

        member _.SilksFile(pattern, path) =
            run (Xanthos.SdkOperations.silksFile pattern path)
            |> Result.map (fun image ->
                if image.State = Xanthos.ImageState.Available then
                    Some image.Value
                else
                    None)

        member _.SilksBinary pattern =
            run (Xanthos.SdkOperations.silksBinary pattern)
            |> Result.map (fun image ->
                if image.State = Xanthos.ImageState.Available then
                    Some image.Value
                else
                    None)

        member _.MovieCheck key =
            run (Xanthos.SdkOperations.movieCheck key) |> Result.bind videoAvailability

        member _.MovieCheckWithType(movieType, key) =
            run (Xanthos.SdkOperations.movieCheckWithType movieType key)
            |> Result.bind videoAvailability

        member _.MoviePlay key =
            run (Xanthos.SdkOperations.moviePlay key)

        member _.MoviePlayWithType(movieType, key) =
            run (Xanthos.SdkOperations.moviePlayWithType movieType key)

        member _.MovieOpen(movieType, key) =
            run (Xanthos.SdkOperations.movieOpen movieType key)
            |> Result.bind (function
                | Xanthos.VideoOpenOutcome.Opened -> Ok()
                | Xanthos.VideoOpenOutcome.NoData ->
                    Error(InvalidState "No movie data; use JvLink.movieOpen to distinguish NoData."))

        member _.MovieRead() =
            run Xanthos.SdkOperations.movieRead
            |> Result.bind (function
                | Xanthos.VideoReadOutcome.EndOfStream _ -> Ok MovieEnd
                | Xanthos.VideoReadOutcome.Record(text, _, _) -> Ok(MovieRecord(WorkoutVideoListing.parse text))
                | Xanthos.VideoReadOutcome.DownloadPending _ ->
                    Error(InvalidState "Movie download pending; use JvLink.movieRead to distinguish DownloadPending."))

        member _.TryGetSaveFlag() =
            run Xanthos.SdkOperations.getSaveFlag |> Result.map ((<>) 0)

        member _.TryGetSavePath() = run Xanthos.SdkOperations.getSavePath
        member _.TryGetServiceKey() = run Xanthos.SdkOperations.getServiceKey
        member _.TryGetJVLinkVersion() = run Xanthos.SdkOperations.getVersion

        member _.TryGetTotalReadFileSize() =
            run Xanthos.SdkOperations.getTotalReadFileSize
            |> Result.map (fun size -> int64 size.RawKilobytes)

        member _.TryGetCurrentReadFileSize() =
            run Xanthos.SdkOperations.getCurrentReadFileSize |> Result.map int64

        member _.TryGetCurrentFileTimestamp() = timestamp ()

        member _.TryGetParentWindowHandle() =
            Error(InvalidState "ParentHWnd is write-only.")

        member _.TryGetPayoffDialogSuppressed() =
            run Xanthos.SdkOperations.getPayFlag |> Result.map ((<>) 0)

        member _.SaveFlag
            with get () = (this :> IJvLinkClient).TryGetSaveFlag() |> Result.defaultValue false
            and set value = run (Xanthos.SdkOperations.setSaveFlag value) |> ignore

        member _.SavePath = (this :> IJvLinkClient).TryGetSavePath() |> Result.defaultValue ""

        member _.ServiceKey =
            (this :> IJvLinkClient).TryGetServiceKey() |> Result.defaultValue ""

        member _.JVLinkVersion =
            (this :> IJvLinkClient).TryGetJVLinkVersion() |> Result.defaultValue ""

        member _.TotalReadFileSize =
            (this :> IJvLinkClient).TryGetTotalReadFileSize() |> Result.defaultValue 0L

        member _.CurrentReadFileSize =
            (this :> IJvLinkClient).TryGetCurrentReadFileSize() |> Result.defaultValue 0L

        member _.CurrentFileTimestamp = timestamp () |> Result.defaultValue None

        member _.ParentWindowHandle
            with get () = IntPtr.Zero
            and set value = run (Xanthos.SdkOperations.setParentWindowHandle value) |> ignore

        member _.PayoffDialogSuppressed
            with get () =
                (this :> IJvLinkClient).TryGetPayoffDialogSuppressed()
                |> Result.defaultValue false
            and set _ = Diagnostics.emit "m_payflag is read-only; use JVSetUIProperties."

    /// Runs only on the owning STA, including deferred cleanup after a timed-out call.
    member private _.ReleaseResources() =
        let close api : Result<int, Xanthos.JvError> =
            Diagnostics.emit $"DISPOSE {api} begin"

            match jvType.InvokeMember(api, BindingFlags.InvokeMethod, null, comObj, [||]) with
            | :? int as code ->
                Diagnostics.emit $"DISPOSE {api} code={code}"
                Ok code
            | _ ->
                Error
                    { Api = api
                      Code = None
                      Kind = Xanthos.JvErrorKind.Invocation
                      Outputs = Map.empty
                      Message = "Expected a 32-bit SDK return code during cleanup." }

        let detach () =
            eventSink.ClearCallback()
            eventSubscription |> Option.iter eventConnector.Disconnect
            eventSubscription <- None

        let release () =
            if not (isNull comObj) then
                Diagnostics.emit "DISPOSE FinalReleaseComObject begin"
                activation.Release comObj
                Diagnostics.emit $"COM object released for ProgID '{progId}'."

        lifetime.Cleanup(close, detach, release)

    member private this.Cleanup() =
        if System.Threading.Interlocked.CompareExchange(&disposeState, 1, 0) <> 0 then
            Ok()
        else
            let mutable result = Ok()

            let remember error =
                if Result.isOk result then
                    result <- Error error

            try
                try
                    if legacySession.IsValueCreated then
                        legacySession.Value.ReleaseDelivery()
                with ex ->
                    remember (nativeError "eventDelivery.shutdown" ex)

                try
                    match dispatcher.Invoke("JVLink.Dispose", this.ReleaseResources) with
                    | Error error -> remember error
                    | Ok() -> ()
                with ex ->
                    remember (nativeError "dispose" ex)

                try
                    Diagnostics.emit "DISPOSE STA shutdown begin"
                    (dispatcher :> IDisposable).Dispose()
                    Diagnostics.emit "DISPOSE STA shutdown complete"
                with ex ->
                    remember (nativeError "STA.shutdown" ex)
            finally
                System.Threading.Volatile.Write(&disposeState, 2)
                GC.SuppressFinalize(this)

            result

    member private this.Abandon(timeout: TimeSpan) =
        if System.Threading.Interlocked.CompareExchange(&disposeState, 1, 0) = 0 then
            let cleanup () =
                try
                    // The native call has returned before the STA reaches this callback.
                    if legacySession.IsValueCreated then
                        legacySession.Value.ReleaseDelivery()
                finally
                    try
                        match this.ReleaseResources() with
                        | Error error -> Xanthos.CleanupFailure.report error
                        | Ok() -> ()
                    finally
                        System.Threading.Volatile.Write(&disposeState, 2)

            let completed = (dispatcher :?> IComShutdown).Shutdown(timeout, cleanup)

            if not completed then
                Diagnostics.emit
                    "STA shutdown deferred: native work is still running; cleanup remains on its original STA."

            GC.SuppressFinalize(this)

    interface Xanthos.INativeCleanup with
        member this.Cleanup() = this.Cleanup()

    interface IComAbandonable with
        member this.Abandon() = this.Abandon(TimeSpan.FromSeconds 5.)

    interface IDisposable with
        member this.Dispose() =
            match this.Cleanup() with
            | Ok() -> ()
            | Error error -> Xanthos.CleanupFailure.report error

    interface INativeWatchEventSource with
        member _.WatchNativeEvent callback =
            run (Xanthos.SdkOperations.watchEvent callback)

    interface IComDispatchProvider with
        member _.Dispatcher = dispatcher

    interface Xanthos.INativeJvLink with
        member _.Invoke(name, args, indices) =
            nativeInvoke name BindingFlags.InvokeMethod args indices

        member _.Get name =
            nativeInvoke name BindingFlags.GetProperty [||] []

        member _.Put(name, value) =
            nativeInvoke name BindingFlags.SetProperty [| value |] [] |> Result.map ignore

        member _.Watch callback =
            try
                invokeOnSta "JVWatchEvent" (fun () ->
                    if eventSubscription.IsSome then
                        Error
                            { Api = "JVWatchEvent"
                              Code = None
                              Kind = Xanthos.JvErrorKind.Busy
                              Outputs = Map.empty
                              Message = "A watch subscription is already active." }
                    else
                        eventSink.SetNativeCallback callback

                        match eventConnector.Connect comObj eventSink with
                        | Error error ->
                            eventSink.ClearCallback()
                            Error error
                        | Ok subscription ->
                            match nativeInvoke "JVWatchEvent" BindingFlags.InvokeMethod [||] [] with
                            | Ok(:? int as code) when code = 0 ->
                                eventSubscription <- Some subscription
                                Ok()
                            | result ->
                                eventConnector.Disconnect subscription
                                eventSink.ClearCallback()

                                match result with
                                | Error error -> Error error
                                | Ok value ->
                                    Error
                                        { Api = "JVWatchEvent"
                                          Code =
                                            (match value with
                                             | :? int as code -> Some code
                                             | _ -> None)
                                          Kind = Xanthos.JvErrorKind.Sdk
                                          Outputs = Map.empty
                                          Message = "SDK watch registration failed." })
            with ex ->
                Error(nativeError "JVWatchEvent" ex)

        member _.StopWatch() =
            try
                invokeOnSta "JVWatchEventClose" (fun () ->
                    let closeResult =
                        if lifetime.Watching then
                            nativeInvoke "JVWatchEventClose" BindingFlags.InvokeMethod [||] []
                        else
                            Ok(box 0)

                    match closeResult with
                    | Ok(:? int as code) when code = 0 ->
                        eventSink.ClearCallback()
                        eventSubscription |> Option.iter eventConnector.Disconnect
                        eventSubscription <- None
                        Ok()
                    | Error error -> Error error
                    | Ok value ->
                        Error
                            { Api = "JVWatchEventClose"
                              Code =
                                (match value with
                                 | :? int as code -> Some code
                                 | _ -> None)
                              Kind = Xanthos.JvErrorKind.Sdk
                              Outputs = Map.empty
                              Message = "SDK watch shutdown failed." })
            with ex ->
                Error(nativeError "JVWatchEventClose" ex)

    override this.Finalize() =
        // Finalization must never terminate the process, including shutdown failures.
        try
            this.Abandon(TimeSpan.Zero)
        with _ ->
            ()
#else
module ComJvLinkClient =
    let notAvailable () =
        failwith "COM interop unavailable on this platform."
#endif

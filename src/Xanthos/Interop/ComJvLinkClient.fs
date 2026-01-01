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
/// Reflection-based JV-Link COM client implementation that avoids static COM references.
/// Implements IDisposable to properly release COM resources and prevent RCW leaks.
type ComJvLinkClient(?useJvGets: bool) =
    let useJvGetsOverride = useJvGets
    // Note: The ProgID is "JVDTLab.JVLink" (not "JVDTLabLib.JVLink")
    // JVDTLabLib is the type library name used in VB6 references
    let progId = "JVDTLab.JVLink"
    let jvType = Type.GetTypeFromProgID(progId, throwOnError = false)

    do
        if isNull jvType then
            Diagnostics.emit $"COM activation failed: ProgID '{progId}' not found. Falling back to stub."

            raise (
                ComActivationException
                    { Reason = ComFaultReason.ActivationFailure
                      Details = $"ProgID '{progId}' not registered."
                      Exception = None }
            )

    let dispatcher: IComDispatcher =
        new StaThreadDispatcher("JV-Link STA Dispatcher") :> IComDispatcher

    let syncRoot = obj ()
    // Reusable byte buffer for JVGets to avoid repeated allocations
    let getsBuffer = Array.zeroCreate<byte> 65536
    let getsFilename = System.Text.StringBuilder(260)
    let clear (sb: System.Text.StringBuilder) = sb.Clear() |> ignore
    // Event sink for COM event delivery (instance-based, not global)
    let eventSink = JvLinkEventSink()
    let mutable eventSubscription: EventSubscription option = None
    let mutable disposed = false

    let comObj =
        try
            dispatcher.Invoke(
                "JVLink.Activate",
                fun () ->
                    Diagnostics.emit $"COM activation succeeded for ProgID '{progId}'."
                    Activator.CreateInstance jvType
            )
        with ex ->
            (dispatcher :> IDisposable).Dispose()
            reraise ()

    let invokeOnSta name work = dispatcher.Invoke(name, work)

    let invoke (name: string) (args: obj[]) : obj =
        invokeOnSta name (fun () ->
            lock syncRoot (fun () ->
                Diagnostics.emit $"CALL {name} args={args.Length}"

                try
                    let rv = jvType.InvokeMember(name, BindingFlags.InvokeMethod, null, comObj, args)
                    let typeName = if isNull rv then "null" else rv.GetType().Name
                    Diagnostics.emit ($"RET {name} type={typeName}")
                    rv
                with ex ->
                    Diagnostics.emit $"ERR {name} ex={ex.Message}"

                    raise (
                        ComActivationException
                            { Reason = ComFaultReason.InvocationFailure
                              Details = $"Invocation failure calling {name}: {ex.Message}"
                              Exception = Some ex }
                    )))

    let invokeCode name args : int =
        match invoke name args with
        | :? int as i ->
            Diagnostics.emit $"OK {name} code={i}"
            i
        | null ->
            Diagnostics.emit $"OK {name} code=0 (null)"
            0
        | other ->
            Diagnostics.emit $"WARN unexpected return type from {name}: {other.GetType().Name}"
            0

    /// Invokes a COM method with explicit ByRef parameter handling.
    /// byRefIndexes specifies which parameter indexes should be passed by reference.
    let invokeWithByRef (name: string) (args: obj[]) (byRefIndexes: int list) : int =
        invokeOnSta name (fun () ->
            lock syncRoot (fun () ->
                Diagnostics.emit $"CALL {name} args={args.Length}"

                try
                    let modifiers =
                        if List.isEmpty byRefIndexes then
                            null
                        else
                            let mutable pm = ParameterModifier(args.Length)

                            for idx in byRefIndexes do
                                if idx >= 0 && idx < args.Length then
                                    pm.[idx] <- true

                            [| pm |]

                    let rv =
                        jvType.InvokeMember(
                            name,
                            BindingFlags.InvokeMethod,
                            null,
                            comObj,
                            args,
                            modifiers,
                            null,
                            null
                        )

                    match rv with
                    | :? int as i ->
                        Diagnostics.emit $"OK {name} code={i}"
                        i
                    | null ->
                        Diagnostics.emit $"OK {name} code=0 (null)"
                        0
                    | other ->
                        Diagnostics.emit $"WARN unexpected return type from {name}: {other.GetType().Name}"
                        0
                with ex ->
                    Diagnostics.emit $"ERR {name} ex={ex.Message}"

                    raise (
                        ComActivationException
                            { Reason = ComFaultReason.InvocationFailure
                              Details = $"Invocation failure calling {name}: {ex.Message}"
                              Exception = Some ex }
                    )))

    let getPropertyInt name =
        invokeOnSta $"get-{name}" (fun () ->
            lock syncRoot (fun () ->
                try
                    jvType.InvokeMember(name, BindingFlags.GetProperty, null, comObj, [||]) :?> int
                with ex ->
                    Diagnostics.emit $"ERR reading property {name}: {ex.Message}"
                    0))

    let getPropertyString name =
        invokeOnSta $"get-{name}" (fun () ->
            lock syncRoot (fun () ->
                try
                    match jvType.InvokeMember(name, BindingFlags.GetProperty, null, comObj, [||]) with
                    | :? string as s -> s
                    | null -> ""
                    | _ -> ""
                with ex ->
                    Diagnostics.emit $"ERR reading property {name}: {ex.Message}"
                    ""))

    let getPropertyInt64 name =
        invokeOnSta $"get-{name}" (fun () ->
            lock syncRoot (fun () ->
                try
                    match jvType.InvokeMember(name, BindingFlags.GetProperty, null, comObj, [||]) with
                    | :? int64 as i -> i
                    | :? int as i -> int64 i
                    | _ -> 0L
                with ex ->
                    Diagnostics.emit $"ERR reading property {name}: {ex.Message}"
                    0L))

    // Result-returning property getters for explicit error handling
    let tryGetPropertyInt name : Result<int, ComError> =
        invokeOnSta $"try-get-{name}" (fun () ->
            lock syncRoot (fun () ->
                try
                    Ok(jvType.InvokeMember(name, BindingFlags.GetProperty, null, comObj, [||]) :?> int)
                with ex ->
                    Diagnostics.emit $"ERR reading property {name}: {ex.Message}"
                    Error(Unexpected $"Failed to read property {name}: {ex.Message}")))

    let tryGetPropertyString name : Result<string, ComError> =
        invokeOnSta $"try-get-{name}" (fun () ->
            lock syncRoot (fun () ->
                try
                    match jvType.InvokeMember(name, BindingFlags.GetProperty, null, comObj, [||]) with
                    | :? string as s -> Ok s
                    | null -> Ok ""
                    | _ -> Ok ""
                with ex ->
                    Diagnostics.emit $"ERR reading property {name}: {ex.Message}"
                    Error(Unexpected $"Failed to read property {name}: {ex.Message}")))

    let tryGetPropertyInt64 name : Result<int64, ComError> =
        invokeOnSta $"try-get-{name}" (fun () ->
            lock syncRoot (fun () ->
                try
                    match jvType.InvokeMember(name, BindingFlags.GetProperty, null, comObj, [||]) with
                    | :? int64 as i -> Ok i
                    | :? int as i -> Ok(int64 i)
                    | _ -> Ok 0L
                with ex ->
                    Diagnostics.emit $"ERR reading property {name}: {ex.Message}"
                    Error(Unexpected $"Failed to read property {name}: {ex.Message}")))

    let tryGetPropertyIntPtr name : Result<IntPtr, ComError> =
        invokeOnSta $"try-get-{name}" (fun () ->
            lock syncRoot (fun () ->
                try
                    match jvType.InvokeMember(name, BindingFlags.GetProperty, null, comObj, [||]) with
                    | :? int64 as i -> Ok(IntPtr i)
                    | :? int as i -> Ok(IntPtr i)
                    | :? IntPtr as p -> Ok p
                    | _ -> Ok IntPtr.Zero
                with ex ->
                    Diagnostics.emit $"ERR reading property {name}: {ex.Message}"
                    Error(Unexpected $"Failed to read property {name}: {ex.Message}")))

    let tryGetPropertyBool name : Result<bool, ComError> =
        tryGetPropertyInt name |> Result.map (fun v -> v <> 0)

    let setPropertyInt name (value: int) : Result<unit, ComError> =
        invokeOnSta $"set-{name}" (fun () ->
            lock syncRoot (fun () ->
                try
                    jvType.InvokeMember(name, BindingFlags.SetProperty, null, comObj, [| box value |])
                    |> ignore

                    Diagnostics.emit $"OK set property {name}={value}"
                    Ok()
                with ex ->
                    Diagnostics.emit $"ERR setting property {name}: {ex.Message}"
                    Error(Unexpected ex.Message)))

    let setPropertyString name (value: string) : Result<unit, ComError> =
        invokeOnSta $"set-{name}" (fun () ->
            lock syncRoot (fun () ->
                try
                    jvType.InvokeMember(name, BindingFlags.SetProperty, null, comObj, [| box value |])
                    |> ignore

                    Diagnostics.emit $"OK set property {name}=\"{value}\""
                    Ok()
                with ex ->
                    Diagnostics.emit $"ERR setting property {name}: {ex.Message}"
                    Error(Unexpected ex.Message)))

    let interpretCode methodName code = ErrorCodes.interpret methodName code

    let ensureSuccess methodName code =
        match interpretCode methodName code with
        | Ok() ->
            Diagnostics.emit $"OK {methodName} code={code}"
            Ok()
        | Error e ->
            Diagnostics.emit $"FAIL {methodName} code={code} -> {e}"
            Error e

    // Helper to read and parse m_CurrentFileTimestamp after a successful JVRead/JVGets call.
    // Returns None if the property is empty, "00000000000000", or cannot be parsed.
    let getCurrentTimestamp () : DateTime option =
        match tryGetPropertyString "m_CurrentFileTimestamp" with
        | Ok timestamp when not (String.IsNullOrWhiteSpace timestamp) && timestamp <> "00000000000000" ->
            match
                DateTime.TryParseExact(
                    timestamp,
                    "yyyyMMddHHmmss",
                    Globalization.CultureInfo.InvariantCulture,
                    Globalization.DateTimeStyles.None
                )
            with
            | true, dt -> Some dt
            | false, _ -> None
        | _ -> None

    // Determines whether to use JVGets (byte array) instead of JVRead (BSTR).
    // Priority: 1) constructor parameter, 2) XANTHOS_USE_JVGETS environment variable
    let checkUseJvGets () =
        match useJvGetsOverride with
        | Some value ->
            Diagnostics.emit $"UseJvGets override={value} (from config)"
            value
        | None ->
            let envValue = Environment.GetEnvironmentVariable("XANTHOS_USE_JVGETS")
            // Normalize: trim whitespace and convert to lowercase for case-insensitive comparison
            let normalized =
                if isNull envValue then
                    ""
                else
                    envValue.Trim().ToLowerInvariant()

            let result =
                match normalized with
                | ""
                | "0"
                | "false"
                | "no"
                | "off" -> false
                | _ -> true

            Diagnostics.emit $"XANTHOS_USE_JVGETS env='{envValue}' -> useJvGets={result}"
            result

    // JVRead implementation: uses BSTR with UTF-16 byte extraction
    // JVRead returns data as BSTR. JV-Link writes Shift-JIS bytes to the BSTR buffer, but COM
    // interprets these as Unicode code units. We extract the low bytes from each char to recover
    // the original Shift-JIS bytes.
    let readRecordViaJvRead () : Result<JvReadOutcome, ComError> =
        let size = 65536
        // Pass empty strings; COM will populate them as ByRef parameters
        let args: obj[] = [| ""; size; "" |]
        // Use invokeWithByRef to properly handle ByRef parameters (buff=0, filename=2)
        let code = invokeWithByRef "JVRead" args [ 0; 2 ]

        if code = 0 then
            Ok EndOfStream
        elif code = -1 then
            Ok FileBoundary
        elif code = -3 then
            Ok DownloadPending
        elif code < -1 then
            match interpretCode "JVRead" code with
            | Ok() -> Ok EndOfStream
            | Error e -> Error e
        else
            let readBytes = code
            // Read back the modified string from the args array (COM should have updated it)
            let buffStr =
                match args.[0] with
                | :? string as s -> s
                | _ -> ""
            // JV-Link writes Shift-JIS bytes directly to the BSTR buffer.
            // COM interprets each byte as a UTF-16 code unit (character).
            // To recover the original Shift-JIS bytes, extract the low byte from each character.
            let actualData =
                if String.IsNullOrEmpty buffStr then
                    Array.empty
                else
                    // Each character holds one Shift-JIS byte in its low byte
                    let count = min readBytes buffStr.Length
                    buffStr.ToCharArray().[0 .. count - 1] |> Array.map (fun c -> byte c)

            Ok(
                Payload
                    { Timestamp = getCurrentTimestamp ()
                      Data = actualData }
            )

    // JVGets implementation: uses SAFEARRAY<byte> for raw Shift-JIS bytes
    // This avoids string marshalling issues.
    let readRecordViaJvGets () : Result<JvReadOutcome, ComError> =
        // Use same buffer size as JVRead (64KB) to handle large records (analysis/master data)
        // Per spec, 'size' tells JV-Link the max bytes to copy - truncation occurs if too small
        let size = 65536
        clear getsFilename
        // Prepare SAFEARRAY<byte> placeholder. Passing an empty byte[] lets the COM interop layer allocate a new one.
        let mutable bytesHolder: obj = box (Array.zeroCreate<byte> 0)
        // JVGets expects: buff() As Byte (ByRef), size As Long, fname As String (ByRef)
        // Pass empty string for fname - COM will write back the filename
        let args: obj[] = [| bytesHolder; size; "" |]
        // Use invokeWithByRef with indices [0; 2] to properly handle ByRef parameters
        let code = invokeWithByRef "JVGets" args [ 0; 2 ]
        Diagnostics.emit $"JVGets returned code={code}"

        if code = 0 then
            Ok EndOfStream
        elif code = -1 then
            Ok FileBoundary
        elif code = -3 then
            Ok DownloadPending
        elif code < -1 then
            match interpretCode "JVGets" code with
            | Ok() -> Ok EndOfStream
            | Error e -> Error e
        else
            // Extract resulting SAFEARRAY<Byte> from ByRef parameter
            let extractedBytes =
                match args.[0] with
                | :? (byte[]) as direct -> direct
                | :? Array as a when a.GetType().GetElementType() = typeof<byte> ->
                    let tmp = Array.zeroCreate<byte> a.Length

                    for i in 0 .. a.Length - 1 do
                        match a.GetValue i with
                        | :? byte as b -> tmp.[i] <- b
                        | _ -> ()

                    tmp
                | _ -> Array.empty

            Diagnostics.emit $"JVGets extractedBytes.Length={extractedBytes.Length}"
            // Copy only the bytes indicated by the return code
            let actualData =
                if code <= extractedBytes.Length then
                    Array.sub extractedBytes 0 code
                else
                    extractedBytes

            Diagnostics.emit $"JVGets actualData.Length={actualData.Length}"

            Ok(
                Payload
                    { Timestamp = getCurrentTimestamp ()
                      Data = actualData }
            )
    // Note: Avoid forced GC here - unpinning the handle is sufficient.
    // Forced GC per-record causes significant performance degradation.

    // Main read dispatcher - selects implementation based on environment variable
    let readRecord () : Result<JvReadOutcome, ComError> =
        if checkUseJvGets () then
            Diagnostics.emit "Using JVGets path (XANTHOS_USE_JVGETS=1)"
            readRecordViaJvGets ()
        else
            readRecordViaJvRead ()

    let mapJvGetsCode (code: int) : Result<int, ComError> =
        if code >= 0 then
            Ok code
        else
            match code with
            | -1 -> Ok -1
            | -3 -> Ok -3
            | other ->
                match interpretCode "JVGets" other with
                | Ok() -> Ok 0
                | Error e -> Error e

    let mapComFault methodName (fault: ComFault) =
        let reasonText =
            match fault.Reason with
            | ComFaultReason.ActivationFailure -> "COM activation failure"
            | ComFaultReason.MethodResolutionFailure -> "COM method resolution failure"
            | ComFaultReason.InvocationFailure -> "COM invocation failure"

        let methodPart =
            if String.IsNullOrWhiteSpace methodName then
                reasonText
            else
                $"{reasonText} during {methodName}"

        let message =
            if String.IsNullOrWhiteSpace fault.Details then
                methodPart
            else
                $"{methodPart}: {fault.Details}"

        CommunicationFailure(ErrorCodes.ComConnectionFailure, message)

    let protect methodName (work: unit -> Result<'a, ComError>) =
        try
            work ()
        with ComActivationException fault ->
            Error(mapComFault methodName fault)

    interface IJvLinkClient with
        member _.Init sid =
            protect "JVInit" (fun () -> ensureSuccess "JVInit" (invokeCode "JVInit" [| sid |]))

        member _.Open request =
            protect "JVOpen" (fun () ->
                // JVOpen signature: spec, fromTime, option, readcount (ByRef), downloadcount (ByRef), lastfiletimestamp (ByRef)
                // Initialize ByRef parameters with default values
                let args: obj[] =
                    [| request.Spec
                       request.FromTime.ToString("yyyyMMddHHmmss")
                       request.Option
                       box 0 // readcount (ByRef out)
                       box 0 // downloadcount (ByRef out)
                       box "" |] // lastfiletimestamp (ByRef out)
                // Use invokeWithByRef with indices [3; 4; 5] for ByRef parameters
                let code = invokeWithByRef "JVOpen" args [ 3; 4; 5 ]

                // Extract ByRef output values
                let readCount =
                    match args.[3] with
                    | :? int as i -> i
                    | _ -> 0

                let downloadCount =
                    match args.[4] with
                    | :? int as i -> i
                    | _ -> 0

                let lastFileTimestamp =
                    match args.[5] with
                    | :? string as s when not (String.IsNullOrWhiteSpace s) -> Some s
                    | _ -> None

                Diagnostics.emit
                    $"JVOpen ByRef: readCount={readCount}, downloadCount={downloadCount}, lastFileTimestamp={lastFileTimestamp}"

                // JVOpen returns -1 when no matching data exists for the specified parameters.
                // This is a normal condition - return HasData=false to indicate no data available.
                if code = -1 then
                    Diagnostics.emit "OK JVOpen code=-1 (no matching data)"

                    Ok
                        { HasData = false
                          ReadCount = readCount
                          DownloadCount = downloadCount
                          LastFileTimestamp = lastFileTimestamp }
                else
                    ensureSuccess "JVOpen" code
                    |> Result.map (fun () ->
                        { HasData = true
                          ReadCount = readCount
                          DownloadCount = downloadCount
                          LastFileTimestamp = lastFileTimestamp }))

        member _.OpenRealtime(spec, key) =
            protect "JVRTOpen" (fun () ->
                let code = invokeCode "JVRTOpen" [| spec; key |]
                // JVRTOpen returns -1 when no matching data exists for the specified parameters.
                // Note: JVRTOpen does not have ByRef parameters like JVOpen
                if code = -1 then
                    Diagnostics.emit "OK JVRTOpen code=-1 (no matching data)"

                    Ok
                        { HasData = false
                          ReadCount = 0
                          DownloadCount = 0
                          LastFileTimestamp = None }
                else
                    ensureSuccess "JVRTOpen" code
                    |> Result.map (fun () ->
                        { HasData = true
                          ReadCount = 0
                          DownloadCount = 0
                          LastFileTimestamp = None }))

        member _.Read() = protect "JVRead" readRecord

        member _.Gets(buffer: byref<string>, bufferSize: int, filename: byref<string>) =
            // Cannot use 'protect' here because byref parameters cannot be captured by closures.
            try
                // Prepare SAFEARRAY<byte> placeholder. Passing an empty byte[] lets the COM interop layer allocate a new one.
                let mutable bytesHolder: obj = box (Array.zeroCreate<byte> 0)
                // JVGets signature: buff() As Byte (ByRef), size As Long, fname As String (ByRef)
                // Pass empty string for fname - COM will write back the filename
                let args: obj[] = [| bytesHolder; bufferSize; "" |]
                // Use invokeWithByRef with indices [0; 2] to properly handle ByRef parameters
                let code = invokeWithByRef "JVGets" args [ 0; 2 ]

                // For non-positive codes (EOF, FileBoundary, DownloadPending, errors), don't decode
                // COM may reuse arrays with residual data from previous calls
                if code <= 0 then
                    buffer <- ""
                else
                    // Extract resulting SAFEARRAY<Byte>
                    let extractedBytes =
                        match args.[0] with
                        | :? (byte[]) as direct -> direct
                        | :? Array as a when a.GetType().GetElementType() = typeof<byte> ->
                            let tmp = Array.zeroCreate<byte> a.Length

                            for i in 0 .. a.Length - 1 do
                                match a.GetValue i with
                                | :? byte as b -> tmp.[i] <- b
                                | _ -> ()

                            tmp
                        | _ -> Array.empty

                    // Trim to actual bytes indicated by return code to avoid residual garbage
                    let actualBytes =
                        if code <= extractedBytes.Length then
                            Array.sub extractedBytes 0 code
                        else
                            extractedBytes

                    // Decode bytes directly (Shift-JIS) instead of re-encoding from a string.
                    buffer <- Text.decodeShiftJis actualBytes

                // Extract filename from the ByRef parameter
                filename <-
                    match args.[2] with
                    | :? string as s -> s
                    | _ -> ""

                mapJvGetsCode code
            with ComActivationException fault ->
                Error(mapComFault "JVGets" fault)

        member _.Close() =
            try
                ignore (invokeCode "JVClose" [||])
            with _ ->
                ()

        member _.Status() =
            protect "JVStatus" (fun () ->
                let code = invokeCode "JVStatus" [||]

                if code >= 0 then
                    Ok code
                else
                    interpretCode "JVStatus" code |> Result.map (fun () -> 0))

        member _.Skip() =
            protect "JVSkip" (fun () -> ensureSuccess "JVSkip" (invokeCode "JVSkip" [||]))

        member _.Cancel() =
            protect "JVCancel" (fun () -> ensureSuccess "JVCancel" (invokeCode "JVCancel" [||]))

        member _.DeleteFile filename =
            protect "JVFiledelete" (fun () -> ensureSuccess "JVFiledelete" (invokeCode "JVFiledelete" [| filename |]))

        member _.WatchEvent(callback) =
            protect "JVWatchEvent" (fun () ->
                invokeOnSta "JVWatchEvent.Setup" (fun () ->
                    // Set up the event sink callback
                    eventSink.SetCallback(callback)
                    // Attempt to connect the event sink to COM connection points
                    match ComEventConnection.tryConnect comObj eventSink with
                    | Some subscription ->
                        // Connection succeeded - store subscription and invoke JVWatchEvent
                        eventSubscription <- Some subscription

                        match ensureSuccess "JVWatchEvent" (invokeCode "JVWatchEvent" [||]) with
                        | Ok() as success -> success
                        | Error _ as err ->
                            // JVWatchEvent failed - disconnect and clean up to prevent RCW leak
                            ComEventConnection.disconnect subscription
                            eventSubscription <- None
                            eventSink.ClearCallback()
                            err
                    | None ->
                        // COM event connection failed - events will not be delivered
                        // Return error so caller knows watch events won't work
                        eventSink.ClearCallback()

                        Error(
                            CommunicationFailure(
                                ErrorCodes.ComConnectionFailure,
                                "COM event connection failed; JV-Link events cannot be delivered. Verify the _IJVLinkEvents IID and DISPIDs match the type library."
                            )
                        )))

        member _.WatchEventClose() =
            protect "JVWatchEventClose" (fun () ->
                invokeOnSta "JVWatchEventClose" (fun () ->
                    // Disconnect event sink (instance-based cleanup)
                    match eventSubscription with
                    | Some sub ->
                        ComEventConnection.disconnect sub
                        eventSubscription <- None
                    | None -> ()

                    eventSink.ClearCallback()
                    ensureSuccess "JVWatchEventClose" (invokeCode "JVWatchEventClose" [||])))

        member _.SetUiProperties() =
            protect "JVSetUIProperties" (fun () ->
                ensureSuccess "JVSetUIProperties" (invokeCode "JVSetUIProperties" [||]))

        member _.SetSaveFlag enabled =
            protect "JVSetSaveFlag" (fun () ->
                ensureSuccess "JVSetSaveFlag" (invokeCode "JVSetSaveFlag" [| if enabled then 1 else 0 |]))

        member _.SetServiceKeyDirect key =
            protect "JVSetServiceKey" (fun () ->
                // JVSetServiceKey returns:
                //   0: Success - key registered
                //  -100/-101: "パラメータが不正あるいはレジストリへの保存に失敗既に利用キーが登録されている"
                //             This is an ERROR (invalid param, registry save failed, or already registered with no change)
                // Only 0 is success; all negative codes are errors per JV-Link spec.
                ensureSuccess "JVSetServiceKey" (invokeCode "JVSetServiceKey" [| key |]))

        member _.SetSavePathDirect path =
            protect "JVSetSavePath" (fun () -> ensureSuccess "JVSetSavePath" (invokeCode "JVSetSavePath" [| path |]))

        member _.SetParentWindowHandleDirect handle =
            setPropertyInt "ParentHWnd" (int handle)

        member _.SetPayoffDialogSuppressedDirect suppressed =
            setPropertyInt "m_payflag" (if suppressed then 1 else 0)

        member _.CourseFile key =
            protect "JVCourseFile" (fun () ->
                // JVCourseFile: key (in), filepath (out ByRef), explanation (out ByRef)
                let args: obj[] = [| key; ""; "" |]
                let code = invokeWithByRef "JVCourseFile" args [ 1; 2 ]

                match ensureSuccess "JVCourseFile" code with
                | Ok() ->
                    let filepath =
                        match args.[1] with
                        | :? string as s -> s
                        | _ -> ""

                    let explanation =
                        match args.[2] with
                        | :? string as s -> s
                        | _ -> ""

                    Ok(filepath, explanation)
                | Error e -> Error e)

        member _.CourseFile2(key, filepath) =
            protect "JVCourseFile2" (fun () ->
                // JVCourseFile2: key (in), filepath (in) - saves course diagram to specified path
                let code = invokeCode "JVCourseFile2" [| key; filepath |]
                ensureSuccess "JVCourseFile2" code)

        member _.SilksFile(pattern, outputPath) =
            protect "JVFukuFile" (fun () ->
                // JVFukuFile returns:
                //   0: Success - image file created
                //  -1: Success - No Image (正常終了、No Image画像を出力)
                //  <-1: Error codes
                let code = invokeCode "JVFukuFile" [| pattern; outputPath |]

                if code = 0 then
                    Diagnostics.emit $"OK JVFukuFile code={code} (image created)"
                    Ok(Some outputPath)
                elif code = -1 then
                    Diagnostics.emit $"OK JVFukuFile code={code} (No Image)"
                    Ok None
                else
                    match ensureSuccess "JVFukuFile" code with
                    | Ok() -> Ok None // Should not happen, but handle gracefully
                    | Error e -> Error e)

        member _.SilksBinary pattern =
            protect "JVFuku" (fun () ->
                // JVFuku returns:
                //   0: Success - image data returned in buffer
                //  -1: Success - No Image (No Image画像が出力された場合)
                //  <-1: Error codes
                // JVFuku: pattern (in), buff (out ByRef as Byte Array)
                // Per JV-Link spec: Long JVFuku ( String 型 pattern, Byte Array 型 buff )
                // Pass empty byte array placeholder - COM will allocate the actual array
                let mutable bytesHolder: obj = box (Array.zeroCreate<byte> 0)
                let args: obj[] = [| pattern; bytesHolder |]
                let code = invokeWithByRef "JVFuku" args [ 1 ]

                if code = 0 then
                    Diagnostics.emit $"OK JVFuku code={code}"

                    // Extract resulting SAFEARRAY<Byte> from ByRef parameter
                    let bytes =
                        match args.[1] with
                        | :? (byte[]) as direct -> direct
                        | :? Array as a when a.GetType().GetElementType() = typeof<byte> ->
                            let tmp = Array.zeroCreate<byte> a.Length

                            for i in 0 .. a.Length - 1 do
                                match a.GetValue i with
                                | :? byte as b -> tmp.[i] <- b
                                | _ -> ()

                            tmp
                        | _ -> Array.empty

                    Ok(Some bytes)
                elif code = -1 then
                    Diagnostics.emit $"OK JVFuku code={code} (No Image)"
                    Ok None
                else
                    match ensureSuccess "JVFuku" code with
                    | Ok() -> Ok None // Should not happen, but handle gracefully
                    | Error e -> Error e)

        member _.MovieCheck key =
            protect "JVMVCheck" (fun () ->
                let code = invokeCode "JVMVCheck" [| key |]

                if code = 1 then
                    Ok MovieAvailability.Available
                elif code = 0 then
                    Ok MovieAvailability.Unavailable
                elif code = -1 then
                    Ok MovieAvailability.NotFound
                elif code < -1 then
                    match ensureSuccess "JVMVCheck" code with
                    | Ok() -> Ok MovieAvailability.Unavailable
                    | Error e -> Error e
                else
                    Ok MovieAvailability.Unavailable)

        member _.MovieCheckWithType(movieType, key) =
            protect "JVMVCheckWithType" (fun () ->
                let code = invokeCode "JVMVCheckWithType" [| movieType; key |]

                if code = 1 then
                    Ok MovieAvailability.Available
                elif code = 0 then
                    Ok MovieAvailability.Unavailable
                elif code = -1 then
                    Ok MovieAvailability.NotFound
                elif code < -1 then
                    match ensureSuccess "JVMVCheckWithType" code with
                    | Ok() -> Ok MovieAvailability.Unavailable
                    | Error e -> Error e
                else
                    Ok MovieAvailability.Unavailable)

        member _.MoviePlay key =
            protect "JVMVPlay" (fun () -> ensureSuccess "JVMVPlay" (invokeCode "JVMVPlay" [| key |]))

        member _.MoviePlayWithType(movieType, key) =
            protect "JVMVPlayWithType" (fun () ->
                ensureSuccess "JVMVPlayWithType" (invokeCode "JVMVPlayWithType" [| movieType; key |]))

        member _.MovieOpen(movietype, searchKey) =
            protect "JVMVOpen" (fun () -> ensureSuccess "JVMVOpen" (invokeCode "JVMVOpen" [| movietype; searchKey |]))

        member _.MovieRead() =
            protect "JVMVRead" (fun () ->
                // JVMVRead: buff (out ByRef), size (in)
                let size = 4096
                let args: obj[] = [| ""; size |]
                let code = invokeWithByRef "JVMVRead" args [ 0 ]

                if code = 0 then
                    Ok MovieEnd
                elif code < 0 then
                    match interpretCode "JVMVRead" code with
                    | Ok() -> Ok MovieEnd
                    | Error e -> Error e
                else
                    let line =
                        match args.[0] with
                        | :? string as s -> s
                        | _ -> ""

                    let workoutDate, regId =
                        if String.IsNullOrWhiteSpace line || line.Length < 18 then
                            None, None
                        else
                            let dateStr = line.Substring(0, 8)
                            let idStr = line.Substring(8).Trim()

                            let dt =
                                match
                                    DateTime.TryParseExact(
                                        dateStr,
                                        "yyyyMMdd",
                                        Globalization.CultureInfo.InvariantCulture,
                                        Globalization.DateTimeStyles.None
                                    )
                                with
                                | true, d -> Some d
                                | _ -> None

                            dt, if idStr = "" then None else Some idStr

                    Ok(
                        MovieRecord
                            { RawKey = line
                              WorkoutDate = workoutDate
                              RegistrationId = regId }
                    ))

        member _.SaveFlag
            with get () = getPropertyInt "m_saveflag" <> 0
            and set value = setPropertyInt "m_saveflag" (if value then 1 else 0) |> ignore

        member _.SavePath = getPropertyString "m_savepath"

        member _.ServiceKey
            with get () = getPropertyString "m_servicekey"
            and set value = setPropertyString "m_servicekey" value |> ignore

        member _.TryGetSaveFlag() =
            tryGetPropertyInt "m_saveflag" |> Result.map (fun v -> v <> 0)

        member _.TryGetSavePath() = tryGetPropertyString "m_savepath"
        member _.TryGetServiceKey() = tryGetPropertyString "m_servicekey"
        member _.TryGetJVLinkVersion() = tryGetPropertyString "m_JVLinkVersion"

        member _.TryGetTotalReadFileSize() =
            tryGetPropertyInt64 "m_TotalReadFilesize"

        member _.TryGetCurrentReadFileSize() =
            tryGetPropertyInt64 "m_CurrentReadFilesize"

        member _.TryGetCurrentFileTimestamp() =
            tryGetPropertyString "m_CurrentFileTimestamp"
            |> Result.map (fun timestamp ->
                if String.IsNullOrWhiteSpace timestamp || timestamp = "00000000000000" then
                    None
                else
                    match
                        DateTime.TryParseExact(
                            timestamp,
                            "yyyyMMddHHmmss",
                            Globalization.CultureInfo.InvariantCulture,
                            Globalization.DateTimeStyles.None
                        )
                    with
                    | true, dt -> Some dt
                    | false, _ -> None)

        member _.TryGetParentWindowHandle() = tryGetPropertyIntPtr "ParentHWnd"
        member _.TryGetPayoffDialogSuppressed() = tryGetPropertyBool "m_payflag"

        member _.JVLinkVersion = getPropertyString "m_JVLinkVersion"
        member _.TotalReadFileSize = getPropertyInt64 "m_TotalReadFilesize"
        member _.CurrentReadFileSize = getPropertyInt64 "m_CurrentReadFilesize"

        member _.CurrentFileTimestamp =
            let timestamp = getPropertyString "m_CurrentFileTimestamp"

            if String.IsNullOrWhiteSpace timestamp || timestamp = "00000000000000" then
                None
            else
                match
                    DateTime.TryParseExact(
                        timestamp,
                        "yyyyMMddHHmmss",
                        Globalization.CultureInfo.InvariantCulture,
                        Globalization.DateTimeStyles.None
                    )
                with
                | true, dt -> Some dt
                | false, _ -> None

        member _.ParentWindowHandle
            with get () = IntPtr(getPropertyInt "ParentHWnd")
            and set value = setPropertyInt "ParentHWnd" (int value) |> ignore

        member _.PayoffDialogSuppressed
            with get () = getPropertyInt "m_payflag" <> 0
            and set value = setPropertyInt "m_payflag" (if value then 1 else 0) |> ignore

    /// <summary>
    /// Releases COM resources and disconnects event subscriptions.
    /// Call this method when the client is no longer needed to prevent RCW leaks.
    /// </summary>
    member private this.Dispose(disposing: bool) =
        if not disposed then
            let releaseResources () =
                try
                    dispatcher.Invoke(
                        "JVLink.Dispose",
                        fun () ->
                            // Disconnect event subscription if active
                            match eventSubscription with
                            | Some sub ->
                                try
                                    ComEventConnection.disconnect sub
                                with _ ->
                                    ()

                                eventSubscription <- None
                            | None -> ()

                            // Clear event callback
                            eventSink.ClearCallback()

                            // Call JVClose to end any active session
                            try
                                ignore (invokeCode "JVClose" [||])
                            with _ ->
                                ()

                            // Release COM object on the STA thread
                            if not (isNull comObj) then
                                try
                                    Marshal.FinalReleaseComObject(comObj) |> ignore
                                    Diagnostics.emit $"COM object released for ProgID '{progId}'."
                                with ex ->
                                    Diagnostics.emit $"Failed to release COM object: {ex.Message}"
                    )
                    |> ignore
                with ex ->
                    // Swallow errors during shutdown to avoid crashing finalizer paths.
                    Diagnostics.emit $"STA dispatcher disposal failed: {ex.GetType().Name} {ex.Message}"

            releaseResources ()
            (dispatcher :> IDisposable).Dispose()
            disposed <- true

    interface IDisposable with
        member this.Dispose() =
            this.Dispose(true)
            GC.SuppressFinalize(this)

    interface IComDispatchProvider with
        member _.Dispatcher = dispatcher

    override this.Finalize() = this.Dispose(false)
#else
module ComJvLinkClient =
    let notAvailable () =
        failwith "COM interop unavailable on this platform."
#endif

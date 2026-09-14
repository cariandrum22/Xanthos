namespace Xanthos

open System
open System.Globalization
open Xanthos.Interop

/// Synchronous, effectful SDK functions. Each session owns one COM instance and STA.
module internal SdkOperations =
    let private failure api kind code message =
        Error
            { Api = api
              Code = code
              Kind = kind
              Outputs = Map.empty
              Message = message }

    let private sdkError api code =
        // Preserve -413 like other SDK errors; its underlying native error may already be lost.
        // Do not infer HTTP status or retry here. See docs/sdk-known-limitations.md (SDK-COM-413).
        failure api JvErrorKind.Sdk (Some code) $"{api} returned SDK code {code}."

    let private mismatch api expected =
        failure api JvErrorKind.Invocation None $"SDK returned an unexpected value; expected {expected}."

    let private code api args indices (native: INativeJvLink) =
        native.Invoke(api, args, indices)
        |> Result.bind (function
            | :? int as value -> Ok value
            | _ -> mismatch api "32-bit SDK code")

    let private unitCall api args (session: Session) =
        session.Run(
            api,
            fun native ->
                code api args [] native
                |> Result.bind (fun value -> if value = 0 then Ok() else sdkError api value)
        )

    let private voidCall api (session: Session) =
        session.Run(
            api,
            fun native ->
                native.Invoke(api, [||], [])
                |> Result.bind (fun value -> if isNull value then Ok() else mismatch api "void")
        )

    let init (sid: string) session = unitCall "JVInit" [| box sid |] session

    let configureUi session =
        unitCall "JVSetUIProperties" [||] session

    let setServiceKey (key: string) session =
        unitCall "JVSetServiceKey" [| box key |] session

    let setSaveFlag enabled session =
        unitCall "JVSetSaveFlag" [| box (if enabled then 1 else 0) |] session

    let setSavePath (path: string) session =
        unitCall "JVSetSavePath" [| box path |] session

    let closeData session = unitCall "JVClose" [||] session
    let skip session = voidCall "JVSkip" session
    let cancel session = voidCall "JVCancel" session

    let deleteFile (filename: string) session =
        unitCall "JVFiledelete" [| box filename |] session

    let openData (request: OpenRequest) (session: Session) =
        let timestamp (value: DateTime) =
            value.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)

        if request.ToTime |> Option.exists (fun ending -> ending < request.FromTime) then
            failure "JVOpen" JvErrorKind.InvalidInput None "End time precedes start time."
        else
            let fromtime =
                timestamp request.FromTime
                + (request.ToTime
                   |> Option.map (fun ending -> "-" + timestamp ending)
                   |> Option.defaultValue "")

            session.Run(
                "JVOpen",
                fun native ->
                    let args =
                        [| box request.Dataspec
                           box fromtime
                           box request.Option
                           box 0
                           box 0
                           box "" |]

                    code "JVOpen" args [ 3; 4; 5 ] native
                    |> Result.bind (fun value ->
                        if value = 0 || value = -1 then
                            match args.[3], args.[4], args.[5] with
                            | (:? int as read), (:? int as downloaded), timestamp when
                                isNull timestamp || (timestamp :? string)
                                ->
                                let metadata =
                                    { ReadCount = read
                                      DownloadCount = downloaded
                                      LastFileTimestamp = if isNull timestamp then "" else unbox timestamp }

                                Ok(
                                    if value = 0 then
                                        OpenOutcome.Opened metadata
                                    else
                                        OpenOutcome.NoData metadata
                                )
                            | _ -> mismatch "JVOpen" "readcount, downloadcount, lastfiletimestamp"
                        else
                            sdkError "JVOpen" value)
            )

    let openRealtime (dataspec: string) (key: string) (session: Session) =
        session.Run(
            "JVRTOpen",
            fun native ->
                code "JVRTOpen" [| box dataspec; box key |] [] native
                |> Result.bind (fun value ->
                    match value with
                    | 0 -> Ok RealtimeOpenOutcome.Opened
                    | -1 -> Ok RealtimeOpenOutcome.NoData
                    | _ -> sdkError "JVRTOpen" value)
        )

    let status (session: Session) =
        session.Run(
            "JVStatus",
            fun native ->
                code "JVStatus" [||] [] native
                |> Result.bind (fun value -> if value >= 0 then Ok value else sdkError "JVStatus" value)
        )

    let private readUsing api capacity (session: Session) =
        if capacity <= 0 then
            failure api JvErrorKind.InvalidInput None "Read capacity must be positive."
        else
            session.Run(
                api,
                fun native ->
                    let buffer =
                        if api = "JVGets" then
                            box (Array.zeroCreate<byte> capacity)
                        else
                            box (String(' ', capacity))

                    let args = [| buffer; box capacity; box "" |]
                    let indices = if api = "JVGets" then [ 0; 2 ] else [ 0; 1; 2 ]

                    code api args indices native
                    |> Result.bind (fun value ->
                        if value < 0 && value <> -1 && value <> -3 then
                            sdkError api value
                            |> Result.mapError (fun error ->
                                { error with
                                    Outputs =
                                        [ match args.[2] with
                                          | :? string as filename -> yield "filename", filename
                                          | _ -> ()
                                          match args.[1] with
                                          | :? int as size ->
                                              yield "size", size.ToString(CultureInfo.InvariantCulture)
                                          | _ -> () ]
                                        |> Map.ofList })
                        else
                            match args.[1], args.[2] with
                            | (:? int as size), filename when isNull filename || (filename :? string) ->
                                let data =
                                    match args.[0] with
                                    | null when value <= 0 -> Some [||]
                                    | :? (byte[]) as bytes -> Some bytes
                                    | :? string as text -> Some(ComInterop.readBstrBytes text)
                                    | _ -> None

                                match data with
                                | Some bytes when value <= bytes.Length ->
                                    let state =
                                        match value with
                                        | 0 -> ReadState.EndOfStream
                                        | -1 -> ReadState.FileBoundary
                                        | -3 -> ReadState.DownloadPending
                                        | _ -> ReadState.Record

                                    Ok
                                        { State = state
                                          Data = (if value > 0 then bytes.[0 .. value - 1] else [||])
                                          RawText =
                                            (match args.[0] with
                                             | :? string as text -> Some text
                                             | _ -> None)
                                          Filename = if isNull filename then "" else unbox filename
                                          ByteCount = max value 0
                                          BufferSize = size
                                          ReturnCode = value }
                                | _ -> mismatch api "a buffer containing the reported byte count"
                            | _ -> mismatch api "buffer size and filename")
            )

    let readWithCapacity capacity session = readUsing "JVRead" capacity session
    let getsWithCapacity capacity session = readUsing "JVGets" capacity session
    let read session = readWithCapacity 131072 session
    let gets session = getsWithCapacity 131072 session
    let decodeShiftJis bytes = Core.Text.decodeShiftJis bytes

    let private image api args indices extract (session: Session) =
        session.Run(
            api,
            fun native ->
                code api args indices native
                |> Result.bind (fun value ->
                    if value = 0 || value = -1 then
                        extract args
                        |> Result.map (fun output ->
                            { State =
                                (if value = 0 then
                                     ImageState.Available
                                 else
                                     ImageState.NoImage)
                              Value = output
                              ReturnCode = value })
                    else
                        sdkError api value)
        )

    let silksFile (pattern: string) (filepath: string) session =
        image "JVFukuFile" [| box pattern; box filepath |] [] (fun _ -> Ok filepath) session

    let silksBinary (pattern: string) session =
        image
            "JVFuku"
            [| box pattern; box (Array.zeroCreate<byte> 0) |]
            [ 1 ]
            (fun args ->
                match args.[1] with
                | :? (byte[]) as bytes -> Ok(Array.copy bytes)
                | _ -> mismatch "JVFuku" "SAFEARRAY<byte>")
            session

    let courseFile (key: string) session =
        image
            "JVCourseFile"
            [| box key; box ""; box "" |]
            [ 1; 2 ]
            (fun args ->
                match args.[1], args.[2] with
                | (:? string as path), (:? string as explanation) ->
                    Ok
                        { Filepath = path
                          Explanation = explanation }
                | _ -> mismatch "JVCourseFile" "filepath and explanation")
            session

    let courseFile2 (key: string) (filepath: string) session =
        image "JVCourseFile2" [| box key; box filepath |] [] (fun _ -> Ok filepath) session

    let private movieCheckUsing api args (session: Session) =
        session.Run(
            api,
            fun native ->
                code api args [] native
                |> Result.bind (function
                    | 1 -> Ok VideoAvailability.Available
                    | 0 -> Ok VideoAvailability.Unpublished
                    | -1 -> Ok VideoAvailability.Missing
                    | value when value > 1 -> Ok(VideoAvailability.Unknown value)
                    | value -> sdkError api value)
        )

    let movieCheck (key: string) session =
        movieCheckUsing "JVMVCheck" [| box key |] session

    let movieCheckWithType (movieType: string) (key: string) session =
        movieCheckUsing "JVMVCheckWithType" [| box movieType; box key |] session

    let moviePlay (key: string) session =
        unitCall "JVMVPlay" [| box key |] session

    let moviePlayWithType (movieType: string) (key: string) session =
        unitCall "JVMVPlayWithType" [| box movieType; box key |] session

    let movieOpen (movieType: string) (searchKey: string) (session: Session) =
        session.Run(
            "JVMVOpen",
            fun native ->
                code "JVMVOpen" [| box movieType; box searchKey |] [] native
                |> Result.bind (function
                    | 0 -> Ok VideoOpenOutcome.Opened
                    | -1 -> Ok VideoOpenOutcome.NoData
                    | value -> sdkError "JVMVOpen" value)
        )

    let movieReadWithCapacity capacity (session: Session) =
        if capacity <= 0 then
            failure "JVMVRead" JvErrorKind.InvalidInput None "Read capacity must be positive."
        else
            session.Run(
                "JVMVRead",
                fun native ->
                    let args = [| box (String(' ', capacity)); box capacity |]

                    code "JVMVRead" args [ 0; 1 ] native
                    |> Result.bind (fun value ->
                        if value < 0 && value <> -3 then
                            sdkError "JVMVRead" value
                        else
                            match args.[0], args.[1] with
                            | text, (:? int as size) when (text :? string) || (isNull text && value <= 0) ->
                                let text = if isNull text then "" else unbox<string> text

                                Ok(
                                    if value = -3 then
                                        VideoReadOutcome.DownloadPending(text, size)
                                    elif value = 0 then
                                        VideoReadOutcome.EndOfStream(text, size)
                                    else
                                        VideoReadOutcome.Record(text, value, size)
                                )
                            | _ -> mismatch "JVMVRead" "buffer and size")
            )

    let movieRead session = movieReadWithCapacity 4096 session

    let private property<'a> name (session: Session) =
        session.Run(
            name,
            fun native ->
                native.Get name
                |> Result.bind (function
                    | :? 'a as value -> Ok value
                    | _ -> mismatch name typeof<'a>.Name)
        )

    let getSaveFlag session = property<int> "m_saveflag" session
    let getSavePath session = property<string> "m_savepath" session
    let getServiceKey session = property<string> "m_servicekey" session

    let getVersion session =
        property<string> "m_JVLinkVersion" session

    let getTotalReadFileSize session =
        property<int> "m_TotalReadFilesize" session
        |> Result.map (fun value ->
            { RawKilobytes = value
              Bytes = int64 value * 1024L })

    let getCurrentReadFileSize session =
        property<int> "m_CurrentReadFilesize" session

    let getCurrentFileTimestamp (session: Session) =
        session.Run(
            "m_CurrentFileTimestamp",
            fun native ->
                native.Get "m_CurrentFileTimestamp"
                |> Result.bind (function
                    // The native BSTR is null before the first JVRead/JVGets.
                    | null -> Ok ""
                    | :? string as value -> Ok value
                    | _ -> mismatch "m_CurrentFileTimestamp" "String")
        )

    let getPayFlag session = property<int> "m_payflag" session

    let setParentWindowHandle (handle: nativeint) (session: Session) =
        let value = handle.ToInt64()

        if value < int64 Int32.MinValue || value > int64 Int32.MaxValue then
            failure "ParentHWnd" JvErrorKind.InvalidInput None "ParentHWnd is a signed 32-bit COM Long."
        else
            session.Run("ParentHWnd", fun native -> native.Put("ParentHWnd", box (int value)))

    let watchEvent callback (session: Session) =
        session.BeginWatch(SubscriptionOptions.Default.Capacity, callback)
        |> Result.map ignore

    let watchEventClose (session: Session) = session.EndWatch None

    let watchError (session: Session) =
        session.Run("watchError", fun _ -> Ok session.WatchError)

    let subscribeWithOptions options callback (session: Session) =
        if options.Capacity <= 0 || options.CancellationToken.IsCancellationRequested then
            failure
                "JVWatchEvent"
                JvErrorKind.InvalidInput
                None
                "Subscription capacity must be positive and its cancellation token must not already be canceled."
        else
            session.BeginWatch(options.Capacity, callback)
            |> Result.map (fun (generation, delivery) ->
                let stop () =
                    match session.EndWatch(Some generation) with
                    | Error error when error.Kind = JvErrorKind.Disposed ->
                        delivery.Stop()
                        Ok()
                    | result -> result

                let subscription = new Subscription(stop, fun () -> delivery.Error)

                let registration =
                    options.CancellationToken.Register(fun () ->
                        Threading.ThreadPool.QueueUserWorkItem(fun _ ->
                            // Stop stores a failed attempt in subscriptionError; a Busy
                            // attempt must not terminate a still-registered delivery worker.
                            subscription.Stop() |> ignore)
                        |> ignore)

                subscription.SetCancellation registration
                subscription)

    let subscribe callback session =
        subscribeWithOptions SubscriptionOptions.Default callback session

    let unsubscribe (subscription: Subscription) = subscription.Stop()
    let subscriptionError (subscription: Subscription) = subscription.Error

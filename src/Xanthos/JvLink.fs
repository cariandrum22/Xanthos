namespace Xanthos

open System
open System.Globalization
open Xanthos.Interop

/// Synchronous, effectful SDK functions. Each session owns one COM instance and STA.
module JvLink =
    let private failure api kind code message =
        Error
            { Api = api
              Code = code
              Kind = kind
              Outputs = Map.empty
              Message = message }

    let private sdkError api code =
        // Keep the original API/code, including -413; do not infer retryability from the SDK label.
        // See docs/sdk-known-limitations.md (SDK-COM-413).
        failure api JvErrorKind.Sdk (Some code) $"{api} returned SDK code {code}."

    let private mismatch api expected =
        failure api JvErrorKind.Invocation None $"SDK returned an unexpected value; expected {expected}."

    let connect (options: ConnectionOptions) : Result<Session, JvError> =
#if WINDOWS
        try
            let native = new ComJvLinkClient(progId = options.ProgId) :> INativeJvLink
            Ok(new Session(native))
        with ex ->
            failure "connect" JvErrorKind.Invocation (Some ex.HResult) ex.Message
#else
        failure
            "connect"
            JvErrorKind.Invocation
            None
            "Use the net10.0-windows target with a JV-Link installation matching the process architecture."
#endif

    let disconnect (session: Session) = session.Disconnect()

    /// Releases the session on success, Error, or consumer exception.
    let withSession options action =
        connect options |> Result.bind (fun session -> session.WithScope action)

    let cancel arg1 = SdkOperations.cancel arg1
    let closeData arg1 = SdkOperations.closeData arg1
    let configureUi arg1 = SdkOperations.configureUi arg1
    let courseFile arg1 arg2 = SdkOperations.courseFile arg1 arg2

    let courseFile2 arg1 arg2 arg3 =
        SdkOperations.courseFile2 arg1 arg2 arg3

    let decodeShiftJis arg1 = SdkOperations.decodeShiftJis arg1
    let deleteFile arg1 arg2 = SdkOperations.deleteFile arg1 arg2

    let getCurrentFileTimestamp arg1 =
        SdkOperations.getCurrentFileTimestamp arg1

    let getCurrentReadFileSize arg1 =
        SdkOperations.getCurrentReadFileSize arg1

    let getPayFlag arg1 = SdkOperations.getPayFlag arg1
    let gets arg1 = SdkOperations.gets arg1
    let getSaveFlag arg1 = SdkOperations.getSaveFlag arg1
    let getSavePath arg1 = SdkOperations.getSavePath arg1
    let getServiceKey arg1 = SdkOperations.getServiceKey arg1

    let getsWithCapacity arg1 arg2 =
        SdkOperations.getsWithCapacity arg1 arg2

    let getTotalReadFileSize arg1 = SdkOperations.getTotalReadFileSize arg1
    let getVersion arg1 = SdkOperations.getVersion arg1
    let init arg1 arg2 = SdkOperations.init arg1 arg2
    let movieCheck arg1 arg2 = SdkOperations.movieCheck arg1 arg2

    let movieCheckWithType arg1 arg2 arg3 =
        SdkOperations.movieCheckWithType arg1 arg2 arg3

    let movieOpen arg1 arg2 arg3 = SdkOperations.movieOpen arg1 arg2 arg3
    let moviePlay arg1 arg2 = SdkOperations.moviePlay arg1 arg2

    let moviePlayWithType arg1 arg2 arg3 =
        SdkOperations.moviePlayWithType arg1 arg2 arg3

    let movieRead arg1 = SdkOperations.movieRead arg1

    let movieReadWithCapacity arg1 arg2 =
        SdkOperations.movieReadWithCapacity arg1 arg2

    let openData arg1 arg2 = SdkOperations.openData arg1 arg2

    let openRealtime arg1 arg2 arg3 =
        SdkOperations.openRealtime arg1 arg2 arg3

    let read arg1 = SdkOperations.read arg1

    let readWithCapacity arg1 arg2 =
        SdkOperations.readWithCapacity arg1 arg2

    let setParentWindowHandle arg1 arg2 =
        SdkOperations.setParentWindowHandle arg1 arg2

    let setSaveFlag arg1 arg2 = SdkOperations.setSaveFlag arg1 arg2
    let setSavePath arg1 arg2 = SdkOperations.setSavePath arg1 arg2
    let setServiceKey arg1 arg2 = SdkOperations.setServiceKey arg1 arg2
    let silksBinary arg1 arg2 = SdkOperations.silksBinary arg1 arg2
    let silksFile arg1 arg2 arg3 = SdkOperations.silksFile arg1 arg2 arg3
    let skip arg1 = SdkOperations.skip arg1
    let status arg1 = SdkOperations.status arg1
    let subscribe arg1 arg2 = SdkOperations.subscribe arg1 arg2
    let watchEvent arg1 arg2 = SdkOperations.watchEvent arg1 arg2
    let watchEventClose arg1 = SdkOperations.watchEventClose arg1

    let subscribeWithOptions options callback session =
        SdkOperations.subscribeWithOptions options callback session

    let unsubscribe subscription = SdkOperations.unsubscribe subscription

    let subscriptionError subscription =
        SdkOperations.subscriptionError subscription

    let watchError session = SdkOperations.watchError session
    let parseEvent event = EventKeys.parse event

    let toRealtimeRequest event =
        EventKeys.parse event |> Result.map _.Request

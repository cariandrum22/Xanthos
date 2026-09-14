namespace Xanthos.Interop

open System
open System.Runtime.InteropServices

/// COM event sink for JV-Link events.
/// Receives callbacks from the JVDTLabLib.JVLink COM component.
#if WINDOWS
[<ComVisible(true)>]
[<ClassInterface(ClassInterfaceType.None)>]
type JvLinkEventSink() =
    let mutable callback: (string -> unit) option = None
    let mutable nativeCallback: (Xanthos.JvEvent -> unit) option = None

    let deliver kind key =
        nativeCallback |> Option.iter (fun cb -> cb { Kind = kind; RawKey = key })
        callback |> Option.iter (fun cb -> cb key)

    member internal _.SetNativeCallback(cb) = nativeCallback <- Some cb

    /// Sets the callback function invoked when any event fires.
    member _.SetCallback(cb: string -> unit) = callback <- Some cb

    /// Clears the callback.
    member _.ClearCallback() =
        callback <- None
        nativeCallback <- None

    // COM event methods called by JVDTLabLib.JVLink
    // Preserve both origin and raw key. Pay/Weight keys do not contain a dataspec prefix.

    /// Payoff confirmation event (払戻確定)
    member _.JVEvtPay(bstr: string) = deliver Xanthos.EventKind.Pay bstr

    /// Horse weight announcement event (馬体重発表)
    member _.JVEvtWeight(bstr: string) = deliver Xanthos.EventKind.Weight bstr

    /// Jockey change event (騎手変更)
    member _.JVEvtJockeyChange(bstr: string) =
        deliver Xanthos.EventKind.JockeyChange bstr

    /// Weather/track condition change event (天候馬場状態変更)
    member _.JVEvtWeather(bstr: string) = deliver Xanthos.EventKind.Weather bstr

    /// Course change event (コース変更)
    member _.JVEvtCourseChange(bstr: string) =
        deliver Xanthos.EventKind.CourseChange bstr

    /// Race withdrawal/exclusion event (出走取消・競走除外)
    member _.JVEvtAvoid(bstr: string) = deliver Xanthos.EventKind.Avoid bstr

    /// Start time change event (発走時刻変更)
    member _.JVEvtTimeChange(bstr: string) =
        deliver Xanthos.EventKind.TimeChange bstr

/// Tracks active event subscription for cleanup.
/// Instance-based to support multiple ComJvLinkClient instances.
type EventSubscription =
    { ComObject: obj
      SourceIID: Guid
      mutable Dispids: int list
      mutable Delegates: Delegate list }

/// Manages COM event connection points. IID and DISPIDs were checked against the
/// installed JV-Link 5.0 x64 type library on 2026-09-12. Successful registration
/// verifies connection setup; delivery of each notification needs a live event.
module ComEventConnection =
    open System.Runtime.InteropServices.ComTypes

    /// JVDTLabLib._IJVLinkEvents source interface IID.
    /// Verified against the installed JVDTLabLib type library.
    let private JVLinkEventsIID = Guid("17E1E656-828B-4849-B043-FA62B92D9E41")

    /// DISPIDs for JV-Link events as defined in the type library.
    /// Verified against the installed JVDTLabLib type library.
    let private DispidPay = 1 // JVEvtPay
    let private DispidJockeyChange = 2 // JVEvtJockeyChange
    let private DispidWeather = 3 // JVEvtWeather
    let private DispidCourseChange = 4 // JVEvtCourseChange
    let private DispidAvoid = 5 // JVEvtAvoid
    let private DispidTimeChange = 6 // JVEvtTimeChange
    let private DispidWeight = 7 // JVEvtWeight

    /// Attempts to connect an event sink to a COM object's connection point.
    /// Returns the EventSubscription for cleanup, or the original registration/rollback error.
    /// The subscription should be stored by the caller for later cleanup via disconnect.
    let tryConnect (comObject: obj) (sink: JvLinkEventSink) : Result<EventSubscription, Xanthos.JvError> =
        let registrations =
            [ DispidPay, Action<string>(sink.JVEvtPay) :> Delegate
              DispidJockeyChange, Action<string>(sink.JVEvtJockeyChange) :> Delegate
              DispidWeather, Action<string>(sink.JVEvtWeather) :> Delegate
              DispidCourseChange, Action<string>(sink.JVEvtCourseChange) :> Delegate
              DispidAvoid, Action<string>(sink.JVEvtAvoid) :> Delegate
              DispidTimeChange, Action<string>(sink.JVEvtTimeChange) :> Delegate
              DispidWeight, Action<string>(sink.JVEvtWeight) :> Delegate ]

        let register id handler =
            ComEventsHelper.Combine(comObject, JVLinkEventsIID, id, handler)

        let unregister id handler =
            ComEventsHelper.Remove(comObject, JVLinkEventsIID, id, handler) |> ignore

        Xanthos.EventRegistration.attach register unregister registrations
        |> Result.map (fun () ->
            Diagnostics.emit "COM event sink connected via ComEventsHelper"

            { ComObject = comObject
              SourceIID = JVLinkEventsIID
              Dispids = List.map fst registrations
              Delegates = List.map snd registrations })

    /// Attempt every removal and report failures instead of silently losing them.
    let internal disconnectWith unregister (subscription: EventSubscription) =
        let remaining, result =
            Xanthos.EventRegistration.detachRemaining unregister (List.zip subscription.Dispids subscription.Delegates)

        subscription.Dispids <- List.map fst remaining
        subscription.Delegates <- List.map snd remaining

        match result with
        | Ok() -> Diagnostics.emit "COM event sink disconnected"
        | Error error -> raise (Xanthos.SessionCleanupException error)

    let disconnect (subscription: EventSubscription) =
        disconnectWith
            (fun id handler ->
                ComEventsHelper.Remove(subscription.ComObject, subscription.SourceIID, id, handler)
                |> ignore)
            subscription

/// Testable boundary around event connection points; default uses the real COM helpers.
type internal ComEventConnector =
    { Connect: obj -> JvLinkEventSink -> Result<EventSubscription, Xanthos.JvError>
      Disconnect: EventSubscription -> unit }

    static member Default =
        { Connect = ComEventConnection.tryConnect
          Disconnect = ComEventConnection.disconnect }

#endif

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

    /// Sets the callback function invoked when any event fires.
    member _.SetCallback(cb: string -> unit) = callback <- Some cb

    /// Clears the callback.
    member _.ClearCallback() = callback <- None

    // COM event methods called by JVDTLabLib.JVLink
    // Each event receives a raw key string (e.g., "0B1220240101010101") that can be
    // passed directly to JVRTOpen. The raw key already includes the dataspec prefix.

    /// Payoff confirmation event (払戻確定)
    member _.JVEvtPay(bstr: string) =
        callback |> Option.iter (fun cb -> cb bstr)

    /// Horse weight announcement event (馬体重発表)
    member _.JVEvtWeight(bstr: string) =
        callback |> Option.iter (fun cb -> cb bstr)

    /// Jockey change event (騎手変更)
    member _.JVEvtJockeyChange(bstr: string) =
        callback |> Option.iter (fun cb -> cb bstr)

    /// Weather/track condition change event (天候馬場状態変更)
    member _.JVEvtWeather(bstr: string) =
        callback |> Option.iter (fun cb -> cb bstr)

    /// Course change event (コース変更)
    member _.JVEvtCourseChange(bstr: string) =
        callback |> Option.iter (fun cb -> cb bstr)

    /// Race withdrawal/exclusion event (出走取消・競走除外)
    member _.JVEvtAvoid(bstr: string) =
        callback |> Option.iter (fun cb -> cb bstr)

    /// Start time change event (発走時刻変更)
    member _.JVEvtTimeChange(bstr: string) =
        callback |> Option.iter (fun cb -> cb bstr)

/// Tracks active event subscription for cleanup.
/// Instance-based to support multiple ComJvLinkClient instances.
type EventSubscription =
    { ComObject: obj
      SourceIID: Guid
      Dispids: int list
      Delegates: Delegate list }

/// Manages COM event connection point for JV-Link.
///
/// ## Verified IID/DISPID Values
///
/// The IID and DISPIDs below were extracted from JVDTLabLib type library using
/// OleView on Windows with JV-Link installed. These are production values.
///
/// Last verified: 2025 (JV-Link version 4.x)
///
/// ## Re-verification Instructions
///
/// If JV-Link is updated and events stop working, re-verify the values:
///
/// ### Method 1: OleView (recommended)
/// 1. Open OleView.exe (Windows SDK)
/// 2. Navigate to: Type Libraries → JVDTLabLib (JV-Link Type Library)
/// 3. Find the `_IJVLinkEvents` dispinterface
/// 4. The IID is shown as `[uuid(xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx)]`
/// 5. Each method shows `[id(N)]` where N is the DISPID
///
/// ### Method 2: PowerShell
/// ```powershell
/// # Get the type library info
/// $jvlink = New-Object -ComObject JVDTLabLib.JVLink
/// $jvlink.PSObject.TypeNames  # Shows available interfaces
/// ```
///
/// ### Method 3: tlbimp + ildasm
/// ```cmd
/// tlbimp JVDTLab.dll /out:JVDTLabInterop.dll
/// ildasm JVDTLabInterop.dll
/// ```
/// Look for `_IJVLinkEvents` interface and its GUID.
///
/// ## Runtime Verification
///
/// To verify the values are correct:
/// ```fsharp
/// match ComClientFactory.tryCreate None with
/// | Ok client ->
///     let service = JvLinkService(client, config)
///     match service.StartWatchEvents() with
///     | Ok () -> printfn "COM events connected - IID/DISPIDs verified"
///     | Error e -> printfn "Connection failed: %A" e
/// | Error e -> printfn "Client creation failed: %A" e
/// ```
///
/// If `StartWatchEvents` returns `Ok`, the IID and DISPIDs are correct.
/// If it fails with E_NOINTERFACE or similar, re-verify the IID.
/// If events don't fire, re-verify the DISPIDs.
module ComEventConnection =
    open System.Runtime.InteropServices.ComTypes

    /// JVDTLabLib._IJVLinkEvents source interface IID.
    /// Verified from JVDTLabLib type library via OleView on Windows.
    let private JVLinkEventsIID = Guid("17E1E656-828B-4849-B043-FA62B92D9E41")

    /// DISPIDs for JV-Link events as defined in the type library.
    /// Verified from JVDTLabLib type library via OleView on Windows.
    let private DispidPay = 1 // JVEvtPay
    let private DispidJockeyChange = 2 // JVEvtJockeyChange
    let private DispidWeather = 3 // JVEvtWeather
    let private DispidCourseChange = 4 // JVEvtCourseChange
    let private DispidAvoid = 5 // JVEvtAvoid
    let private DispidTimeChange = 6 // JVEvtTimeChange
    let private DispidWeight = 7 // JVEvtWeight

    /// Attempts to connect an event sink to a COM object's connection point.
    /// Returns the EventSubscription if successful for proper cleanup, or None if connection fails.
    /// The subscription should be stored by the caller for later cleanup via disconnect.
    let tryConnect (comObject: obj) (sink: JvLinkEventSink) : EventSubscription option =
        if isNull comObject then
            None
        else
            try
                // Create delegates for each event (order matches DISPIDs from type library)
                let payDelegate = Action<string>(sink.JVEvtPay) :> Delegate // DISPID 1
                let jockeyDelegate = Action<string>(sink.JVEvtJockeyChange) :> Delegate // DISPID 2
                let weatherDelegate = Action<string>(sink.JVEvtWeather) :> Delegate // DISPID 3
                let courseDelegate = Action<string>(sink.JVEvtCourseChange) :> Delegate // DISPID 4
                let avoidDelegate = Action<string>(sink.JVEvtAvoid) :> Delegate // DISPID 5
                let timeDelegate = Action<string>(sink.JVEvtTimeChange) :> Delegate // DISPID 6
                let weightDelegate = Action<string>(sink.JVEvtWeight) :> Delegate // DISPID 7

                let delegates =
                    [ payDelegate
                      jockeyDelegate
                      weatherDelegate
                      courseDelegate
                      avoidDelegate
                      timeDelegate
                      weightDelegate ]

                let dispids =
                    [ DispidPay
                      DispidJockeyChange
                      DispidWeather
                      DispidCourseChange
                      DispidAvoid
                      DispidTimeChange
                      DispidWeight ]

                // Use ComEventsHelper to register each event handler
                // ComEventsHelper.Combine connects to the COM object's source interface
                // Track registered (dispid, delegate) pairs for cleanup on partial failure
                let mutable registered: (int * Delegate) list = []

                let registerEvent dispid del =
                    ComEventsHelper.Combine(comObject, JVLinkEventsIID, dispid, del) |> ignore
                    registered <- (dispid, del) :: registered

                let cleanupRegistered () =
                    for (dispid, del) in registered do
                        try
                            ComEventsHelper.Remove(comObject, JVLinkEventsIID, dispid, del) |> ignore
                        with _ ->
                            ()

                try
                    registerEvent DispidPay payDelegate
                    registerEvent DispidJockeyChange jockeyDelegate
                    registerEvent DispidWeather weatherDelegate
                    registerEvent DispidCourseChange courseDelegate
                    registerEvent DispidAvoid avoidDelegate
                    registerEvent DispidTimeChange timeDelegate
                    registerEvent DispidWeight weightDelegate

                    let subscription =
                        { ComObject = comObject
                          SourceIID = JVLinkEventsIID
                          Dispids = dispids
                          Delegates = delegates }

                    Diagnostics.emit "COM event sink connected via ComEventsHelper"
                    Some subscription
                with ex ->
                    // Clean up any delegates that were successfully registered before the failure
                    cleanupRegistered ()
                    Diagnostics.emit $"ComEventsHelper.Combine failed: {ex.Message}; COM events will not be delivered"
                    None
            with ex ->
                Diagnostics.emit $"COM event connection setup failed: {ex.Message}"
                None

    /// Disconnects an event sink using the provided subscription.
    let disconnect (subscription: EventSubscription) =
        try
            // Remove each delegate using ComEventsHelper.Remove
            List.iter2
                (fun dispid del ->
                    try
                        ComEventsHelper.Remove(subscription.ComObject, subscription.SourceIID, dispid, del)
                        |> ignore
                    with _ ->
                        ())
                subscription.Dispids
                subscription.Delegates

            Diagnostics.emit "COM event sink disconnected"
        with ex ->
            Diagnostics.emit $"COM event disconnect failed: {ex.Message}"

#endif

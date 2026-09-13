namespace Xanthos.Functional

open System
open Xanthos

/// Compile-checked examples of every SDK method/property and all seven event origins.
/// Destructive, configuration and UI examples are opt-in functions, never startup actions.
module Examples =
    let request =
        { Dataspec = "RACE"
          FromTime = DateTime(2026, 9, 5)
          ToTime = Some(DateTime(2026, 9, 11, 23, 59, 59))
          Option = 1 }

    let openRequested = JvLink.openData request
    let openViaPipeline session = session |> openRequested

    let fetchFirst session =
        JvLink.init "UNKNOWN" session
        |> Result.bind (fun () -> openViaPipeline session)
        |> Result.bind (fun _ ->
            let result = JvLink.gets session

            match JvLink.closeData session with
            | Ok() -> result
            | Error error -> Error error)

    /// The owning helper also releases COM when initialization, open, or read fails.
    let fetchFirstWithConnection options = JvLink.withSession options fetchFirst

    let eventOrigin event =
        match event.Kind with
        | EventKind.Pay -> "JVEvtPay"
        | EventKind.JockeyChange -> "JVEvtJockeyChange"
        | EventKind.Weather -> "JVEvtWeather"
        | EventKind.CourseChange -> "JVEvtCourseChange"
        | EventKind.Avoid -> "JVEvtAvoid"
        | EventKind.TimeChange -> "JVEvtTimeChange"
        | EventKind.Weight -> "JVEvtWeight"
        | EventKind.Unknown origin -> origin

    /// Each entry has a concrete type through the public .fsi, without COM/byref/member calls.
    let apiExamples serviceKey outputPath session : (string * (unit -> Result<unit, JvError>)) list =
        let discard result = Result.map ignore result

        [ "JVInit", (fun () -> JvLink.init "UNKNOWN" session)
          "JVSetUIProperties", (fun () -> JvLink.configureUi session)
          "JVSetServiceKey", (fun () -> JvLink.setServiceKey serviceKey session)
          "JVSetSaveFlag", (fun () -> JvLink.setSaveFlag false session)
          "JVSetSavePath", (fun () -> JvLink.setSavePath outputPath session)
          "JVOpen", (fun () -> openViaPipeline session |> discard)
          "JVRTOpen", (fun () -> JvLink.openRealtime "0B12" "202609120511" session |> discard)
          "JVStatus", (fun () -> JvLink.status session |> discard)
          "JVRead", (fun () -> JvLink.read session |> discard)
          "JVGets", (fun () -> JvLink.gets session |> discard)
          "JVSkip", (fun () -> JvLink.skip session)
          "JVCancel", (fun () -> JvLink.cancel session)
          "JVClose", (fun () -> JvLink.closeData session)
          "JVFiledelete", (fun () -> JvLink.deleteFile "owned-test-file.jvd" session)
          "JVFukuFile", (fun () -> JvLink.silksFile "test-pattern" outputPath session |> discard)
          "JVFuku", (fun () -> JvLink.silksBinary "test-pattern" session |> discard)
          "JVMVCheck", (fun () -> JvLink.movieCheck "202609120511" session |> discard)
          "JVMVCheckWithType", (fun () -> JvLink.movieCheckWithType "00" "202609120511" session |> discard)
          "JVMVPlay", (fun () -> JvLink.moviePlay "202609120511" session)
          "JVMVPlayWithType", (fun () -> JvLink.moviePlayWithType "00" "202609120511" session)
          "JVMVOpen", (fun () -> JvLink.movieOpen "11" "20260905" session |> discard)
          "JVMVRead", (fun () -> JvLink.movieRead session |> discard)
          "JVCourseFile", (fun () -> JvLink.courseFile "test-course" session |> discard)
          "JVCourseFile2", (fun () -> JvLink.courseFile2 "test-course" outputPath session |> discard)
          "JVWatchEvent", (fun () -> JvLink.watchEvent (fun event -> eventOrigin event |> ignore) session)
          "JVWatchEventClose", (fun () -> JvLink.watchEventClose session)
          "m_saveflag", (fun () -> JvLink.getSaveFlag session |> discard)
          "m_savepath", (fun () -> JvLink.getSavePath session |> discard)
          "m_servicekey", (fun () -> JvLink.getServiceKey session |> discard)
          "m_JVLinkVersion", (fun () -> JvLink.getVersion session |> discard)
          "m_TotalReadFilesize", (fun () -> JvLink.getTotalReadFileSize session |> discard)
          "m_CurrentReadFilesize", (fun () -> JvLink.getCurrentReadFileSize session |> discard)
          "m_CurrentFileTimestamp", (fun () -> JvLink.getCurrentFileTimestamp session |> discard)
          "ParentHWnd", (fun () -> JvLink.setParentWindowHandle 0n session)
          "m_payflag", (fun () -> JvLink.getPayFlag session |> discard) ]

module Program =
    [<EntryPoint>]
    let main arguments =
        if arguments <> [| "--com-version" |] then
            printfn
                "Pass --com-version to initialize installed JV-Link and read its version. Other examples are never run automatically."

            0
        else
            JvLink.withSession ConnectionOptions.Default (fun session ->
                JvLink.init "UNKNOWN" session
                |> Result.bind (fun () -> JvLink.getVersion session))
            |> function
                | Ok version ->
                    printfn
                        "MODE=COM ARCH=%O POINTER_SIZE=%d VERSION=%s"
                        Runtime.InteropServices.RuntimeInformation.ProcessArchitecture
                        IntPtr.Size
                        version

                    0
                | Error error ->
                    eprintfn "%s: %s (code=%A)" error.Api error.Message error.Code
                    2

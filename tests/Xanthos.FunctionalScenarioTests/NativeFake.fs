namespace Xanthos.FunctionalScenarioTests

open System
open System.Collections.Generic
open System.IO
open System.Text.Json
open System.Threading
open Xanthos
open Xanthos.Cli

/// A store belongs to one test. Multiple explicit sessions can share it.
type internal SettingsStore() =
    member val SaveFlag = 1 with get, set
    member val SavePath = "initial" with get, set

/// All controlled responses sit below the public functional API.
type internal NativeFake(store: SettingsStore) =
    let calls = ResizeArray<string>()
    let mutable callback: (JvEvent -> unit) option = None
    let mutable disposals = 0
    member _.Calls = lock calls (fun () -> calls |> Seq.toArray)
    member _.Disposals = disposals
    member val Handler: string -> obj[] -> Result<obj, JvError> option = (fun _ _ -> None) with get, set
    member val OnWatch: unit -> unit = ignore with get, set
    member val DisposeError: JvError option = None with get, set
    member val StopWatchError: JvError option = None with get, set

    member _.Emit kind key =
        callback |> Option.iter (fun action -> action { Kind = kind; RawKey = key })

    interface INativeJvLink with
        member this.Invoke(api, arguments, _) =
            lock calls (fun () -> calls.Add api)

            match this.Handler api arguments with
            | Some result -> result
            | None ->
                match api with
                | "JVOpen" ->
                    arguments[3] <- box 1
                    arguments[4] <- box 0
                    arguments[5] <- box "20260905000000"
                    Ok(box 0)
                | "JVSetSaveFlag" ->
                    store.SaveFlag <- unbox arguments[0]
                    Ok(box 0)
                | "JVSetSavePath" ->
                    store.SavePath <- unbox arguments[0]
                    Ok(box 0)
                | "JVRead"
                | "JVGets" ->
                    arguments[0] <- if api = "JVRead" then box "" else box (Array.empty<byte>)
                    arguments[2] <- box ""
                    Ok(box 0)
                | "JVMVRead" ->
                    arguments[0] <- box ""
                    Ok(box 0)
                | "JVSkip"
                | "JVCancel" -> Ok null
                | _ -> Ok(box 0)

        member _.Get(name) =
            lock calls (fun () -> calls.Add name)

            match name with
            | "m_JVLinkVersion" -> Ok(box "0500")
            | "m_saveflag" -> Ok(box store.SaveFlag)
            | "m_savepath" -> Ok(box store.SavePath)
            | "m_servicekey" -> Ok(box "")
            | "m_CurrentFileTimestamp" -> Ok(box "")
            | _ -> Ok(box 0)

        member _.Put(name, _) =
            lock calls (fun () -> calls.Add name)
            Ok()

        member this.Watch(action) =
            lock calls (fun () -> calls.Add "JVWatchEvent")
            callback <- Some action
            this.OnWatch()
            Ok()

        member this.StopWatch() =
            lock calls (fun () -> calls.Add "JVWatchEventClose")

            match this.StopWatchError with
            | Some error -> Error error
            | None ->
                callback <- None
                Ok()

        member this.Dispose() =
            lock calls (fun () -> calls.Add "Dispose")
            Interlocked.Increment(&disposals) |> ignore

            this.DisposeError
            |> Option.iter (fun error -> raise (SessionCleanupException error))

module internal Host =
    let dependencies connect writer token : FunctionalExecution.Dependencies =
        { Connect = connect
          WriteLine = writer
          CancellationToken = token
          HandleConsoleCancellation = false
          EventQueueCapacity = 256
          HasDesktop = true
          Mode = "FAKE" }

    let runWith connect token argv =
        let output = ResizeArray<string>()

        let deps =
            dependencies connect (fun line -> lock output (fun () -> output.Add line)) token

        let code =
            Program.runWith deps (Array.append [| "--com"; "--sid"; "UNKNOWN" |] argv)

        code, String.Join("\n", output)

    let run (native: NativeFake) argv =
        runWith (fun () -> Ok(new Session(native))) CancellationToken.None argv

    let record id =
        use json =
            JsonDocument.Parse(
                File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Contracts", "record-layouts.json"))
            )

        let layout =
            json.RootElement.GetProperty("records").EnumerateArray()
            |> Seq.find (fun r -> r.GetProperty("id").GetString() = id)

        let bytes = Array.create (layout.GetProperty("length").GetInt32()) (byte ' ')

        let write position (value: string) =
            let data = Xanthos.Core.Text.encodeShiftJis value
            Array.Copy(data, 0, bytes, position - 1, data.Length)

        write 1 id
        write 3 "1"
        write 4 "20260913"
        // Official WH race identity: year/month-day/course/meeting/day/race.
        if id = "WE" then
            write 12 "20260913060504"
        elif List.contains id [ "WH"; "HR"; "JC"; "CC"; "AV"; "TC" ] then
            write 12 "2026091306050401"

        if id = "JC" then
            write 77 "00123"
            write 120 "00456"

        bytes[bytes.Length - 2] <- 13uy
        bytes[bytes.Length - 1] <- 10uy
        bytes

    let supplyRecord (native: NativeFake) bytes =
        let mutable read = false

        native.Handler <-
            fun api args ->
                if api = "JVRead" || api = "JVGets" then
                    if read then
                        Some(Ok(box 0))
                    else
                        read <- true

                        args[0] <-
                            if api = "JVRead" then
                                box (Xanthos.Core.Text.decodeShiftJis bytes)
                            else
                                box bytes

                        args[2] <- box "synthetic.jvd"
                        Some(Ok(box bytes.Length))
                else
                    None

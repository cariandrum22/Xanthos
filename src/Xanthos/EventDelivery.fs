namespace Xanthos

open System
open System.Collections.Concurrent
open System.Threading

/// The SDK callback only enqueues; consumer code never runs on the COM STA.
type internal EventDelivery(capacity: int, callback: JvEvent -> unit) =
    let queue = new BlockingCollection<JvEvent>(capacity)
    let gate = obj ()
    let mutable stopped = false
    let mutable worker: Thread option = None
    let mutable error: JvError option = None

    let finish () =
        stopped <- true

        if not queue.IsAddingCompleted then
            queue.CompleteAdding()

    member _.Error = lock gate (fun () -> error)
    member _.IsAlive = lock gate (fun () -> worker |> Option.exists _.IsAlive)

    member _.IsCurrentThread =
        lock gate (fun () -> worker |> Option.exists ((=) Thread.CurrentThread))

    member _.Fail failure =
        lock gate (fun () ->
            if error.IsNone then
                error <- Some failure

            if not stopped then
                finish ())

    member this.Post event =
        lock gate (fun () ->
            if not stopped && not (queue.TryAdd event) then
                this.Fail
                    { Api = "eventQueue"
                      Code = None
                      Kind = JvErrorKind.Busy
                      Outputs =
                        Map.ofList
                            [ "capacity", string capacity
                              "key", event.RawKey
                              "origin", EventKeys.originName event.Kind ]
                      Message =
                        "Event queue capacity was exceeded; delivery has stopped. Inspect subscriptionError and unsubscribe." })

    member this.Start() =
        lock gate (fun () ->
            if not stopped && worker.IsNone then
                let thread =
                    new Thread(
                        ThreadStart(fun () ->
                            try
                                for event in queue.GetConsumingEnumerable() do
                                    if not (lock gate (fun () -> stopped)) then
                                        try
                                            callback event
                                        with ex ->
                                            this.Fail
                                                { Api = "eventCallback"
                                                  Code = Some ex.HResult
                                                  Kind = JvErrorKind.Invocation
                                                  Outputs =
                                                    Map.ofList
                                                        [ "key", event.RawKey
                                                          "origin", EventKeys.originName event.Kind ]
                                                  Message = ex.Message }
                            finally
                                lock gate (fun () -> stopped <- true)
                                queue.Dispose())
                    )

                thread.IsBackground <- true
                thread.Name <- "Xanthos event delivery"
                worker <- Some thread
                thread.Start())

    member _.Stop() =
        let thread =
            lock gate (fun () ->
                if not stopped then
                    finish ()

                worker)

        match thread with
        | Some thread when Thread.CurrentThread <> thread -> thread.Join()
        | Some _ -> ()
        | None -> queue.Dispose()

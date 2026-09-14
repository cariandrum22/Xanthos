namespace Xanthos

open System

/// Explicit ownership of one COM instance. Dispose releases it; closeData only closes its data session.
[<Sealed>]
type Session internal (native: INativeJvLink) =
    let mutable disposed = 0
    let mutable active = 0
    let operationGate = obj ()
    let mutable watch: (int64 * EventDelivery) option = None
    let mutable lastDelivery: EventDelivery option = None
    let mutable generation = 0L
    let mutable deliveries: EventDelivery list = []

    member internal _.WatchError = lastDelivery |> Option.bind _.Error
    member internal _.IsDisposed = Threading.Volatile.Read(&disposed) <> 0

    member internal _.Run(api, operation: INativeJvLink -> Result<'a, JvError>) =
        let error kind message =
            Error
                { Api = api
                  Code = None
                  Kind = kind
                  Outputs = Map.empty
                  Message = message }

        let acquired =
            lock operationGate (fun () ->
                if active <> 0 || Threading.Volatile.Read(&disposed) <> 0 then
                    false
                else
                    // Only owned watch shutdown is joined by Disconnect. Other concurrent
                    // public operations retain the documented Busy contract.
                    active <- if api = "JVWatchEventClose" then 2 else 1
                    true)

        if Threading.Volatile.Read(&disposed) <> 0 then
            if acquired then
                lock operationGate (fun () ->
                    active <- 0
                    Threading.Monitor.PulseAll operationGate)

            error JvErrorKind.Disposed "Session has been disconnected."
        elif not acquired then
            error JvErrorKind.Busy "Another operation owns this session."
        else
            try
                try
                    if Threading.Volatile.Read(&disposed) <> 0 then
                        error JvErrorKind.Disposed "Session has been disconnected."
                    else
                        operation native
                with ex ->
                    Error
                        { Api = api
                          Code = Some ex.HResult
                          Kind = JvErrorKind.Invocation
                          Outputs = Map.empty
                          Message = ex.Message }
            finally
                lock operationGate (fun () ->
                    active <- 0
                    Threading.Monitor.PulseAll operationGate)

    member internal this.BeginWatch(capacity, callback) =
        let delivery = new EventDelivery(capacity, callback)

        let result =
            this.Run(
                "JVWatchEvent",
                fun native ->
                    if watch.IsSome then
                        Error
                            { Api = "JVWatchEvent"
                              Code = None
                              Kind = JvErrorKind.Busy
                              Outputs = Map.empty
                              Message = "A watch subscription is already active." }
                    else
                        native.Watch delivery.Post
                        |> Result.map (fun () ->
                            generation <- generation + 1L
                            watch <- Some(generation, delivery)
                            lastDelivery <- Some delivery
                            deliveries <- delivery :: (deliveries |> List.filter _.IsAlive)
                            generation, delivery)
            )

        match result with
        | Ok _ -> delivery.Start()
        | Error _ -> delivery.Stop()

        result

    member internal this.EndWatch(expectedGeneration: int64 option) =
        let mutable selected = None

        let result =
            this.Run(
                "JVWatchEventClose",
                fun native ->
                    match expectedGeneration, watch with
                    | Some expected, Some(current, _) when expected <> current -> Ok()
                    | Some _, None -> Ok()
                    | _ ->
                        native.StopWatch()
                        |> Result.map (fun () ->
                            selected <- watch |> Option.map snd
                            watch <- None)
            )

        selected |> Option.iter (fun delivery -> delivery.Stop())
        result

    /// Also used when the legacy COM owner is disposed directly rather than through this handle.
    member internal _.ReleaseDelivery() =
        let mutable waiting = Threading.SpinWait()

        while Threading.Volatile.Read(&active) <> 0 && Threading.Volatile.Read(&disposed) = 0 do
            waiting.SpinOnce()

        Threading.Interlocked.Exchange(&disposed, 1) |> ignore

        try
            let mutable failure: exn option = None

            for delivery in deliveries do
                try
                    delivery.Stop()
                with ex ->
                    if failure.IsNone then
                        failure <- Some ex

            failure |> Option.iter raise
        finally
            watch <- None
            deliveries <- []

    member internal this.Disconnect() =
        let onDeliveryThread = deliveries |> List.exists _.IsCurrentThread

        let acquired =
            lock operationGate (fun () ->
                while active = 2 || (active = 3 && not onDeliveryThread) do
                    Threading.Monitor.Wait operationGate |> ignore

                if active <> 0 then
                    false
                else
                    active <- 3
                    true)

        if not acquired && onDeliveryThread && this.IsDisposed then
            // An external Disconnect may be joining this callback.
            Ok()
        elif not acquired then
            Error
                { Api = "disconnect"
                  Code = None
                  Kind = JvErrorKind.Busy
                  Outputs = Map.empty
                  Message = "Wait for the active SDK call to return before disconnecting." }
        else
            try
                try
                    if Threading.Interlocked.Exchange(&disposed, 1) = 0 then
                        let mutable cleanupResult = Ok()

                        try
                            this.ReleaseDelivery()
                        finally
                            match native with
                            | :? INativeCleanup as cleanup -> cleanupResult <- cleanup.Cleanup()
                            | _ -> native.Dispose()

                        cleanupResult
                    else
                        Ok()
                with
                | SessionCleanupException error -> Error error
                | ex ->
                    Error
                        { Api = "disconnect"
                          Code = Some ex.HResult
                          Kind = JvErrorKind.Invocation
                          Outputs = Map.empty
                          Message = ex.Message }
            finally
                lock operationGate (fun () ->
                    active <- 0
                    Threading.Monitor.PulseAll operationGate)

    interface IDisposable with
        member this.Dispose() =
            match this.Disconnect() with
            | Ok() -> ()
            | Error error -> CleanupFailure.report error

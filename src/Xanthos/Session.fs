namespace Xanthos

open System

/// Explicit ownership of one COM instance. Dispose releases it; closeData only closes its data session.
[<Sealed>]
type Session internal (native: INativeJvLink) =
    let mutable disposed = 0
    let mutable active = 0
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

        if Threading.Volatile.Read(&disposed) <> 0 then
            error JvErrorKind.Disposed "Session has been disconnected."
        elif Threading.Interlocked.CompareExchange(&active, 1, 0) <> 0 then
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
                Threading.Volatile.Write(&active, 0)

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
                        selected <- watch |> Option.map snd
                        native.StopWatch() |> Result.map (fun () -> watch <- None)
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
        if Threading.Interlocked.CompareExchange(&active, 1, 0) <> 0 then
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
                        try
                            this.ReleaseDelivery()
                        finally
                            native.Dispose()

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
                Threading.Volatile.Write(&active, 0)

    interface IDisposable with
        member this.Dispose() =
            match this.Disconnect() with
            | Ok() -> ()
            | Error error -> raise (InvalidOperationException(error.Message))

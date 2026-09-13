# Functional event subscriptions

Use `JvLink.subscribe` with an initialized `Session`. Each subscription owns a bounded FIFO queue and a worker thread. SDK callbacks enqueue notifications; consumer code runs on the worker, outside the COM STA. `subscribeWithOptions` accepts a queue capacity and cancellation token. The default capacity is 256 notifications.

```fsharp
JvLink.withSession ConnectionOptions.Default (fun session ->
    JvLink.init "UNKNOWN" session
    |> Result.bind (fun () ->
        JvLink.subscribe (fun notification ->
            match JvLink.toRealtimeRequest notification with
            | Ok request -> printfn "%s %s" request.Dataspec request.Key
            | Error error -> eprintfn "%s" error.Message) session)
    |> Result.bind (fun subscription ->
        use owned = subscription
        System.Console.ReadLine() |> ignore
        JvLink.unsubscribe subscription
        |> Result.bind (fun () ->
            match JvLink.subscriptionError subscription with
            | Some error -> Error error
            | None -> Ok())))
```

`parseEvent` retains the original notification and parses its meeting date, course, race number and optional transmission timestamp. Times are Japanese service-local values represented by `DateTimeKind.Unspecified`.

| Notification | Raw key | Dataspec |
|---|---|---|
| Pay | `YYYYMMDDJJRR` | `0B12` |
| Weight | `YYYYMMDDJJRR` | `0B11` |
| JockeyChange / Weather / CourseChange / Avoid / TimeChange | `JC/WE/CC/AV/TC` + `YYYYMMDDJJRR` + `yyyyMMddHHmmss` | `0B16` |

Pass `request.Key` unchanged to `openRealtime`; never add a dataspec prefix. Unknown origins and malformed keys return errors retaining the origin and raw key in `JvError.Outputs`. A bare key cannot distinguish Pay from Weight; keep `JvEvent.Kind` from the original callback. The legacy string-only parser cannot supply that missing information.

Queue overflow or a consumer exception stops delivery and is exposed by `subscriptionError`. Hosts should monitor it during long-running subscriptions. For the lower-level `watchEvent` operation, use `watchError session`. A delivery failure does not count as successful notification processing.

`unsubscribe` closes the SDK registration and waits for an in-progress callback to return. Calling it from that callback avoids joining itself. Pending queued notifications are discarded during shutdown. Cancellation schedules the same shutdown; an SDK operation already using the session can cause a typed busy error. Dispose the subscription or session to finish cleanup. Session disposal also stops its owned workers. Callbacks must eventually return; arbitrary consumer code is not forcibly terminated.

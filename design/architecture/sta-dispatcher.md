# STA Dispatcher Design

This note documents the STA (Single-Threaded Apartment) dispatcher that backs the COM implementation. It explains why the dispatcher exists, the contract it exposes, and how it integrates with `JvLinkService`.

## Background

- The original implementation executed COM calls via `Task.Run` on the MTA thread pool and used `comLock` for serialization. This relied on .NET's COM marshaler for apartment transitions and could not guarantee timely delivery of ActiveX events such as `WatchEvent`.
- JV-Link expects all calls to be performed on an STA thread that runs a message pump. Without it, JV-Link UI dialogs, watch events, and certain APIs behave unpredictably.
- To handle STA requirements deterministically, we introduced an explicit dispatcher that owns the COM apartment and exposes a simple invocation API to the rest of the runtime.

## Goals

1. Guarantee that every COM call executes on a dedicated STA thread with a running message pump.
2. Provide a single abstraction that both `ComJvLinkClient` and higher layers can use so they no longer rely on `Task.Run`.
3. Preserve existing retry and timeout behaviour inside `JvLinkService`.

## Non-goals

- Scaling out across multiple STA threads or building a thread pool. JV-Link itself is single-threaded.
- Hosting UI frameworks (WinForms/WPF) on the dispatcher thread. The dispatcher only exists to satisfy COM requirements.

## Dispatcher Contract

```fsharp
type IComDispatcher =
    inherit IDisposable
    abstract member Invoke<'a> : name:string * call:(unit -> 'a) -> 'a
    abstract member InvokeAsync<'a> : name:string * call:(unit -> 'a) -> Task<'a>

type IComDispatchProvider =
    abstract member Dispatcher : IComDispatcher
```

`Invoke` blocks the caller until the STA thread completes the work, returning the result (any `Result` wrapping happens at the call site). `InvokeAsync` is used when the caller already manages cancelation or timeouts. `name` is logged whenever the dispatcher reports errors.

`ComJvLinkClient` implements `IComDispatchProvider`, so `JvLinkService` can reuse the same dispatcher without knowing whether the client is the COM implementation or a stub.

## Architecture

### StaThreadDispatcher

- Creates a dedicated `Thread`, sets its apartment state to STA, calls `CoInitializeEx(COINIT_APARTMENTTHREADED)` on startup, and `CoUninitialize` on shutdown.
- Spins up `System.Windows.Threading.Dispatcher.CurrentDispatcher` on that thread and runs `Dispatcher.Run()` as the message pump.
- Uses `Dispatcher.Invoke`/`InvokeAsync` to marshal work items. Minimal tracing is emitted via `Diagnostics.emit`.
- Shutdown is coordinated with `Dispatcher.BeginInvokeShutdown`, a `TaskCompletionSource`, and a bounded `Thread.Join` to guarantee the message loop exits.

### Timeout/Retry Coordination

`JvLinkService.executeCom` now does the following:

1. Determine whether the client exposes a dispatcher. If it does, call `dispatcher.InvokeAsync(name, work)`; otherwise fall back to `Task.Run` (stubs).
2. Race the call against `Task.Delay(policy.Timeout)`. If the delay completes first, the method returns a timeout error (`CommunicationFailure -999`), while the in-flight call continues inside the dispatcher.
3. Apply the existing retry logic for recoverable errors before surfacing a failure to the caller.

`closeQuietly` simply calls `IJvLinkClient.Close();` the COM implementation handles dispatcher teardown inside its `Dispose`.

## Message Pump Strategy

We intentionally rely on `System.Windows.Threading.Dispatcher`:

- It is part of `WindowsBase`, available on any Windows-targeted .NET runtime, and keeps the implementation small.
- JV-Link watch events surface through COM connection points, which require a functioning message pump. Dispatcher fulfils that requirement without additional P/Invoke plumbing.
- A native `GetMessage`/`DispatchMessage` loop remains a fallback idea, but there is no need for it at present.

## Error Handling

- STA thread startup failures map to `ComFaultReason.ActivationFailure` via `ComClientFactory.tryCreate`.
- Exceptions inside dispatcher work items are logged with their type/name and then re-thrown to the caller so that existing `Result` conversions continue to work.
- If the dispatcher thread crashes unexpectedly, `dispatcher.Invoke` throws; higher layers surface the error and encourage the caller to reinitialise JV-Link.

## Resource Management

1. `Dispatcher.BeginInvokeShutdown` stops the message pump.
2. The dispatcher waits for `Dispatcher.Run()` to exit using a `TaskCompletionSource`.
3. `Thread.Join` ensures the STA thread terminates before releasing the COM runtime via `CoUninitialize`.
4. `ComJvLinkClient.Dispose` uses the dispatcher to disconnect event sinks, invoke `JVClose`, and release the RCW with `Marshal.FinalReleaseComObject`.

Each `ComJvLinkClient` owns its dispatcher (a shared dispatcher pool is intentionally out of scope for now).

## Integration with JvLinkService

1. `IJvLinkClient` implementations that expose a dispatcher automatically route all COM calls through it. Stub clients simply do not implement `IComDispatchProvider`.
2. `JvLinkService.executeCom` no longer uses `Task.Run` or `comLock`. It awaits the dispatcher task and applies the same retry/backoff semantics as before.
3. `readWithTimeout`/`skipWithTimeout` remain tiny helpers that call `executeCom`; nothing else in the runtime needs to know about the dispatching mechanism.

## Roadmap

| Phase | Status | Notes |
|-------|--------|-------|
| 0 | ✅ | Document STA approach (this file). |
| 1 | ✅ | Implement `StaThreadDispatcher` and basic tests. |
| 2 | ✅ | Move `ComJvLinkClient` onto the dispatcher. |
| 3 | ✅ | Update `JvLinkService` to remove `Task.Run`/`comLock`. |
| 4 | ☐ | Perform full COM regression testing (watch events, realtime CLI commands) on a Windows machine before each release. |

## Open Questions

- WatchEvent lifecycle: should long-lived CLI processes share a dispatcher instance or create a new client per session?
- Target framework matrix: adding `net8.0-windows` would make the dispatcher available to earlier runtimes, but needs compatibility testing.
- Timeout cancellation: JV-Link APIs do not support cooperative cancellation, so timeouts remain best-effort logging. Future JV-Link updates might expose explicit cancel hooks that we could integrate with the dispatcher.

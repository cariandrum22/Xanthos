# Functional API contract

`Xanthos.JvLink` projects all 26 SDK methods and nine properties as curried functions, with `Session` last and typed `Result` returns. `Records` parses all 38 official record types independently of COM. See the [coverage mapping](../design/architecture/api-coverage.md) for SDK names and the [compiled examples](../samples/Xanthos.Functional/Examples.fs) for partial application and pipelines.

## Ownership and execution

`connect` creates one COM instance and a dedicated STA with a Windows message loop. Use `withSession` or explicitly `disconnect` the returned session. `closeData` closes its current data/movie stream while retaining the connection. `withSession` also cleans up when a consumer raises an exception, then rethrows that consumer exception. A cleanup failure is returned as an error on the normal Result path. Explicit `disconnect` preserves the failing API, SDK code and outputs. Implicit `IDisposable.Dispose` reports cleanup failures through `System.Diagnostics.Trace` without replacing an exception from the application body.

Operations are synchronous. COM calls execute on the owned STA, whose native locale is Japanese to preserve SDK text; caller culture and Windows settings are unchanged. Concurrent use of a session returns `Busy`; calls after disconnect return `Disposed`. The legacy COM adapter queues its operations on the STA before acquiring the operation gate, preserving serialized getters during reads. Reentrant native callbacks still receive `Busy`. Create a separate session for work triggered by notifications. Session disposal owns subscription cleanup; see [event delivery](functional-events.md) for queue limits, callback threads and shutdown behavior.

`disconnect` closes admission to new operations and waits for an in-flight SDK call before releasing the owner exactly once. `withSession` uses the same cleanup path, preserving its result or consumer exception. New operations during shutdown return `Disposed`. If disconnect is called reentrantly from the native owner or a delivery callback that cannot join itself, it requests deferred cleanup and returns; an external `disconnect` can join completion. Deferred cleanup failures are reported through `System.Diagnostics.Trace`. Native calls and application callbacks must eventually return; shutdown does not abort them.

Returned byte arrays belong to the caller and remain valid after subsequent reads or disconnect. Treat them as immutable while sharing them. `JVGets` preserves native bytes; `JVRead` converts the Japanese BSTR back to CP932. Prefer `gets` for raw capture and parse bytes with `Records`, keeping `Filename`, `ByteCount`, `BufferSize` and `ReturnCode` for diagnostics.

See [read fidelity and recovery](read-fidelity.md) for the current conversion-failure contract and the separately specified, unimplemented diagnostic extension.

## States, errors and configuration

Match `OpenOutcome` and `ReadState` explicitly: NoData, file boundaries, download-pending and EOF are distinct from records. Images preserve Available/NoImage and their native output; movie availability and movie-list states are separate from successful playback. Negative SDK errors retain their API name and numeric code in `JvError`; meaningful failed-read outputs are retained in `Outputs`. Do not replace errors with empty successful data or fall back to a stub.

SDK 5.0 x64 can intermittently return `-413`, also reproduced without Xanthos. Preserve the error; its code alone does not identify an HTTP status or transport cause and does not promise retryability. See [SDK-COM-413](sdk-known-limitations.md#sdk-com-413-intermittent-native-communication-failure) for observations and caller responsibilities.

Setters affect the installed SDK's configuration; these settings are not automatically rolled back when a session closes. Tests that change settings must restore them and verify using a fresh instance. `ParentHWnd` is write-only and remains a signed 32-bit COM Long even in x64. `m_payflag` is read-only; use the SDK configuration UI to change its setting. Never log service-key values.

## Time, cancellation and consent

Request dates and event timestamps use Japanese service time (JST); represent them with `DateTimeKind.Unspecified`. There is no implicit conversion from UTC or the caller's timezone. Choose a publication interval containing available data and use `DataSpecs` to validate stream/option/time-range combinations. Record layout selection follows the requested dataspec and the documented historical format, not merely record length.

A cancellation token cannot interrupt an executing native call. CLI streaming observes cancellation between calls, invokes SDK cancellation as appropriate and closes the stream. SDK dialogs wait without an automatic deadline for the user's choice. Refusal or missing agreement (`-305`) terminates with the original error. SDK settings-dialog Cancel has its own documented result and must not be confused with consent refusal.

Unknown record/code values are preserved rather than coerced to a known value. See [record migration](record-migration.md) for typed records, raw values, historical identifiers and field-position errors.

## Process termination

An external process supervisor can force the host process to terminate even while the SDK is waiting. On Windows use process termination (for example, .NET `Process.Kill()` followed by `WaitForExit()`); POSIX `SIGKILL` is not a Windows signal. Forced termination bypasses orderly session cleanup and cannot guarantee settings restoration or completion of pending file writes. It terminates the selected process, not necessarily separate SDK services. See [Microsoft's Process.Kill documentation](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.kill?view=net-10.0). Xanthos does not invoke this automatically or use it to decide consent.

## Migration from service methods

| Previous surface | Functional surface |
|---|---|
| `new ComJvLinkClient()` / owned `JvLinkService` | `JvLink.withSession options action` |
| `Init`, configuration service methods | `init`, `setSavePath`, `setSaveFlag`, `configureUi` |
| `FetchPayloads` / streaming helpers | `openData` or `openRealtime`, then `gets` / `read`, then `closeData` |
| `Status`, `Skip`, `Cancel` | `status`, `skip`, `cancel` on the same session |
| Image/movie service methods | `silksBinary`, `courseFile`, `movieCheck`, `movieOpen`, `movieRead`, etc. |
| String-only event callbacks | `subscribe` receiving `JvEvent.Kind` and the original `RawKey` |
| Earlier record modules | `Records.parse` / `parseWith`; previous layouts are isolated under `Xanthos.Legacy.Records` |

The compatibility service remains available, but its models and error types are not aliases for the new public contract. Update call sites explicitly and retain specification-based tests when migrating.

For both WH and SE records, `WeightChange.Kilograms n` is signed: a decrease of 4 kg is `Kilograms -4`. Use `n` directly for arithmetic or display; do not apply `ChangeSign` / `WeightChangeSign` again. The separate sign and three-character `Raw` magnitude remain available for source auditing. Blank and `999` continue to map to `Missing` and `Unmeasurable`.

For CLI `capture-fixtures`, a command-level `--use-jvgets` or `--no-jvgets` takes precedence over the global option. Without either option, capture uses JVRead. Capture metadata records the actual selected method.

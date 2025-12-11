# JV-Link Early Binding Investigation

## Summary

The JV-Link Type Library declares `JVRead` as `Long JVRead(BSTR* buff, Long* size, BSTR* filename)` but the COM server actually expects caller-managed buffers (equivalent to `LPWSTR` pointers). When the managed code attempts to call `IJVLink.JVRead` via the generated interop assembly, JV-Link returns `RPC_E_SERVERFAULT` as soon as downloads begin. Therefore Xanthos intentionally relies on the late-bound `InvokeMember` path and no longer keeps the experimental early-binding code.

## Observations

- Manually inspecting `ITypeInfo` shows three parameters of type `VT_BSTR | VT_BYREF` plus a `VT_I4` return value, yet successful calls require allocating writable buffers up front (mimicking the legacy C++ sample behaviour).
- F# early-binding experiments that passed `ref string` (or `StringBuilder`) always failed once JV-Link emitted `-3 (DownloadPending)`, whereas the late-bound implementation worked consistently.
- Because the COM server does not honour the Type Library contract, any attempt to consume `IJVLink` directly is fragile and cannot be supported.

### Reproduction Logs

- CLI / Inspector traces before the cleanup consistently showed:

  ```
  [TRACE] TypedJVRead request capacity=65536 length=65536
  [TRACE] TypedJVRead response code=-3 length=0 file= sample=""
  [TRACE] TypedJVRead request capacity=65536 length=65536
  [TRACE] TypedJVRead exception 0x80010105 (capacity=65536)
  ```

  indicating that the first `-3` (DownloadPending) succeeds, but all subsequent typed calls abort with `RPC_E_SERVERFAULT`, forcing the system to fall back to the late-bound invocation.

- Late-bound calls issued immediately after the failure continued to succeed:

  ```
  [READ] Invoking late-bound JVRead for comparison.
  [READ] code=80 len=80 bytes=80 file=JGDW2024112320241122112817.jvd
  ```

- The native helper (`JvLinkInspectorNative.exe`) confirmed the TypeInfo signature but also demonstrated that the COM server only behaves when the caller allocates writable buffers via `SysAllocStringLen` and passes them by reference. It emitted logs such as:

  ```
  [TYPE] JVRead signature
    Params: 3
      [0] VT_BSTR | VT_BYREF
      [1] VT_BSTR | VT_BYREF
      [2] VT_BSTR | VT_BYREF
    Return: VT_I4
  ```

  followed by successful `JVRead` results only when preallocated buffers were supplied. Later, this helper project was removed from the repository once the investigation concluded, but the above behaviour remains documented here.

## Decision

- Xanthos ships only the late-bound COM bridge; the `IJVLink` stubs remain unused.
- Diagnostic code related to early binding has been removed to avoid confusion.
- If the vendor ever realigns the COM implementation with the Type Library, the early-binding approach can be reconsidered.

# Read fidelity and failure recovery

## Design decision

Use `JvLink.gets` / `getsWithCapacity` for ingestion, archival and fixture capture. Preserve each returned byte buffer before parsing it with `Records`. `JvLink.read` / `readWithCapacity` remains the compatibility path for callers requiring JVRead. Never change the native reader automatically during an open stream.

JVRead supplies a Unicode BSTR, which Xanthos encodes as CP932 using exception fallback. Strict conversion prevents silent replacement, but does not prove that re-encoding Unicode recovers the original byte representation. JVGets supplies bytes directly and is the appropriate source for byte hashes and reproducible captures.

## Behavior shipped in 0.3.0

| Boundary | Result and recovery |
|---|---|
| Native read error | Preserve the SDK API/code and available filename/size outputs. Do not infer retryability from the code. |
| JVRead conversion failure | Return an invocation error. The SDK cursor may already have advanced; calling read or gets again does not recover that record. The existing error does not retain the failed BSTR. |
| Byte read succeeds, parser rejects | Keep the bytes and located `RecordParseError`. Fixture capture retains raw data and failure metadata, continues collection and returns a failing exit code. |
| File boundary, pending download or EOF | Treat these as stream states, not records or successful parsing. |

Use a per-record ingestion loop and retain accepted records outside the session. Legacy bulk methods returning a single `Result<list<_>, _>` do not provide a partial list after a read failure. Application recovery must reopen an explicitly chosen acquisition range, retain provenance and deduplicate records; Xanthos does not promise cursor rewind or exactly-once delivery.

## Diagnostic extension: specified, not implemented

This additive API design is reserved for a subsequent implementation. These names and fields are **not available in 0.3.0**:

```fsharp
type ReadFailure =
    { Error: JvError
      RawText: string option
      RawTextTruncated: bool
      OriginalCodeUnitCount: int option
      Filename: string option
      NativeReturnCode: int option
      BufferSize: int option }

val readDetailed: Session -> Result<ReadResult, ReadFailure>
val readDetailedWithCapacity: int -> Session -> Result<ReadResult, ReadFailure>
```

Invoke JVRead exactly once and snapshot returned outputs before conversion. For a positive native return code, encode strictly. A conversion failure preserves the BSTR and native outputs in `ReadFailure`, with `Error.Api = "JVRead"` and `Error.Kind = Invocation`. Keep the conversion exception code separate from `NativeReturnCode`. For non-record states, do not encode an unused buffer. Existing `read` functions can later delegate to this path and project `ReadFailure.Error`, preserving their signatures.

Retain at most 131,072 UTF-16 code units in failure diagnostics, record the original length and mark truncation explicitly. Truncated text is never a complete capture. Do not place record text in `JvError.Outputs`, automatic logs or exception messages. Explicit persistence must preserve UTF-16 code units, including NUL and unpaired surrogates; ordinary JSON/UTF-8 encoding can replace invalid surrogates. A persisted BSTR is evidence of the SDK response, not reconstructed original CP932 bytes.

Verification must cover Japanese text, unmappable characters, unpaired surrogates, NUL, retention limits, non-record states, native errors and a conversion failure followed by a valid record. Assert one native invocation per request, no fallback/rewind, original outputs retained, and no raw text in automatic logs. The extension does not make JVRead lossless or replace JVGets for archival use.

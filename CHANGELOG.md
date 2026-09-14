# Changelog

All notable changes to Xanthos will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Document intermittent native SDK `-413` failures and the caller contract for preserving errors without implicit retry or fallback.

- Validate CLI backend and x64 evidence centrally in the test harness, with regression cases for fallback, conflicting markers and missing evidence.

- Document the public functional ownership/error/time contract and the SDK-to-function migration map. Verify package consumption independently of project references and keep real COM, Stub and deferred service/UI evidence distinct.

- Add lossless, curried `Records` parsers for all 38 official record types, including complete nested arrays, training/weight/mining sentinels and seven explicit legacy identifier layouts. See `docs/record-migration.md`.
- Add pure `DataSpecs` metadata, parsing-option selection and optional JVOpen preflight for documented stream/option/time-range combinations.

- Preserve codes from all 19 official tables through `Codes`, and provide byte-oriented `RecordBytes` readers with lossless headers and field-specific errors. See `docs/record-foundations.md`; full record-layout migration remains separate.

### Fixed

- Bound poisoned-service STA shutdown while retaining normal consent waits and deferred cleanup on the owning STA.
- Preserve application exceptions during implicit disposal; keep explicit cleanup errors and retryable watch-stop failures observable. Join cancellation-owned shutdown before disconnecting.
- Serialize legacy COM getters, preserve all seven native event origins, and validate historical/full-width realtime inputs before native calls.
- Honor capture read-method option precedence and record the actual method in fixture metadata.
- Interpret WH and SE weight changes consistently as signed kilograms while preserving source signs and raw magnitudes.

- Use an Intel macOS CI runner for the explicit x64 CLI test suite.
- Include the functional sample in the solution so Release solution builds also compile the sample in Release.
- Allow the expanded solution's lint scan to finish within the CI time budget; retain its existing non-blocking warning policy.

- Initialize JV-Link's owned STA with a Japanese native thread locale, preserving Japanese COM text and image-pattern arguments when the caller uses an English culture.

- Preserve the seven notification origins and official event keys, report queue/consumer failures, and release partially registered COM handlers and owned delivery threads.
- Preserve movie-list no-data and download-pending outcomes, and close data sessions after SDK setup cancellation or an empty movie-list open.
- Retain the native filename and size in `JvError.Outputs` for SDK read failures so callers can identify a damaged file.
- Avoid a redundant `JVClose` during COM disposal after a successful explicit close, including after event monitoring stops. Track data sessions and event monitoring separately; close sessions that are still open before releasing COM.
- Preserve the API name and original SDK code when `JvLink.disconnect` encounters a cleanup failure, while still attempting COM reference release and STA shutdown.
- Return an empty current-file timestamp when the SDK returns a null BSTR before reading data.

### Changed

- Route CLI COM commands through the public functional API. Add `session-check` for open/status/read/skip/cancel/close/reopen in one Session; preserve parsed records, image states and actual movie-list buffers.
- Capture raw COM fixtures with acquisition metadata and SHA-256 sidecars; prefix filenames with the dataspec to separate streams.

- **Breaking:** isolate previous record modules under `Xanthos.Legacy.Records`; Runtime payload parsing now delegates to `Records`, carries complete official models for all 38 types and preserves located failures in `RecordError`. Unofficial H5 is treated as unknown.

- Functional event subscriptions return `Subscription`; use `JvLink.unsubscribe` and `subscriptionError`. Callbacks run on an owned worker with a bounded queue. See `docs/functional-events.md` for shutdown, cancellation and key handling.
- The new functional `JvLink.movieOpen` returns `VideoOpenOutcome` (`Opened` / `NoData`). `VideoReadOutcome` now includes `DownloadPending`; callers should wait and read again for that case, and close either open outcome.
- `JvLink.openRealtime` returns `RealtimeOpenOutcome`, without placeholder file counts or timestamps that JVRTOpen does not provide.
- Update the build SDK to .NET 10.0.401 and use global.json in CI and release workflows, excluding preview SDKs.
- Update NuGet test dependencies and F# development tools to stable releases.
- Refresh the Nix dependency lock and align its SDK with global.json using verified official archives.
- Point FSharpLint at the solution instead of treating directory names as inline source.

## [0.2.0] - 2026-02-22

### Breaking Changes

- Make `IJvLinkClient.SavePath` property read-only ([#1](https://github.com/cariandrum22/Xanthos/issues/1), [#4](https://github.com/cariandrum22/Xanthos/pull/4))
  - Per JV-Link specification: `SavePath` can only be set via `SetSavePathDirect` method
- Make `IJvLinkClient.ServiceKey` property read-only ([#5](https://github.com/cariandrum22/Xanthos/issues/5))
  - Per JV-Link specification: `ServiceKey` can only be set via `SetServiceKeyDirect` method
- Change default JVRead/JVGets behavior: use JVGets by default; set `XANTHOS_USE_JVREAD=1` to opt out ([#3](https://github.com/cariandrum22/Xanthos/issues/3))
  - `XANTHOS_USE_JVGETS` is still supported as a legacy override

### Fixed

- Reduce excessive diagnostic logging in `checkUseJvGets()` by caching the resolved value ([#2](https://github.com/cariandrum22/Xanthos/issues/2))
- Avoid unsupported COM property access for `ParentHWnd` (write-only) and `m_payflag` (read-only); return clear `Unsupported` errors from `JvLinkService` in COM mode ([#14](https://github.com/cariandrum22/Xanthos/issues/14))
- Improve CLI E2E harness diagnostics for exe-mode builds on Windows ([#17](https://github.com/cariandrum22/Xanthos/issues/17))
- Fix CLI mojibake for Japanese text when stdout is redirected ([#19](https://github.com/cariandrum22/Xanthos/issues/19))

## [0.1.0] - 2025-12-10

### Added

- Initial F# library implementation with full JV-Link API coverage
- Three-layer architecture: Core, Interop, Runtime
  - **Core**: Domain types, error handling, record parsing
  - **Interop**: COM client, stub implementation, event handling
  - **Runtime**: High-level service, validation, configuration
- `JvLinkService` high-level service for JV-Link operations
  - Data fetching with `FetchPayloads`, `StreamRealtimePayloads`
  - Watch events with `StartWatchEvents`, `StopWatchEvents`
  - Media operations (course diagrams, silks images, movies)
  - Configuration management
- `ComJvLinkClient` reflection-based COM client for JV-Link ActiveX
- `JvLinkStub` in-memory test double for cross-platform testing
- JV-Data record type parsers for 29 record types
- CLI sample application (`Xanthos.Cli`) with comprehensive commands
- Unit tests with xUnit and FsCheck property-based testing
- E2E testing infrastructure with Stub mode support
- GitHub Actions CI/CD workflow for cross-platform testing
- Comprehensive error handling with `Result<'T, XanthosError>` types
- Error catalog generated from JV-Link specifications
- API documentation with fsdocs

[0.2.0]: https://github.com/cariandrum22/Xanthos/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/cariandrum22/Xanthos/releases/tag/v0.1.0

# Changelog

All notable changes to Xanthos will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed

- Update CI, release, managed verification and documentation workflows to stable GitHub Actions that use Node.js 24.

## [0.3.1] - 2026-09-16

### Added

- Add structured issue forms, a pull request template and automatic contribution metadata validation.
- Add a managed line coverage badge from verified Linux Coverage suites, with source commit/run provenance and automatic generated-file refreshes on develop.

### Fixed

- Decode documented CP932 aliases in official record text, including roman numerals in BT lineage descriptions, while preserving original bytes and rejecting malformed sequences on all supported platforms.
- Build parser and text test fixtures directly with the CP932 provider so isolated and coverage runs do not depend on other tests registering code pages first.
- Make repeated watch-event subscription tests wait for acknowledged delivery before unsubscribing, and verify that disposed subscribers receive no later events.

## [0.3.0] - 2026-09-14

### Added

- Support JV-Link SDK 5.0 on Windows x64 through a functional F# API: all 26 methods, nine properties and seven event origins, with typed results and explicit session ownership.
- Provide both `net10.0` (portable parsing) and `net10.0-windows` (COM and parsing) assets in the NuGet package.
- Require three-OS Fast/Coverage gates and separate artifact verification; run managed Stress with recorded seeds.
- Add controlled functional CLI scenarios, deterministic record and Session properties, and SDK-free Windows x64 boundary tests.
- Add reviewed test inventories and Fast/Coverage/WindowsManaged/Stress profiles that reject missing cases, unexpected skips and invalid evidence; provide isolated SDK-free Windows CI verification.
- Document intermittent native SDK `-413` failures and the caller contract for preserving errors without implicit retry or fallback.
- Validate CLI backend and x64 evidence centrally in the test harness, with regression cases for fallback, conflicting markers and missing evidence.
- Document the public functional ownership/error/time contract and the SDK-to-function migration map. Verify package consumption independently of project references and keep real COM, Stub and deferred service/UI evidence distinct.
- Add lossless, curried `Records` parsers for all 38 official record types, including complete nested arrays, training/weight/mining sentinels and seven explicit legacy identifier layouts. See `docs/record-migration.md`.
- Add pure `DataSpecs` metadata, parsing-option selection and optional JVOpen preflight for documented stream/option/time-range combinations.
- Preserve codes from all 19 official tables through `Codes`, and provide byte-oriented `RecordBytes` readers with lossless headers and field-specific errors. See `docs/record-foundations.md`.

### Fixed

- Route legacy native notifications directly to the service queue and retain observable overflow during registration and restart.
- Close session admission before joining in-flight SDK calls; preserve scoped results and exceptions, release once, and defer reentrant cleanup to avoid self-deadlock.
- Resolve concatenated dataspec parsing per record kind and reject ambiguous legacy/expanded combinations before acquisition.
- Keep failed COM activation safe through garbage collection; finalize only fully initialized clients and contain finalizer exceptions.
- Preserve the legacy course-image error for NoImage instead of returning a nonexistent path; normalize null SDK string properties to empty strings.
- Add explicit identifier/odds parsing options to compatibility-service acquisition and batch parsing, including collection of partial successes.
- Save rejected fixture bytes and their parse diagnostics, continue collecting later records and dataspecs, and return a failing exit code when retained captures contain parse errors.
- Isolate interactive settings verification before changing the saving flag; verify normal and exception recovery from fresh sessions and retain original cache/data integrity checks.
- Keep consent-policy tests independent of shared thread-pool scheduling without extending their timeout or consent-wait assertions.
- Allow CI to combine successful OS jobs across rerun attempts of the same workflow run, replace rerun artifacts, and require SDK-absence evidence in the Windows gate.
- Preserve CLI notification errors through cleanup, report queue overflow and pending keys, and capture configuration/Stub output through the injected writer.
- Unify coverage and test-evidence completeness checks, verify negative-control rejection reasons, and support reproducible varying-seed Stress runs for existing FsCheck properties.
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
- The functional `JvLink` API is new in 0.3.0; its outcome types do not replace published 0.2.0 function signatures. Functional event subscriptions return `Subscription`; use `JvLink.unsubscribe` and `subscriptionError`. Callbacks run on an owned worker with a bounded queue. See `docs/functional-events.md` for shutdown, cancellation and key handling.
- The new functional `JvLink.movieOpen` returns `VideoOpenOutcome` (`Opened` / `NoData`). `VideoReadOutcome` now includes `DownloadPending`; callers should wait and read again for that case, and close either open outcome.
- `JvLink.openRealtime` returns `RealtimeOpenOutcome`, without placeholder file counts or timestamps that JVRTOpen does not provide.
- Update the build SDK to .NET 10.0.401 and use global.json in CI and release workflows, excluding preview SDKs.
- Update NuGet test dependencies and F# development tools to stable releases.
- Refresh the Nix dependency lock and align its SDK with global.json using verified official archives.
- Point FSharpLint at the solution instead of treating directory names as inline source.
- Manage package and assembly versions centrally in `Directory.Build.props`; require release tags or manual version inputs to match the source version.
- Run required Fast, Coverage and SDK-free Windows checks before publishing, retain their evidence, and update GitHub Release creation to a Node 24 action.

### Breaking Changes

- COM use requires the Windows target, an x64 process and JV-Link 5.0 x64 with its service key registered. Portable record parsing does not require the SDK.
- Previous record modules are now under `Xanthos.Legacy.Records`. Runtime payload parsing delegates to `Records`, carries complete official models for all 38 types and preserves located failures in `RecordError`. Unofficial H5 is treated as unknown. See [record migration](docs/record-migration.md) and the [functional API contract](docs/functional-api.md).
- Update exhaustive matches on `XanthosError` for the new `RecordError` case. Existing payload union case names now carry the official record models, so consumers must update their field access and rebuild.
- Legacy `JvLinkService.WatchEvents` retains its public type but now classifies native events using their SDK origin and official key. Consumers must use `Event` instead of assuming an application-added `0B` prefix or treating a change timestamp as `ParticipantId`. `Serialization.parseWatchEvent` remains available for historical application-formatted strings.

### Known Limitations

- The JVRead string conversion can fail after the SDK cursor advances when a record cannot be encoded as CP932. The read fails without recovering that record; prefer the byte-oriented JVGets path.
- [Read fidelity and recovery](docs/read-fidelity.md) specifies an additive diagnostic extension; it is not implemented in this release.
- UI-capable legacy COM calls wait without a timeout to allow user consent. A native network stall in the same call also remains unbounded and prevents queued cancel/close operations from executing until that call returns.
- All seven notification origins have deterministic contract coverage; live delivery of every origin has not been verified.
- Historical identifier and odds layouts are tested with specification-based fixtures; real legacy captures have not yet been obtained.
- Intermittent native SDK `-413` errors remain observable to callers. Xanthos does not hide them with automatic retries or backend fallback.

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

[Unreleased]: https://github.com/cariandrum22/Xanthos/compare/v0.3.1...develop
[0.3.1]: https://github.com/cariandrum22/Xanthos/compare/v0.3.0...v0.3.1
[0.3.0]: https://github.com/cariandrum22/Xanthos/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/cariandrum22/Xanthos/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/cariandrum22/Xanthos/releases/tag/v0.1.0

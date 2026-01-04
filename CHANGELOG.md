# Changelog

All notable changes to Xanthos will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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

[0.1.0]: https://github.com/cariandrum22/Xanthos/releases/tag/v0.1.0

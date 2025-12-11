# Changelog

All notable changes to Xanthos will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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

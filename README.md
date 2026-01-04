# Xanthos

[![CI][badge-ci]][link-ci]
[![NuGet][badge-nuget]][link-nuget]
[![.NET][badge-dotnet]][link-dotnet]
[![License: MIT][badge-license]][link-license]

F# wrapper library for the JRA-VAN Data Lab. JV-Link COM API.

## Overview

Xanthos provides a type-safe, modern F# interface to the legacy JV-Link ActiveX
COM component. It leverages F#'s powerful type system to model the JRA-VAN API safely,
converting COM exceptions and error codes into idiomatic `Result<'T, Error>` workflows.

### Features

- **Type-safe API**: Discriminated unions and records model JV-Link data structures
- **Error handling**: COM errors mapped to F# Result types
- **Streaming support**: AsyncSeq-based streaming for large data sets
- **Cross-platform development**: Core logic runs on any .NET platform
  (COM interop requires Windows)
- **Stub mode**: Deterministic `JvLinkStub` enables full testability in CI
  where COM is unavailable
- **CLI tooling**: Rich command surface with E2E coverage in stub mode

## Installation

> **Requirements**: .NET 10 SDK. COM interop functionality is only available on
> Windows.
>
> **Important (Windows COM mode):**
> JV-Link COM server is a 32-bit (x86) component and only works in 32-bit
> processes.
> When using `ComJvLinkClient`, your application must target **x86** or use
> **AnyCPU with "Prefer 32-bit" enabled**.
> Running in a 64-bit process will result in `REGDB_E_CLASSNOTREG` errors.
> The `ComClientFactory.tryCreate` function performs an early check
> and returns a clear error message if called from a 64-bit process.

```bash
dotnet add package Xanthos
```

### From Source

```bash
git clone https://github.com/cariandrum22/Xanthos.git
cd Xanthos
dotnet tool restore
dotnet build src/Xanthos/Xanthos.fsproj
```

## Quick Start

```fsharp
open System
open Xanthos.Runtime
open Xanthos.Interop

// Create configuration
let config =
    { Sid = "YOUR_SID"
      SavePath = Some @"C:\JVData"
      ServiceKey = None
      UseJvGets = None }

let request =
    { Spec = "RACE"
      FromTime = DateTime.Today.AddDays(-7.0)
      Option = 1 }

// IMPORTANT: JvLinkService takes ownership of the client and MUST be disposed.
// Use the 'use' keyword to ensure proper cleanup of COM resources.
use service = new JvLinkService(new ComJvLinkClient(), config)

// Fetch data
match service.FetchPayloads(request) with
| Ok payloads -> printfn "Fetched %d payloads" payloads.Length
| Error err -> printfn "Error: %A" err

// Service and client are automatically disposed when leaving scope
```

> **Resource Management:** `JvLinkService` implements `IDisposable` and takes
> ownership of the `IJvLinkClient` passed to its constructor.
> Always use the `use` keyword or explicitly call `Dispose()` to release COM
> resources and avoid STA thread leaks.

## Architecture

Xanthos follows a three-layer architecture:

1. **Core** (`Xanthos.Core`) - Domain models, error types, text/encoding helpers
2. **Interop** (`Xanthos.Interop`) - COM interface implementations and test stubs
3. **Runtime** (`Xanthos.Runtime`) - High-level service orchestration

See [design/architecture/README.md](design/architecture/README.md) for detailed
documentation.

## CLI Commands (E2E Coverage)

All commands support global options:
`--sid --service-key --save-path [--stub] [--diag]`.

| Command | Description |
| ------- | ----------- |
| `version` | Show JV-Link version and evidence markers |
| `download` | Bulk dataspec download & preview (optional persistence) |
| `realtime` | Stream realtime payloads until end/cancel |
| `status` | Report completed file count |
| `skip` | Skip current file |
| `cancel` | Cancel any active session |
| `delete-file` | Delete a saved JV file by name |
| `set-save-flag` | Enable/disable persistence flag |
| `get-save-flag` | Show persistence flag |
| `set-save-path` | Set save path |
| `get-save-path` | Show save path |
| `set-service-key` | Set service key |
| `get-service-key` | Show service key |
| `set-payoff-dialog` | Enable/disable payoff dialog suppression |
| `get-payoff-dialog` | Show payoff dialog suppression |
| `set-parent-hwnd` | Set parent window handle (UI) |
| `get-parent-hwnd` | Show parent window handle (UI) |
| `course-file` | Retrieve course diagram file path + explanation |
| `course-file2` | Retrieve course diagram file path |
| `silks-file` | Generate silks bitmap file |
| `silks-binary` | Retrieve silks bytes |
| `movie-check` | Check movie availability |
| `movie-check-with-type` | Check movie availability with type code |
| `movie-play` / `movie-play-with-type` | Request playback |
| `movie-open` | Retrieve all workout video listings |
| `help` | Show detailed usage text |

> **Note:** The `status`, `skip`, and `cancel` commands require an active JVOpen
> session in the same process. These commands query or control an ongoing download
> operation and will return error code -203 if no session is open.
> In typical CLI usage, they are only meaningful when called from the same
> long-running process that initiated a download.

### CLI Evidence Markers

CLI output includes:

- `EVIDENCE:MODE=COM|STUB`
- `EVIDENCE:VERSION=<string>`

Used by E2E tests to assert activation pathway and version retrieval logic.

## Streaming APIs

- `StreamRealtimePayloads` (sync): handles `FileBoundary` (skips) and `DownloadPending`
  (incremental backoff) without breaking enumeration.
- `StreamRealtimeAsync` (`IAsyncEnumerable`): cooperative cancellation; swallows
  `OperationCanceledException` and returns `false` from enumerator
  for graceful termination.
  The old `StreamRealtimePayloadsAsync` method name remains as a forwarding alias
  for backward compatibility.
- Boundary tests cover consecutive `FileBoundary` markers and prolonged `DownloadPending`
  sequences.

> **Threading note:** The `*Async` methods return `IAsyncEnumerable` and support
> `await foreach` semantics with cooperative cancellation between iterations. However,
> individual JV-Link COM calls execute synchronously on the STA thread and will
> block the calling thread for their duration. The async pattern enables
> cancellation checking and poll interval delays between COM calls, not true
> non-blocking I/O.
> This is a fundamental limitation of COM interop.
> **Heads-up:** `FetchPayloadsWithBytes` replaces the older `FetchPayloadsWithSize`.
> The legacy name still exists as an alias so existing callers keep working, but
> new code should prefer the clearer `*WithBytes` flavor.

## Development Environment

### Native Diagnostics

When you need to inspect the raw `IDispatch` surface or verify the actual JV-Link
COM behaviour outside of .NET, refer to `design/notes/jvlink-early-binding.md`
for the investigation summary.
We confirmed that, despite the Type Library declaring `[out] BSTR*`, the COM server
expects caller-managed buffers, so Xanthos intentionally sticks to late-bound
invocation in production.

> **Note:** The `JVDTLab.JVLink` COM server exposes a Type Library that marks `JVRead`
> parameters as `[out] BSTR*`, but the actual implementation expects caller-managed
> buffers and does not accept the `IJVLink` early-bound interface generated from
> Type Library.
> As a result, Xanthos intentionally relies on the late-bound `InvokeMember`
> path for `JVRead` (and related methods) and treats the type-safe `IJVLink` stubs
> as non-functional diagnostics only.

### Using Nix (Recommended)

```bash
nix develop
```

This provides .NET SDK, Mono, and development tools with telemetry disabled.

### Manual Setup

This project requires the .NET 10 SDK. Follow these steps to install:

#### Windows (winget)

```powershell
winget install Microsoft.DotNet.SDK.10
```

#### macOS (Homebrew)

```bash
brew install --cask dotnet-sdk
```

#### Linux / Manual Download

Download the .NET 10 SDK from the [.NET 10 downloads page][dotnet-10-downloads].
Select your platform and follow the installation instructions.

#### Verify Installation

```bash
dotnet --version
# Should output 10.0.xxx
```

> **Note**: If you have multiple .NET SDKs installed, you can use a `global.json`
> file to pin the SDK version. This project already includes one.

## Code Formatting and Linting

Fantomas and FSharpLint are provided via dotnet tools:

```bash
dotnet tool restore
dotnet fantomas .                    # format all F# sources
dotnet fsharplint lint src tests     # run FSharpLint
```

## Documentation

Generate API documentation locally using FSharp.Formatting:

```bash
dotnet tool restore
dotnet fsdocs build --clean \
  --parameters \
    fsdocs-logo-src img/logo.png \
    fsdocs-favicon-src img/favicon.png \
    fsdocs-license-link \
      https://github.com/cariandrum22/Xanthos/blob/main/LICENSE \
    fsdocs-release-notes-link \
      https://github.com/cariandrum22/Xanthos/releases \
    fsdocs-repository-link \
      https://github.com/cariandrum22/Xanthos
```

The generated site is output to `output/`. View with `dotnet fsdocs watch`.

## Pre-commit Hooks

Enable pre-commit checks locally:

```bash
pip install --user pre-commit
pre-commit install
```

Run manually with `pre-commit run --all-files`.

## Testing

### Running Tests Locally

```bash
# Run all tests
dotnet test

# Run unit tests only
dotnet test tests/Xanthos.UnitTests

# Run E2E tests in stub mode
XANTHOS_E2E_MODE=STUB \
  dotnet test tests/Xanthos.Cli.E2E
```

### Test Structure

| Project | Description | Platform |
| ------- | ----------- | -------- |
| `Xanthos.UnitTests` | Unit tests with JvLinkStub | All |
| `Xanthos.PropertyTests` | FsCheck property-based tests | All |
| `Xanthos.Cli.E2E` | CLI end-to-end tests | All (Stub) / Windows (COM) |

See `tests/README.md` for the naming conventions used across the unit-test suite
(e.g., how `*ErrorTests` vs `*AbnormalTests` are scoped, and the preferred
`Given/When/Then` style for test names).
For CLI E2E test design and coverage details, see
[design/tests/e2e-cli.md](design/tests/e2e-cli.md).

### CI/CD

The project uses GitHub Actions for continuous integration:

- **Build & Test**: Runs on Linux, macOS, and Windows
- **E2E Tests**: Stub mode on all platforms; COM not required for CI
- **Code Quality**: Format checking with `dotnet fantomas --check .`
- **Coverage**: Coverlet (cobertura & opencover) with ReportGenerator HTML summary

See [`.github/workflows/ci.yml`](.github/workflows/ci.yml) for details.

### Windows COM Verification

> **Note:** CI only validates stub mode. The actual COM layer cannot be tested in
> GitHub Actions because JV-Link is a commercial product that requires local
> installation. Before each release, manual verification on a Windows machine with
> JV-Link installed is required.

Before tagging the initial release (and for any later COM regression), run the bundled
PowerShell workflow on a Windows machine with JV-Link installed:

```powershell
pwsh scripts/run-com-verification.ps1 `
    -ServiceKey "YOUR_SERVICE_KEY" `
    -SavePath "C:\JVData" `
    -Sid "YOUR_SID" `
    -Dataspec "RACE" `
    -FromTime "20240101000000" `
    -RealtimeKey "2024010101010101"
```

Parameters:

- `Sid` defaults to `UNKNOWN`; override with your actual SID.
- `ServiceKey` is required (no default).
  You can also set `XANTHOS_E2E_SERVICE_KEY`.
- `SavePath`, `Dataspec`, `FromTime`, and `RealtimeKey` are optional overrides.
- `WatchDurationSeconds` is an optional override.
- `-SkipBuild`, `-SkipTests`, or `-SkipCli` can be supplied for partial runs.

The script performs `dotnet build`, `dotnet test` (with `XANTHOS_E2E_MODE=COM`),
and a series of CLI commands (`version`, `status`, `download`, `watch-events`,
optional `realtime`).
Review the console output and CLI logs to confirm COM execution succeeded before
shipping.

## Updating Error Catalog

`src/Xanthos/Core/ErrorCatalog.fs` is generated from the official tables in
`design/specs/error_codes.md`.
If the specification changes, regenerate the catalog before building:

```bash
python3 scripts/generate_error_catalog.py
```

Do not hand-edit the generated F# source; update the markdown spec and rerun the
script.

## COM vs Stub Mode

- **Stub** mode (default in CI) guarantees deterministic responses; no JV-Link installation
  required.
- **COM** mode (Windows only) can be used locally if JV-Link is installed and
  ProgID registered.
- CLI outputs `EVIDENCE:MODE` so tests can assert which path executed.

## JV-Link Installation Check (Windows)

Ensure the JV-Link COM registration exists before running the COM-backed samples:

```powershell
pwsh scripts/check_jvlink.ps1
```

The script resolves the default ProgID and exits non-zero if missing.

## License

MIT License - see [LICENSE](LICENSE) for details.

## Contributing

Contributions are welcome. Please:

1. Fork & clone.
2. Add tests for new record parsers or CLI commands.
3. Regenerate the error catalog if you modify the spec.
4. Ensure all tests pass in stub mode.

[badge-ci]: https://github.com/cariandrum22/Xanthos/actions/workflows/ci.yml/badge.svg
[badge-dotnet]: https://img.shields.io/badge/.NET-10.0-512BD4
[badge-license]: https://img.shields.io/badge/License-MIT-yellow.svg
[badge-nuget]: https://img.shields.io/nuget/v/Xanthos.svg
[dotnet-10-downloads]: https://dotnet.microsoft.com/download/dotnet/10.0
[link-ci]: https://github.com/cariandrum22/Xanthos/actions/workflows/ci.yml
[link-dotnet]: https://dotnet.microsoft.com/
[link-license]: https://opensource.org/licenses/MIT
[link-nuget]: https://www.nuget.org/packages/Xanthos

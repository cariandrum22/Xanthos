# Xanthos.Cli.E2E

End-to-end test project that runs `samples/Xanthos.Cli` via `dotnet run` and verifies that CLI subcommands work correctly.

## Test Coverage

**38 test cases** covering 31 of 33 CLI commands (93.9%)

### Test Categories

| Category | Tests | Commands |
|----------|-------|----------|
| Basic | 5 | `version`, `download`, `set-save-flag`, `help` |
| Realtime | 1 | `realtime` |
| Config Round-trip | 5 | `get/set-save-flag`, `get/set-save-path`, `get/set-service-key`, `get/set-payoff-dialog`, `get/set-parent-hwnd` |
| Course Diagram | 2 | `course-file`, `course-file2` |
| Silks | 2 | `silks-file`, `silks-binary` |
| Movie | 5 | `movie-check`, `movie-check-with-type`, `movie-play`, `movie-play-with-type`, `movie-open` |
| File Management | 4 | `status`, `skip`, `cancel`, `delete-file` |
| Status Info | 3 | `total-read-size`, `current-read-size`, `current-file-timestamp` |
| Error Cases | 8 | Invalid arguments, unknown commands, etc. |
| Help | 1 | `help` |

### Untested Commands

- `set-ui-properties` - Displays UI dialog, difficult to automate
- `watch-events` - Requires interactive input

## Execution Modes

| Mode | Description | How to Run |
|------|-------------|------------|
| Stub (default) | Runs CLI with `--stub` to verify command structure without COM | `dotnet test tests/Xanthos.Cli.E2E` |
| COM | Runs on Windows with real JV-Link installation | Set `XANTHOS_E2E_MODE=COM` and required env vars |

## Environment Variables

| Variable | Purpose | Notes |
|----------|---------|-------|
| `XANTHOS_E2E_MODE` | `COM` or `STUB` | Auto-detected if omitted |
| `XANTHOS_E2E_SID` | SID for JVInit | Required |
| `XANTHOS_E2E_SERVICE_KEY` | Service key | Required |
| `XANTHOS_E2E_SAVE_PATH` | Path for JVSetSavePath | Optional |
| `XANTHOS_E2E_SPEC` | Default dataspec for `download`/`realtime` | Optional (default: `RACE`) |
| `XANTHOS_E2E_FROM` | Default fromTime | Optional (default: `20240101000000`) |
| `XANTHOS_E2E_OPTION` | Default open option | Optional (default: `1`) |
| `XANTHOS_E2E_DIAG` | Set `true` to add `--diag` flag | Optional |

## Running Tests

### Stub Mode (CI / Non-Windows)

```bash
# Basic execution
dotnet test tests/Xanthos.Cli.E2E

# With explicit environment variables
XANTHOS_E2E_MODE=STUB \
XANTHOS_E2E_SID=TEST_SID \
XANTHOS_E2E_SERVICE_KEY=TEST_KEY \
dotnet test tests/Xanthos.Cli.E2E
```

### COM Mode (Windows + JV-Link)

```powershell
$env:XANTHOS_E2E_MODE = "COM"
$env:XANTHOS_E2E_SID = "YOUR_SID"
$env:XANTHOS_E2E_SERVICE_KEY = "YOUR_SERVICE_KEY"
$env:XANTHOS_E2E_SAVE_PATH = "C:\JVDATA"
dotnet test tests/Xanthos.Cli.E2E
```

### Visual Studio Developer Command Prompt

```cmd
set XANTHOS_E2E_MODE=COM
set XANTHOS_E2E_SID=YOUR_SID
set XANTHOS_E2E_SERVICE_KEY=YOUR_SERVICE_KEY
set XANTHOS_E2E_SAVE_PATH=C:\JVDATA
dotnet test tests\Xanthos.Cli.E2E\Xanthos.Cli.E2E.fsproj
```

## CI Integration

GitHub Actions runs tests with the following configuration:

- **Linux/macOS/Windows**: E2E tests in **Stub mode only**

See [`.github/workflows/ci.yml`](../../.github/workflows/ci.yml) for details.

### Why No COM Mode in CI?

JV-Link is proprietary software that requires:
1. Windows with JV-Link COM component installed
2. Valid JRA-VAN subscription credentials (SID and service key)

Since JV-Link cannot be freely installed on CI runners, **COM mode verification must be performed locally** by maintainers before release.

> **Warning: COM-specific regressions may not be caught by CI**
>
> Stub mode tests verify command-line parsing, argument handling, and high-level control flow,
> but they cannot detect issues in the actual COM interop layer such as:
> - Buffer size mismatches (e.g., JVGets 4KB vs 64KB)
> - SAFEARRAY marshalling errors (e.g., garbage data from array reuse)
> - STA thread deadlocks or timeout behavior
> - InvokeMember parameter binding issues
>
> **Before merging any PR that modifies `ComJvLinkClient.fs`, `StaThreadDispatcher.fs`,
> or retry/timeout logic, run the full COM verification checklist below.**

### Local COM Verification Checklist

Before releasing, maintainers should verify on a Windows machine with JV-Link:

```powershell
# 1. Set credentials
$env:XANTHOS_E2E_MODE = "COM"
$env:XANTHOS_E2E_SID = "YOUR_SID"
$env:XANTHOS_E2E_SERVICE_KEY = "YOUR_SERVICE_KEY"
$env:XANTHOS_E2E_SAVE_PATH = "C:\JVDATA"

# 2. Run E2E tests
dotnet test tests/Xanthos.Cli.E2E --configuration Release

# 3. Verify specific high-risk commands
dotnet run --project samples/Xanthos.Cli -- version
dotnet run --project samples/Xanthos.Cli -- download --spec RACE --from 20240101000000 --option 2
dotnet run --project samples/Xanthos.Cli -- realtime --spec 0B12 --key 20240101
```

Key areas to verify:
- [ ] `version` returns valid version info
- [ ] `download` fetches real data without errors
- [ ] `realtime` streams data correctly
- [ ] Session management (status, skip, cancel) works
- [ ] Movie APIs return expected results
- [ ] Course diagram and silks generation works

## Test Logs

Test logs are saved to `.artifacts/cli-e2e/test-logs/` during execution.
Use these for debugging failed tests.

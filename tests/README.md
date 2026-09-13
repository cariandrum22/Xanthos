# Test Naming & Structure

The unit-test suite is organised by concern rather than feature parity with the production namespaces. When adding new files, prefer the following conventions:

- **`<Feature>Tests.fs`** – the default for focused scenarios (e.g. `StreamRealtimeTests.fs`, `DownloadMonitorTests.fs`). Multiple logical groups can live in the same file by using nested modules.
- **`*ErrorTests.fs` / `*AbnormalTests.fs`** – reserved for negative-path coverage. Use `Error` when the scenario is driven by the component itself (e.g. validation, exception mapping) and `Abnormal` when simulating external faults such as COM failures.
- **`EdgeCaseTests.fs`** – reserved for rarely triggered yet documented behaviours (e.g. zero-length payloads, unusual timestamp formats). Prefer descriptive test names such as ``StreamRealtimePayloads skips file boundaries``.

When possible, name tests using a short _Given/When/Then_ style: ``StreamRealtimeAsync stops consuming when cancellation is requested``. This keeps expectations consistent across `tests/Xanthos.UnitTests` and `tests/Xanthos.PropertyTests`.

# Fixture-Based Testing

Real JV-Link data can be captured on Windows and used as test fixtures. This enables parser verification without requiring COM access during test execution.

## Capturing Fixtures (Windows Only)

Run the CLI on Windows with JV-Link installed:

```bash
dotnet run --project samples/Xanthos.Cli -- \
    --sid YOUR_SID \
    capture-fixtures \
    --output tests/fixtures \
    --specs "RACE,DIFF,0B12" \
    --from "20240101" \
    --max-records 10
```

### Options

| Option | Description |
|--------|-------------|
| `--output` | Directory to save fixtures (required) |
| `--specs` | Comma-separated list of data specs (required) |
| `--from` | Start time for data retrieval (`yyyyMMdd` or `yyyyMMddHHmmss`; required) |
| `--max-records` | Max records per record type (default: 10) |

### Fixture Directory Structure

```
tests/fixtures/
├── RACE/
│   ├── TK/
│   │   ├── 0001.bin
│   │   └── 0001.meta.json
│   ├── RA/
│   │   ├── 0001.bin
│   │   └── 0001.meta.json
│   └── SE/
│       └── ...
├── DIFF/
│   └── ...
└── 0B12/
    └── ...
```

- `.bin` files contain raw payload bytes
- `.meta.json` files contain metadata (timestamp, byte length, record type)

## Running Fixture Tests

```bash
# Run all fixture tests
dotnet test tests/Xanthos.UnitTests --filter "Category=Fixtures"
```

Tests skip automatically if no fixtures are present, making CI builds pass even without captured data.

## Recommended Specs for Coverage

For comprehensive parser coverage, capture these specs. The goal is to cover all 38 record types.

### Essential Specs (Start Here)

| Spec | Description | Record Types | Priority |
|------|-------------|--------------|----------|
| `RACE` | Race schedule/results | TK, RA, SE, HR | **Required** |
| `DIFF` | Difference data | TK, RA, SE, HR, O1-O6 | **Required** |

### Extended Coverage

| Spec | Description | Record Types | Priority |
|------|-------------|--------------|----------|
| `0B12` | Realtime odds | O1-O6, H1, H5, H6 | Recommended |
| `0B31` | Vote counts | H1, H5, H6 | Recommended |
| `BLOD` | Breeding data | UM, KS, CH, BR, BN | Recommended |
| `SNAP` | Snapshot data | Various | Optional |
| `YSCH` | Year schedule | RA | Optional |

### Record Type Categories

All 38 record types should be covered for complete parser verification:

| Category | Record Types | Coverage Source |
|----------|--------------|-----------------|
| Race Data | TK, RA, SE, HR | RACE, DIFF |
| Odds Data | O1, O2, O3, O4, O5, O6 | DIFF, 0B12 |
| Vote Count | H1, H5, H6 | 0B31 |
| Master Data | UM, KS, CH, BR, BN, HN, SK, RC | BLOD |
| Analysis Data | CK, HC, HS, HY, YS, BT, CS, DM, TM, WF, WC | Various |
| Real-time Data | WH, WE, AV, JC, TC, CC, JG | Realtime specs |

### Capture Command Examples

**Basic coverage (core race data):**
```bash
dotnet run --project samples/Xanthos.Cli -- \
    --sid YOUR_SID capture-fixtures \
    --output tests/fixtures \
    --specs "RACE,DIFF" \
    --from "20240101" \
    --max-records 10
```

**Extended coverage (with odds and master data):**
```bash
dotnet run --project samples/Xanthos.Cli -- \
    --sid YOUR_SID capture-fixtures \
    --output tests/fixtures \
    --specs "RACE,DIFF,0B12,0B31,BLOD" \
    --from "20240101" \
    --max-records 5
```

**Full coverage (all available specs):**
```bash
dotnet run --project samples/Xanthos.Cli -- \
    --sid YOUR_SID capture-fixtures \
    --output tests/fixtures \
    --specs "RACE,DIFF,0B12,0B31,BLOD,SNAP,YSCH" \
    --from "20240101" \
    --max-records 3
```

## CI Integration Guide

### Test Categories

Tests are organized into categories that can be selectively run in CI:

| Category | Description | CI Strategy |
|----------|-------------|-------------|
| Unit | Pure F# unit tests | Always run |
| Property | FsCheck property-based tests | Always run |
| Fixtures | Fixture-based parser tests | Skip if no fixtures |
| E2E | End-to-end CLI tests | Windows with JV-Link only |

### GitHub Actions Workflow

```yaml
name: Test Suite

on: [push, pull_request]

jobs:
  unit-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Run Unit Tests
        run: dotnet test tests/Xanthos.UnitTests

      - name: Run Property Tests
        run: dotnet test tests/Xanthos.PropertyTests

  fixture-tests:
    runs-on: ubuntu-latest
    if: github.event_name == 'pull_request'
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4

      # Fixtures are checked into repo or downloaded from artifacts
      - name: Download fixtures
        uses: actions/download-artifact@v4
        with:
          name: test-fixtures
          path: tests/fixtures
        continue-on-error: true

      - name: Run Fixture Tests
        run: dotnet test tests/Xanthos.UnitTests --filter "Category=Fixtures"
```

### Coverage Analysis

The fixture tests include coverage gap analysis. To see the coverage report:

```bash
# Run with verbose output
dotnet test tests/Xanthos.UnitTests --filter "Category=Fixtures" -- -v

# Check for coverage gaps
dotnet test tests/Xanthos.UnitTests --filter "FullyQualifiedName~CoverageGap"
```

### Fixture Freshness

Fixtures should be refreshed periodically (recommended: every 6 months) to ensure compatibility with JV-Link data format updates. The `FixtureFreshnessTests` will warn if fixtures are older than 180 days.

### Maintaining Fixtures

1. **Capture on Windows**: Run `capture-fixtures` on a Windows machine with JV-Link
2. **Keep real captures local**: Use ignored `.artifacts/fixtures/`; commit specification-based synthetic fixtures only.
3. **Retain evidence**: Keep acquisition metadata and hashes with the local captures.
4. **Track coverage**: Monitor the fixture coverage report in CI logs

---

# Manual COM Verification

Use Windows, JV-Link 5.0 x64, a registered x64 service key and the repository's .NET 10 SDK. Run from the signed-in desktop. A software SID is distinct from the service subscription key.

```powershell
./scripts/run-com-verification.ps1 -FromTime 20260905000000
```

Choose a RACE publication interval containing available data. The script publishes the Windows x64 CLI and runs the separate `Xanthos.ComTests` project: 15 required/negative tests, with no skipped cases or COM-to-Stub fallback. Setting `XANTHOS_E2E_MODE=COM` does not turn the explicit Stub E2E project into a COM suite. See [the COM test guide](Xanthos.ComTests/README.md).

## Data and state verification

Use `session-check` to execute open, status, parsed read, skip, cancel, close and reopen in one Session. Repeat with `--no-jvgets` to verify JVRead. Separate CLI invocations of status/skip/cancel cannot certify an open session's behavior.

```powershell
.artifacts/com-verification/cli-x64/Xanthos.Cli.exe --com --diag session-check --spec RACE --from 20260905000000 --max-records 1
.artifacts/com-verification/cli-x64/Xanthos.Cli.exe --com capture-fixtures --specs RACE --from 20260905000000 --max-records 1 --use-jvgets --output .artifacts/fixtures
```

Validate captured `.bin` files against their `.meta.json` sidecars: source stream/interval, SDK version, record ID, byte length, SHA-256 and official parser result. Preserve original bytes. Restore any changed SDK configuration and verify restoration from a fresh instance.

## Interactive and service-dependent evidence

Image normal/NoImage/error outcomes are separate. Playback requests returning zero do not establish that video displayed; record the user's observation. For real notifications, subscribe before publication, retain origin/raw key, retrieve with that same key and parse the result. Synthetic events certify only deterministic behavior.

SDK consent waits for the user's decision without an automatic timeout. Refusal ends the request. Do not automate agreement. Record actual errors and deferred conditions without counting them as passes. A filtered subset is not the full COM gate.

## Reporting and release checks

Record SDK/runtime versions, CLI architecture, mode, command, test discovery/pass/failure/skip counts and cleanup results. Keep machine-specific logs and licensed captures in ignored local storage; share only suitable summaries without service keys.

Before release, require successful builds for both targets, Contract/Stub results, the full COM gate, independent package consumption, and completed or explicitly documented interactive/service conditions. An unresolved required COM check prevents reporting all DoD complete. See [scenario coverage](Xanthos.Cli.E2E/scenarios.md) and [the functional contract](../docs/functional-api.md).

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
    --from "2024-01-01" \
    --max-records 10
```

### Options

| Option | Description |
|--------|-------------|
| `--output` | Directory to save fixtures (required) |
| `--specs` | Comma-separated list of data specs (required) |
| `--from` | Start date for data retrieval (required) |
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
    --from "2024-01-01" \
    --max-records 10
```

**Extended coverage (with odds and master data):**
```bash
dotnet run --project samples/Xanthos.Cli -- \
    --sid YOUR_SID capture-fixtures \
    --output tests/fixtures \
    --specs "RACE,DIFF,0B12,0B31,BLOD" \
    --from "2024-01-01" \
    --max-records 5
```

**Full coverage (all available specs):**
```bash
dotnet run --project samples/Xanthos.Cli -- \
    --sid YOUR_SID capture-fixtures \
    --output tests/fixtures \
    --specs "RACE,DIFF,0B12,0B31,BLOD,SNAP,YSCH" \
    --from "2024-01-01" \
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
2. **Commit to repo**: Check fixtures into `tests/fixtures/` directory
3. **Upload as artifact**: Optionally upload as GitHub Action artifact for larger datasets
4. **Track coverage**: Monitor the fixture coverage report in CI logs

---

# Manual COM Verification

Some tests require real JV-Link COM access and cannot be automated in CI. This section documents the manual verification procedures.

## Test Environment Requirements

| Requirement | Description |
|-------------|-------------|
| OS | Windows 10/11 |
| JV-Link | Installed and configured |
| SID | Valid JRA-VAN subscription ID |
| IDE | Visual Studio 2022+ or VS Code with Ionide |

## Manual Verification Procedures

### 1. CLI E2E Tests (COM Mode)

Run the E2E test suite in COM mode on Windows:

```powershell
# Set environment for COM mode
$env:XANTHOS_E2E_MODE = "COM"
$env:XANTHOS_SID = "YOUR_SID"

# Run E2E tests
dotnet test tests/Xanthos.Cli.E2E --filter "Category=E2E"
```

Expected: All tests pass or skip appropriately based on COM availability.

### 2. Visual Studio COM Execution Test

1. Open `Xanthos.sln` in Visual Studio
2. Set `Xanthos.Cli` as startup project
3. Configure launch settings with your SID:
   ```json
   {
     "profiles": {
       "Xanthos.Cli": {
         "commandLineArgs": "--sid YOUR_SID fetch --spec RACE --from 20240101",
         "environmentVariables": {
           "XANTHOS_USE_JVGETS": "0"
         }
       }
     }
   }
   ```
4. Run with F5 (Debug) or Ctrl+F5 (Release)
5. Verify output shows fetched records without COM errors

### 3. Capture Fixtures Verification

```powershell
# Run fixture capture
.\scripts\capture-fixtures.ps1 -Sid "YOUR_SID" -Specs "RACE,DIFF" -From "20240101"

# Verify captured files
Get-ChildItem -Recurse ./fixtures/*.bin | Measure-Object
```

Expected: `.bin` and `.meta.json` files created for each record type.

### 4. JVGets Mode Verification

Test the JVGets API path:

```powershell
$env:XANTHOS_USE_JVGETS = "1"
dotnet run --project samples/Xanthos.Cli -- --sid YOUR_SID fetch --spec RACE --from 20240101
```

Expected: Data fetched using JVGets instead of JVOpen.

## Verification Checklist

Use this checklist before releases:

```markdown
## COM Verification Checklist - v{VERSION}

**Environment:**
- [ ] Windows version: ___________
- [ ] JV-Link version: ___________
- [ ] .NET version: ___________

**Tests Executed:**
- [ ] CLI E2E (COM mode) - All pass
- [ ] Visual Studio debug run - Success
- [ ] Fixture capture - Files generated
- [ ] JVGets mode - Data fetched

**Record Types Verified:**
- [ ] TK (Track info)
- [ ] RA (Race info)
- [ ] SE (Entry info)
- [ ] HR (Race results)
- [ ] O1-O6 (Odds)

**Issues Found:**
(List any issues encountered)

**Verified By:** ___________
**Date:** ___________
```

## Reporting Verification Results

### For Pull Requests

Add verification results as a PR comment:

```markdown
## Manual COM Verification

✅ Tested on Windows 11 with JV-Link v4.x
- CLI E2E (COM): 15/15 passed
- Fixture capture: 47 files generated
- JVGets mode: Working

No issues found.
```

### For Releases

Include verification evidence in release notes:

```markdown
## Release Verification

This release was verified on Windows with real JV-Link COM:
- All E2E tests passed in COM mode
- Fixture capture verified for RACE, DIFF specs
- JVGets mode tested and working

Test log: [link to gist or artifact]
```

## Troubleshooting COM Issues

| Issue | Cause | Solution |
|-------|-------|----------|
| `ComException: 0x80040154` | COM not registered | Reinstall JV-Link |
| `InvalidSID` | Expired or invalid SID | Renew JRA-VAN subscription |
| `ServerBusy` | JV-Link occupied | Close other JV-Link apps |
| `FileNotFound` | Missing DLL | Check JV-Link installation path |

## CI vs Manual Test Separation

| Test Category | CI (Linux/macOS) | Manual (Windows) |
|---------------|------------------|------------------|
| Unit Tests | ✅ Always run | ✅ Optional |
| Property Tests | ✅ Always run | ✅ Optional |
| Fixture Tests | ✅ If fixtures exist | ✅ Optional |
| E2E (Stub) | ✅ Always run | ✅ Optional |
| E2E (COM) | ❌ Not possible | ✅ **Required for release** |
| Visual Studio COM | ❌ Not possible | ✅ **Recommended** |

---

# Release Gate Requirements

Before tagging a release, the following COM smoke tests **MUST** pass on a Windows machine with JV-Link installed. These tests cannot run in CI and require manual verification.

## Minimum Smoke Test Suite

```powershell
# Set environment for COM mode
$env:XANTHOS_E2E_MODE = "COM"
$env:XANTHOS_SID = "YOUR_SID"

# 1. Verify COM instantiation works
dotnet run --project samples/Xanthos.Cli -- --sid $env:XANTHOS_SID version

# 2. Verify data fetching (JVRead path)
dotnet run --project samples/Xanthos.Cli -- --sid $env:XANTHOS_SID fetch --spec RACE --from 20240101 --limit 5

# 3. Verify JVGets path (if using Gets mode)
$env:XANTHOS_USE_JVGETS = "1"
dotnet run --project samples/Xanthos.Cli -- --sid $env:XANTHOS_SID fetch --spec RACE --from 20240101 --limit 5

# 4. Run E2E test suite in COM mode
dotnet test tests/Xanthos.Cli.E2E --filter "Category=E2E"
```

## Release Checklist

Before each release, fill out and include in the release notes:

```markdown
## COM Smoke Test Results - v{VERSION}

**Test Environment:**
- Windows Version: [e.g., Windows 11 23H2]
- JV-Link Version: [e.g., 4.x.x]
- .NET SDK Version: [e.g., 10.0.100]

**Smoke Test Results:**
- [ ] `version` command: COM client instantiated successfully
- [ ] `fetch` command (JVRead): Data retrieved and parsed
- [ ] `fetch` command (JVGets): Data retrieved via Gets API (if applicable)
- [ ] E2E test suite (COM mode): All tests pass

**Verified By:** ____________
**Date:** ____________
```

## Why This Matters

The CI test suite uses `JvLinkStub` for all tests, which ensures the F# wrapper logic is correct but does NOT verify:

1. **COM Interop works**: The actual `ComJvLinkClient` uses reflection-based COM calls
2. **Property mappings are correct**: COM property names like `m_savepath` must match exactly
3. **Threading model is correct**: STA threading requirements for COM
4. **Error code translation**: Real JV-Link COM error codes map correctly

A release **must not** be tagged without confirming these work against real JV-Link.

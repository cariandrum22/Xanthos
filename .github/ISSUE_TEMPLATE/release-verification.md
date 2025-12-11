---
name: Release Verification
about: Document manual COM verification for a release
title: 'Release Verification - v'
labels: release, verification
assignees: ''
---

# Release Verification - v{VERSION}

## Environment

| Component | Version |
|-----------|---------|
| Windows | |
| JV-Link | |
| .NET SDK | |

## CI Status

- [ ] All GitHub Actions workflows passing

## Manual COM Verification

### CLI E2E Tests (COM Mode)

```powershell
$env:XANTHOS_E2E_MODE = "COM"
$env:XANTHOS_SID = "YOUR_SID"
dotnet test tests/Xanthos.Cli.E2E --filter "Category=E2E"
```

- [ ] All tests passed
- Total: ___/___
- Skipped: ___

### Fixture Capture

```powershell
.\scripts\capture-fixtures.ps1 -Sid "YOUR_SID" -Specs "RACE,DIFF"
```

- [ ] Fixtures generated successfully
- Files created: ___

### JVGets Mode

```powershell
$env:XANTHOS_USE_JVGETS = "1"
dotnet run --project samples/Xanthos.Cli -- --sid YOUR_SID fetch --spec RACE --from 20240101
```

- [ ] Data fetched successfully

### Visual Studio Debug Run

- [ ] Debug run completed without COM errors

## Record Types Verified

- [ ] TK (Track info)
- [ ] RA (Race info)
- [ ] SE (Entry info)
- [ ] HR (Race results)
- [ ] O1-O6 (Odds)

## Issues Found

<!-- List any issues encountered during verification -->

None / [Describe issues]

## Test Logs

<details>
<summary>E2E Test Output</summary>

```
[Paste test output here]
```

</details>

<details>
<summary>Fixture Capture Output</summary>

```
[Paste capture output here]
```

</details>

## Verification Summary

| Check | Status |
|-------|--------|
| CI Tests | :white_check_mark: / :x: |
| CLI E2E (COM) | :white_check_mark: / :x: |
| Fixture Capture | :white_check_mark: / :x: |
| JVGets Mode | :white_check_mark: / :x: |

**Verified By:** @{username}
**Date:** YYYY-MM-DD

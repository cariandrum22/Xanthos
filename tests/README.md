# Test quality and execution

Run commands from the repository root with PowerShell 7 and the .NET SDK in
`global.json`. Each invocation requires a fresh run ID. Results, logs and reports
are written beneath `.artifacts/test-quality/<run>/<os>/<tfm>/<profile>/`.

## Required profiles

```powershell
./scripts/run-test-profile.ps1 -Profile Fast -RunId local-fast-01
./scripts/run-test-profile.ps1 -Profile Coverage -RunId local-coverage-01
./scripts/run-test-profile.ps1 -Profile WindowsManaged -RunId local-windows-01
./scripts/run-test-profile.ps1 -Profile Stress -RunId local-stress-01
```

| Profile | Contents | Conditions |
|---|---|---|
| Fast | Unit/Contract, FsCheck, functional CLI scenarios, legacy Stub smoke | All three CI operating systems; SDK unnecessary |
| Coverage | Unit, Property and FunctionalScenario projects with VSTest Coverlet collector | Production module and individual results required |
| WindowsManaged | Actual WINDOWS assembly, STA, locale, HWND, BSTR/SAFEARRAY and registration rollback | Windows x64, no JV-Link activation or key |
| Stress | Official record generators, all existing FsCheck properties and session models | Recorded FsCheck seed; fixed record/model seeds |
| OptionalFixtures | Exact 13-case fixture allowlist | Missing data is `not-run`, never a passing real-data test |
| Com | Existing 15 required/negative tests against published CLI | Registered JV-Link 5.0 x64; explicit publication interval |
| Interactive | Exclusive real settings change, fresh-session verification and restoration | Signed-in desktop; notification collector must have ended |
| Live | Read the existing collector summary | Never starts, stops or restarts the collector |

`-Projects UnitTests` selects one project. `-Filter` is a diagnostic subset and
does not produce a full-profile passing manifest. `-NoBuild` requires current
Release binaries. Managed tests have a 10-minute inactivity watchdog (Stress:
30 minutes); CI jobs have a separate overall limit. Native consent waits have
no generic timeout. Refusal is reported without retry or fallback.

## Guarantees and ownership

`FunctionalScenarioTests` invokes the production argument parser and
`FunctionalExecution` with an internal native fake. It uses the real public
F# functions and official parsers, labels captured fixtures `MODE=FAKE`, and
checks exact values, byte ownership, call order and cleanup. Production `--com`
uses the real connection factory. No public injection API or fake CLI option
exists. See [functional scenarios](Xanthos.FunctionalScenarioTests/scenarios.md).

CLI notification queues are bounded. Overflow fails the command with its origin,
key and capacity; accepted but unprocessed keys appear as `EVENT_PENDING` with
`retrieval=not-run`, followed by counts. These keys can be retrieved later;
`--open-after` does not claim they were retrieved. Cleanup failures are reported
alongside the original error. Subscription overflow can also stop delivery before
events reach the CLI queue; this is not a lossless durable notification collector.

The 54 cases in `Cli.E2E` are legacy Stub smoke, consent boundaries and harness
checks. They are not 54 successful native API operations. The two independent
setter/getter smoke tests were renamed from `round-trip` to `separate process
smoke`. Same-session settings and fresh connections sharing an owned store are
verified by `SettingsScenarios`; real persistent settings have a separate gate.
`ComTests` and `WindowsTests` remain outside the portable solution test run.

## Inventory and evidence gates

`test-plan.json` is the reviewed logical inventory. IDs hash the project, exact
FQN and complete Theory display name. OS and TFM are separate run dimensions;
Contract classifications overlap the unit suite and must not be added twice.
Required skips, unexpected/missing/duplicate cases, failed/aborted processes,
wrong run/OS/TFM and changed TRX hashes fail `assert-test-evidence.ps1`.
Coverage must contain executed production lines; no percentage threshold or
production exclusion hides missing measurements. Reports remain separate by
OS/TFM/project. Identical collector attachment copies count once.
Coverage reuses the same TRX/plan validator. Explicit diagnostic coverage subsets
are rejected as acceptance evidence. Negative controls check specific rejection
reasons, including a separate hash-tampering control.

Discover each of the six test projects with
`dotnet test <project> -c Release --no-build --list-tests`. For FunctionalScenario,
also discover with `XANTHOS_TEST_PROFILE=Stress`, and union the exact names before
validation. Run `python scripts/test_inventory.py --discovery PROJECT=FILE`
with one argument for each project. Discovery never activates COM. Review and
update the inventory when tests change; CI cannot regenerate it to excuse loss.

```powershell
./scripts/test-evidence-validators.ps1 -RunId gate-controls-01
./scripts/test-fault-controls.ps1 -RunId parser-faults-01 -Faults RecordOffset,NumericScale,RawOwnership
```

These controls use artificial results or isolated source snapshots and must not
be reported as real SDK evidence. The dependent CI PR adds three-OS artifact
aggregation and its negative controls. A hosted CI run is still required to
establish cross-platform success; local results cannot replace its run ID.

## Generators and replay

Fast uses three fixed record/model seeds: 104729, 130363 and 155921. Each official
record ID receives 100 examples per seed; Stress raises this to 1,000 without
source edits. Nonblank bodies, leap dates and missing dates have asserted
classification counts. Legacy formats and field-codec boundaries supplement
the independent 1,270-field contracts. Fast FsCheck uses replay `104729,130363`, size
100 and domain-specific shrinking. Stress includes the existing UnitTests and
PropertyTests properties and chooses a fresh replay seed, stored as `propertyReplay`
in each invocation. To reproduce it, set `XANTHOS_PROPERTY_REPLAY=seed,gamma` before
running the Stress profile with a fresh run ID. Direct Stress test runs also require
this variable. Model failures save the seed and original/
minimized integer command traces; commands are defined in `SessionModelTests.fs`.
Stress uses ten model seeds, 100 sequences each, with 200–250 operations.
Use `-Filter 'FullyQualifiedName~OfficialRecordProperties'` for a local subset.

`Contracts/field-categories.json` fixes 32 field/model types and their applicable
record IDs. Three types are layout metadata covered by format/dispatcher tests;
the other 29 require nonzero boundary/sentinel counts for every fixed seed.
These are field-type representatives, supplemented by the spreadsheet tests
for individual positions and repeated elements. Changing a required category
requires reviewing the manifest; a missing category fails the test.

Profile runs store synthetic failure JSON in each project's `generated-failures/`
directory. Record failures include original/minimal bytes, field positions and
before/after values. Minimization preserves the exception type and message.
Set `XANTHOS_FAILURE_DIRECTORY` when running `dotnet test` directly to choose
the destination. Replay a saved model trace against a selected build:

```powershell
dotnet fsi scripts/replay-model-failure.fsx tests/Xanthos.FunctionalScenarioTests/bin/Release/net10.0/Xanthos.FunctionalScenarioTests.dll <failure.json>
./scripts/test-fault-controls.ps1 -RunId model-controls-01 -Faults ReadReturnCode,MissingCategory
```

WindowsManaged injects resolution, activation and release into the WINDOWS COM
client, so its actual STA/reflection/HRESULT path runs without creating the
JV-Link server. Registration rollback and removal failures are separate checks.
The public constructor retains normal JV-Link activation and still requires
the Com profile for real SDK evidence.

## Naming and fixtures

Use `<Feature>Tests.fs` with descriptive behavior names. Use `*ErrorTests.fs`
for component-reported errors and `*AbnormalTests.fs` for injected external
failures. Keep F# compilation order explicit in project files. Prefer exact
expected values over marker-only assertions or accepting either Result branch.

Synthetic fixed-length records derive from independent `Contracts/*.json`.
`Contracts/field-applicability.json` fixes each mapped field's semantic profile and
required categories. `FieldApplicabilityProperties` exercises those profiles through
the public record parsers with three seeds, checking typed values, exact errors and
the first/last repeated slots. The manifest distinguishes capped/uncapped odds and
training times, elapsed-time encodings, registration/score limits, optional identifiers
and record-specific sentinel meanings. Header framing and composite identity fields
retain their separate contract checks; they cannot be used as arbitrary exemptions.
Update this manifest and the test inventory when changing mappings; never derive
expected values or required categories from successful parser output.
Licensed captures belong in ignored `tests/fixtures/` or `.artifacts/`, with
source/interval, SDK version, record ID, byte length and SHA-256 metadata.
Use data-spec mappings from `Contracts/dataspecs.json`, not guessed names.

```powershell
./scripts/run-test-profile.ps1 -Profile OptionalFixtures -RunId fixtures-01
```

## Manual COM Verification

Use Windows, JV-Link 5.0 x64, a registered x64 service key and the repository's .NET 10 SDK. Run from the signed-in desktop. A software SID is distinct from the service subscription key.

```powershell
./scripts/run-com-verification.ps1 -FromTime 20260905000000
```

Choose a RACE publication interval containing available data. The script publishes the Windows x64 CLI and runs the separate `Xanthos.ComTests` project: 15 required/negative tests, with no skipped cases or COM-to-Stub fallback. Setting `XANTHOS_E2E_MODE=COM` does not turn the explicit Stub E2E project into a COM suite. See [the COM test guide](Xanthos.ComTests/README.md).

### Data and state verification

Use `session-check` to execute open, status, parsed read, skip, cancel, close and reopen in one Session. Repeat with `--no-jvgets` to verify JVRead. Separate CLI invocations of status/skip/cancel cannot certify an open session's behavior.

```powershell
.artifacts/com-verification/cli-x64/Xanthos.Cli.exe --com --diag session-check --spec RACE --from 20260905000000 --max-records 1
.artifacts/com-verification/cli-x64/Xanthos.Cli.exe --com capture-fixtures --specs RACE --from 20260905000000 --max-records 1 --use-jvgets --output .artifacts/fixtures
```

Validate captured `.bin` files against their `.meta.json` sidecars: source stream/interval, SDK version, record ID, byte length, SHA-256 and official parser result. Preserve original bytes. Restore any changed SDK configuration and verify restoration from a fresh instance.

The interactive settings test can show a native confirmation to delete previously
imported data when disabling saving. Choose **No** to that deletion request; do not
automate it or terminate the process before its restoration checks finish. In the
verified SDK 5.0 run, No declined deletion while the setting still changed and the
method returned zero. Always determine setting/restoration success by fresh-session
reads, not by interpreting a dialog choice as the method's return value.

### Interactive and service-dependent evidence

Image normal/NoImage/error outcomes are separate. Playback requests returning zero do not establish that video displayed; record the user's observation. For real notifications, subscribe before publication, retain origin/raw key, retrieve with that same key and parse the result. Synthetic events certify only deterministic behavior.

SDK consent waits for the user's decision without an automatic timeout. Refusal ends the request. Do not automate agreement. Record actual errors and deferred conditions without counting them as passes. A filtered subset is not the full COM gate.

For SDK-free Windows evidence, `scripts/run-windows-managed-ci.ps1 -RunId <unique-id>` first
checks both registry views, SDK DLLs and services without activating COM. It requires
a Windows x64 runner without JV-Link. The underlying profile accepts
`-RequireSdkAbsent` and stores `windows-environment.json` beside its invocation.
Ordinary local WindowsManaged runs remain usable with an installed SDK.
The dedicated `windows-managed.yml`
workflow runs only this profile and retains the environment record and TRX; its
actual CI result is required separately from tests on an SDK-installed developer PC.

### Reporting and release checks

Record SDK/runtime versions, CLI architecture, mode, command, test discovery/pass/failure/skip counts and cleanup results. Keep machine-specific logs and licensed captures in ignored local storage; share only suitable summaries without service keys.

Before release, require successful builds for both targets, Contract/Stub results, the full COM gate, independent package consumption, and completed or explicitly documented interactive/service conditions. An unresolved required COM check prevents reporting all DoD complete. See [scenario coverage](Xanthos.Cli.E2E/scenarios.md) and [the functional contract](../docs/functional-api.md).

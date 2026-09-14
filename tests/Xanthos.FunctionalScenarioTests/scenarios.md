# Functional CLI scenarios

The host calls `Program.runWith`, the production parser and `FunctionalExecution`
with internal dependencies. Only `INativeJvLink` is controlled. Tests cannot
activate JV-Link through this factory; each created fake and disposal is counted.
Fixtures use official fixed lengths and independent layouts, with `MODE=FAKE`.
Production `Program.main` always supplies `productionDependencies()`.

| Scenario | Guaranteed behavior |
|---|---|
| S01 | JVRead/JVGets exact bytes, parsed WH, EOF and cleanup |
| S02–S03 | NoData makes no read; pending/boundaries do not count as records |
| S04–S05 | SDK filename/code and parser field/position errors survive |
| S06–S08 | Explicit consent gate, refusal without retry, deferred cancellation |
| S09–S10 | Cancel/close ordering and reopening one Session |
| S11 | All seven origins retain raw key/dataspec through fetch and parse |
| S12 | Callback failure and both bounded queues report errors and release |
| S13 | Image availability, exact explanation and binary ownership |
| S14 | Movie record/pending/EOF/NoData preserve buffer metadata |
| S15 | Noninteractive UI rejected before connection; zero versus -305 |
| S16 | Cleanup failure remains nonzero while disposal is attempted |

`CommandScenarios` has one row for every `Command` union case, plus a reflection
check that the table is exhaustive. Unsupported property directions explicitly
fail; Help needs no connection. `SettingsScenarios` checks exact state in one
Session and fresh CLI connections to an owned store, including ignored-setter
fault controls. Native persistence is established only by the exclusive
Interactive profile after live collection ends.

Each test owns temporary paths, callbacks and workers. Gates release in `finally`;
no arbitrary sleep simulates native consent. `SessionModelTests` adds deterministic
operation sequences and native cleanup obligation checks. Consult `test-plan.json`
for exact case IDs and `scripts/run-test-profile.ps1` for Fast/Stress execution.

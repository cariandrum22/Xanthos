# CLI scenario coverage

The 40 original scenarios plus the session-check COM-only guard run with explicit STUB and Category=StubX64. Three consent-boundary tests and ten process-evidence checks bring the E2E project total to 54. The evidence checks use controlled output, not actual COM. COM validation uses the separate Xanthos.ComTests project with 15 required/negative tests. Its required tests never accept STUB fallback. Movie playback and settings dialogs require observed interactive results.

The shared harness rejects a backend different from the requested mode, including conflicting markers. Every COM result must report x64 and an eight-byte pointer; a successful COM run must also report the functional API. Activation failures can omit backend/API markers but cannot advertise a fallback. Logs are retained before validation so a rejected run remains diagnosable.

| # | STUB scenario | COM category / remaining verification |
|---|---|---|
| 1 | version reports JV-Link version and evidence markers | ComX64Required / T02 |
| 2 | download emits evidence markers and payload preview | ComX64Required / T04,T05,T09 |
| 3 | download with output writes files in persist folder | ComX64Required / T04,T05,T09 |
| 4 | set-save-flag executes successfully | ComX64Required / T04,T05,T09 |
| 5 | COM diagnostics appear only in COM mode | ComX64Required / T04,T05,T09 |
| 6 | realtime streams payloads until end of stream | ComX64Required / T04,T05,T09 |
| 7 | set-save-flag and get-save-flag separate process smoke | ComX64Required / T04,T05,T09 |
| 8 | set-save-path and get-save-path separate process smoke | ComX64Required / T04,T05,T09 |
| 9 | initialization reuses configuration without registering a key | ComX64Required / T04,T05,T09 |
| 10 | get-service-key reads current key | ComX64Required / T04,T05,T09 |
| 11 | get-payoff-dialog reads current flag | ComX64Required / T02 |
| 12 | set-parent-hwnd executes successfully | ComX64Required / T04,T05,T09 |
| 13 | course-file retrieves course diagram | ComX64Required / T04,T05,T09 |
| 14 | course-file2 retrieves course diagram (v2) | ComX64Required / T04,T05,T09 |
| 15 | silks-file generates silks bitmap | ComX64Required / T04,T05,T09 |
| 16 | silks-binary retrieves silks data | ComX64Required / T04,T05,T09 |
| 17 | movie-check checks movie availability | ComX64Required or licensed media / T04,T09 |
| 18 | movie-check-with-type checks movie availability with type | ComX64Required or licensed media / T04,T09 |
| 19 | movie-play requests movie playback | ComX64Interactive / T04,T09 |
| 20 | movie-play-with-type requests movie playback with type | ComX64Interactive / T04,T09 |
| 21 | movie-open retrieves workout video listings | ComX64Interactive / T04,T09 |
| 22 | status without open session returns error or reports invalid state | Contract / argument validation; T09 |
| 23 | skip without open session returns error or reports invalid state | Contract / argument validation; T09 |
| 24 | cancel succeeds even without open session | ComX64Required / T04,T05,T09 |
| 25 | delete-file executes or reports file not found | ComX64Required / T04,T05,T09 |
| 26 | total-read-size retrieves total file size | ComX64Required / T02 |
| 27 | current-read-size retrieves current file size | ComX64Required / T02 |
| 28 | current-file-timestamp retrieves timestamp | ComX64Required / T02 |
| 29 | download with invalid dataspec returns error | Contract / argument validation; T09 |
| 30 | download without required arguments returns error | Contract / argument validation; T09 |
| 31 | realtime without required arguments returns error | Contract / argument validation; T09 |
| 32 | unknown command returns error | Contract / argument validation; T09 |
| 33 | set-save-flag with invalid value returns error | Contract / argument validation; T09 |
| 34 | set-parent-hwnd with non-integer returns error | Contract / argument validation; T09 |
| 35 | silks-file without required arguments returns error | Contract / argument validation; T09 |
| 36 | movie-check-with-type without required arguments returns error | Contract / argument validation; T09 |
| 37 | help displays usage information | Contract / argument validation; T09 |
| 38 | watch-events starts and stops successfully | ComX64Required / T04,T05,T09 |
| 39 | capture-fixtures requires COM connection | Contract / argument validation; T09 |
| 40 | capture-fixtures uses sensible defaults | Contract / argument validation; T09 |
| 41 | session-check rejects explicit STUB without COM activation | Stateful normal acquisition is verified by ComX64Required |

Configuration setter/getter pairs in the STUB suite verify individual commands, not persistence across independent stub processes. Real configuration restoration and session state are separate COM requirements. Registered service key values are never test inputs or logged output.

Production functional scenarios are documented separately in [FunctionalScenarioTests](../Xanthos.FunctionalScenarioTests/scenarios.md). Their native fake is internal and uses the real command branches and parsers.

# Windows x64 COM verification

Run `scripts/run-com-verification.ps1` from x64 PowerShell with the repository's .NET SDK. It publishes the Windows x64 CLI and runs this separate test project against that exact executable. The installed JV-Link 5.0 x64 service key must already be registered. Tests never register or print a key.

The gate requires 15 tests: 11 `ComX64Required` checks and 4 `ComX64Negative` checks. Required checks include real non-empty RACE acquisition through both readers, cancel/close/reopen, Japanese course descriptions, No Image and subscription cleanup. They reject missing results, skipped tests, failed discovery, x86 executables, and COM-to-STUB fallback. This project is outside the portable solution; absence of COM or a service error is a failure here. Actual playback and live notifications require separate interactive evidence.

The shared harness validates mode and architecture on every run, independently of individual test assertions. Successful COM commands must emit `EVIDENCE:MODE=COM`, `EVIDENCE:API=FUNCTIONAL`, `EVIDENCE:ARCH=X64` and `EVIDENCE:POINTER_SIZE=8`. Conflicting markers fail even if the process exits successfully. Negative tests can fail before COM activation while retaining architecture evidence.

For an existing published executable:

```powershell
./scripts/run-com-verification.ps1 -SkipPublish -CliPath C:/absolute/path/Xanthos.Cli.exe -FromTime 20260905000000
```

Choose `FromTime` from an available RACE publication interval; an empty response fails. The script supplies `XANTHOS_COM_FROM_TIME` to the tests and restores its previous value. Set that variable explicitly when invoking `dotnet test` directly. Acquisition uses the registered save path; raw comparison files are kept under ignored `.artifacts/cli-e2e/com-records/`.

## Initial consent and cancellation

Run acquisition and SDK settings/playback commands on the signed-in Windows desktop. If an SDK dialog appears, Xanthos waits for the user's choice without a timer. The CLI rejects these commands in Session 0 or with `--non-interactive`, even when a previous installation has been initialized, because another SDK dialog may still be needed.

SDK error `-305` means terms have not been agreed to; it does not distinguish explicit refusal from missing agreement. Xanthos retains the API name and code, releases its client, and exits with code 2 without retry or fallback. Settings-dialog Cancel returns 0 according to the SDK and is a separate case.

An in-progress native COM call cannot be safely interrupted by a library cancellation token. The token remains pending until the SDK call returns, then stops reading and closes the session. The host decides whether explicit process termination is appropriate; the library never exits its host. The CLI's ordinary Ctrl+C process termination remains available; continuous streaming handles Ctrl+C cooperatively between COM calls.

`ConsentTests` uses a controlled boundary to verify waiting, refusal and pending cancellation. These deterministic tests are not evidence of live SDK dialog interaction. Desktop acceptance and subsequent actual COM acquisition are recorded separately in ignored local verification artifacts.

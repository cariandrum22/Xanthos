# Functional CLI execution

`--com` connects through `JvLink.connect`; COM commands use the public curried functions and emit `EVIDENCE:API=FUNCTIONAL`. Activation failure exits without switching to a stub. `--stub` retains the explicit deterministic sample backend.

Publish the x64 Windows executable:

```powershell
dotnet publish samples/Xanthos.Cli -c Release -f net10.0-windows -r win-x64 --self-contained false -o .artifacts/cli
```

Use a publication interval containing available data. This command exercises open, status, a parsed record, skip, cancel, close, reopen and another parsed record in one process and Session:

```powershell
.artifacts/cli/Xanthos.Cli.exe --com --diag session-check --spec RACE --from 20260905000000 --max-records 1
```

`session-check` requires real COM and a non-empty response. Update the example date for the available delivery period. Repeat with `--no-jvgets` before the command to exercise JVRead; `--use-jvgets` selects JVGets. Independent `status`, `skip` and `cancel` invocations each create a fresh session and cannot certify an already-open session's behavior.

Downloads and realtime responses are parsed through `Records`; failures terminate with the record, field and byte position. Raw files can be retained with `download --output` or `capture-fixtures`. Historical identifier interpretation follows the requested dataspec; odds limits follow the race date. `DataSpecs.validateOpen` checks supported option/time-range combinations.

`capture-fixtures` writes `SPEC_ID_NNN.bin` and a `.meta.json` sidecar with the acquisition interval (JST), dataspec, SDK version, source filename, parser status and SHA-256. Keep licensed captures in ignored local directories. Different dataspecs have distinct filenames.

`watch-events --open-after` retains the notification origin and original key and retrieves its response through a separate owned Session. Ctrl+C cancels data/notification loops; SDK consent dialogs wait for the user's decision without an automatic timeout. Declining ends the request.

`movie-open` preserves NoData and download-pending states and prints the actual JVMVRead buffer size and byte count. The SDK requires a racing-viewer-enabled software SID. Its interface specification supplies `SA000000/SD000004` for development, selectable through `--sid`; this is not a production registration or a service key. An unregistered SID can return -304. Do not report that response as successful retrieval or playback.

JV-Link's native Japanese text conversion requires a Japanese native thread locale. Xanthos sets it on its own STA before activating COM. The caller's culture and Windows settings remain unchanged. Images preserve Available/NoImage and native outputs; server errors remain errors.

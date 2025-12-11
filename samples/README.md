# Samples

This directory hosts runnable JV-Link examples. Each sample can be launched with `dotnet run --project samples/<SampleName>`; when the selected target framework is `net10.0-windows` it uses the real JV-Link COM client, while other frameworks fall back to the in-memory `JvLinkStub`.

## Xanthos.Cli

A comprehensive command-line interface for exercising every JV-Link API (bulk download, realtime streaming, housekeeping, movie APIs, event monitoring, etc.).

> **Note**: All commands use `JvLinkService` and interact with the underlying COM client when available (Windows with JV-Link installed). On non-Windows platforms or when `--stub` is specified, the CLI falls back to `JvLinkStub` for testing purposes.

### Environment Variables

| Variable | Required | Description |
|----------|----------|-------------|
| `XANTHOS_JVLINK_SID` | Yes (unless `--sid`) | SID passed to `JVInit` |
| `XANTHOS_JVLINK_SAVE_PATH` | No | Path supplied to `JVSetSavePath` |
| `XANTHOS_JVLINK_SERVICE_KEY` | No | Key supplied to `JVSetServiceKey` |
| `XANTHOS_JVLINK_OPTION` | No | Default `JVOpen` option for `download` command |
| `XANTHOS_JVLINK_OUTPUT` | No | Default output directory for persisted payloads |

### Basic Usage

```bash
dotnet run --project samples/Xanthos.Cli --framework net10.0-windows -- <command> [options]
```

Global options (`--sid`, `--service-key`, `--save-path`, `--stub`, `--diag`, `--help`) can appear before any command.

### Command Examples

#### Data Retrieval

```bash
# Bulk download
dotnet run --project samples/Xanthos.Cli --framework net10.0-windows -- \
  download --spec RACE --from 20240101000000 --option 1 --output ./downloads

# Realtime session (JVRTOpen)
# Key format: YYYYMMDDJJKKHHRR (race), YYYYMMDD (daily), or WatchEvent param
dotnet run --project samples/Xanthos.Cli --framework net10.0-windows -- \
  realtime --spec RACE --key 20240101
```

#### Event Monitoring

```bash
# Watch events for 60 seconds
dotnet run --project samples/Xanthos.Cli --framework net10.0-windows -- \
  watch-events --duration 60

# Watch events and open realtime session on trigger
dotnet run --project samples/Xanthos.Cli --framework net10.0-windows -- \
  watch-events --duration 60 --open-after
```

#### Session Control

```bash
# Check session status
dotnet run --project samples/Xanthos.Cli --framework net10.0-windows -- status

# Delete a specific file
dotnet run --project samples/Xanthos.Cli --framework net10.0-windows -- \
  delete-file --name JGDW2024112320241122112817.jvd
```

#### Movie APIs

```bash
# Check movie availability by type
dotnet run --project samples/Xanthos.Cli --framework net10.0-windows -- \
  movie-check-with-type --movie-type 101 --key 202401010101

# Open movie session
dotnet run --project samples/Xanthos.Cli --framework net10.0-windows -- \
  movie-open --movie-type 101 --search-key 202401010101
```

#### Test Fixture Capture (Windows only)

```bash
# Capture fixtures for multiple specs with date range
dotnet run --project samples/Xanthos.Cli --framework net10.0-windows -- \
  capture-fixtures --output ./fixtures --specs RACE,TOKU,DIFF \
  --from 20240101000000 --to 20240131235959 --max-records 50
```

### Command Categories

Run with `--help` to see the complete list. Commands are grouped as follows:

| Category | Commands |
|----------|----------|
| **Data Retrieval** | `download`, `realtime` |
| **Session Control** | `status`, `skip`, `cancel`, `delete-file` |
| **Event Monitoring** | `watch-events` |
| **Configuration** | `set/get-save-flag`, `set/get-save-path`, `set/get-service-key`, `set/get-parent-hwnd`, `set/get-payoff-dialog`, `set-ui-properties` |
| **Assets** | `course-file`, `course-file2`, `silks-file`, `silks-binary` |
| **Movie APIs** | `movie-check`, `movie-check-with-type`, `movie-play`, `movie-play-with-type`, `movie-open` |
| **Metrics** | `version`, `total-read-size`, `current-read-size`, `current-file-timestamp` |
| **Testing** | `capture-fixtures` (Windows only) |

### Diagnostics

COM diagnostics are routed through `Xanthos.Interop.Diagnostics` only when `--diag` is specified. On non-Windows platforms, the CLI automatically falls back to `JvLinkStub`, which exercises the same code paths for regression testing.

# E2E CLI Test Plan

This document defines the end-to-end scenarios that the command-line tool must execute against JV-Link. Each command maps to one or more COM APIs. Unless otherwise noted, all commands support both **COM** mode (default on Windows) and **stub** mode (`--stub`) for CI environments.

## Test Environment

| Item | Notes |
| --- | --- |
| OS | Windows 10/11, 32-bit process (x86) |
| Tool | `Xanthos.Cli` (Console app in `samples/Xanthos.Cli`) |
| Config | SID / ServiceKey / SavePath read from CLI args or environment variables (`XANTHOS_JVLINK_*`) |
| Modes | `COM` (default), `--stub` for deterministic tests |
| Logging | `--diag` enables COM diagnostics |

## Command Matrix

| Command | JV-Link APIs | Purpose / Expected Outcome | Stub Coverage |
| --- | --- | --- | --- |
| `download --spec <dataspec> --from <ts> [--option <1-4>] [--output <dir>]` | `JVOpen`, repeated `JVRead`, `JVClose` | Materialises all payloads for a dataspec, logs download counts, handles `FileBoundary`/`DownloadPending`. | Yes |
| `realtime --spec <dataspec> [--from <ts>]` | `JVRTOpen`, repeated `JVRead`, `JVCancel` | Streams realtime payloads until cancelled. | Yes |
| `status` | `JVStatus` | Reports current download status. | Yes |
| `skip` | `JVSkip` | Skips current file in session. | Yes |
| `cancel` | `JVCancel` | Cancels current session. | Yes |
| `delete-file --name <filename>` | `JVFiledelete` | Deletes an existing file and reports success. | Yes |
| `watch-events [--duration <sec>] [--open-after]` | `JVWatchEvent`, `JVWatchEventClose` | Subscribes to watch events for specified duration; optionally opens realtime session on event trigger. | Partial (events require stub triggers) |
| `course-file --key <id>` | `JVCourseFile` | Retrieves course diagram path. | Yes |
| `course-file2 --key <id>` | `JVCourseFile2` | Retrieves course diagram path (v2). | Yes |
| `silks-file --pattern <text> --output <path>` | `JVFukuFile` | Generates a bitmap file for the supplied pattern. | Yes |
| `silks-binary --pattern <text>` | `JVFuku` | Returns silks image bytes (hex encoded). | Yes |
| `movie-check --key <search>` | `JVMVCheck` | Validates movie availability. | Yes |
| `movie-check-with-type --movie-type <code> --key <search>` | `JVMVCheckWithType` | Validates movie availability by type. | Yes |
| `movie-play --key <search>` | `JVMVPlay` | Requests movie playback. | Stub: simulated |
| `movie-play-with-type --movie-type <code> --key <search>` | `JVMVPlayWithType` | Requests movie playback by type. | Stub: simulated |
| `movie-open --movie-type <code> --search-key <key>` | `JVMVOpen`, `JVMVRead` | Retrieves all workout video listings in one call via `FetchWorkoutVideos`. | Yes |
| `version` | `JVLink.Version` | Displays JV-Link version information. | Yes |
| `set-save-flag --value <bool>` | `JVSetSaveFlag` | Sets save flag. | Yes |
| `get-save-flag` | `JVGetSaveFlag` | Gets current save flag. | Yes |
| `set-save-path --value <path>` | `JVSetSavePath` | Sets save path directory. | Yes |
| `get-save-path` | `JVGetSavePath` | Gets current save path. | Yes |
| `set-service-key --value <key>` | `JVSetServiceKey` | Sets service key. | Yes |
| `get-service-key` | `JVGetServiceKey` | Gets current service key. | Yes |
| `capture-fixtures --output <dir> --specs <list> --from <ts> [--to <ts>] [--max-records <n>] [--use-jvgets]` | `JVOpen`, `JVRead`/`JVGets` | Captures real COM records as test fixtures (Windows only). | No (requires COM) |

## Execution Flow

1. **Global Options**: `--sid`, `--service-key`, `--save-path`, `--stub`, `--diag`, `--help` are parsed first.
2. **Initialisation**: SID is validated; JV-Link is initialised via `JVInit`. Errors abort the run.
3. **Scenario Commands**: Each command translates to a specific `JvLinkService` call. The CLI ensures that fatal errors are returned as process exit codes.
4. **Stub Mode**: When `--stub` is passed, the CLI injects `JvLinkStub` with scripted responses so the same scenarios run without COM.
5. **Exit Codes**: `0` success; non-zero for errors.

## Future Extensions

- Add `error --code <value>` to dump ErrorCatalog entries for troubleshooting.
- Provide `--script <file>` to run multiple commands sequentially for nightly E2E suites.
- Emit `*.jsonl` logs for ingestion into dashboards comparing COM vs. stub behaviour.

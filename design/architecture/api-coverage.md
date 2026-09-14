# JV-Link 5.0 API coverage

The public `Xanthos.JvLink` module projects all 26 SDK methods and nine properties as curried functions. All seven event origins and 38 official record layouts are represented. This mapping describes implementation and deterministic contract coverage; it does not certify every live service or interactive scenario. The SDK 5.0 distribution includes interface/data specifications labeled 4.9.0.1.

## Methods

| SDK method | Public function (`Session` last) |
|---|---|
| `JVInit` | `JvLink.init` |
| `JVSetUIProperties` | `JvLink.configureUi` |
| `JVSetServiceKey` | `JvLink.setServiceKey` |
| `JVSetSaveFlag` | `JvLink.setSaveFlag` |
| `JVSetSavePath` | `JvLink.setSavePath` |
| `JVOpen` | `JvLink.openData` |
| `JVRTOpen` | `JvLink.openRealtime` |
| `JVStatus` | `JvLink.status` |
| `JVRead` | `JvLink.read` |
| `JVGets` | `JvLink.gets` |
| `JVSkip` | `JvLink.skip` |
| `JVCancel` | `JvLink.cancel` |
| `JVClose` | `JvLink.closeData` |
| `JVFiledelete` | `JvLink.deleteFile` |
| `JVFukuFile` | `JvLink.silksFile` |
| `JVFuku` | `JvLink.silksBinary` |
| `JVMVCheck` | `JvLink.movieCheck` |
| `JVMVCheckWithType` | `JvLink.movieCheckWithType` |
| `JVMVPlay` | `JvLink.moviePlay` |
| `JVMVPlayWithType` | `JvLink.moviePlayWithType` |
| `JVMVOpen` | `JvLink.movieOpen` |
| `JVMVRead` | `JvLink.movieRead` |
| `JVCourseFile` | `JvLink.courseFile` |
| `JVCourseFile2` | `JvLink.courseFile2` |
| `JVWatchEvent` | `JvLink.watchEvent` |
| `JVWatchEventClose` | `JvLink.watchEventClose` |

## Properties

| SDK property | Public function | Access |
|---|---|---|
| `m_saveflag` | `JvLink.getSaveFlag` | read-only |
| `m_savepath` | `JvLink.getSavePath` | read-only |
| `m_servicekey` | `JvLink.getServiceKey` | read-only |
| `m_JVLinkVersion` | `JvLink.getVersion` | read-only |
| `m_TotalReadFilesize` | `JvLink.getTotalReadFileSize` | read-only |
| `m_CurrentReadFilesize` | `JvLink.getCurrentReadFileSize` | read-only |
| `m_CurrentFileTimestamp` | `JvLink.getCurrentFileTimestamp` | read-only |
| `ParentHWnd` | `JvLink.setParentWindowHandle` | write-only; signed 32-bit COM Long |
| `m_payflag` | `JvLink.getPayFlag` | read-only |

Configuration changes use the corresponding SDK setter methods. `m_payflag` has no setter; use `configureUi`. Size units are retained: `getTotalReadFileSize` returns `FileSizeKilobytes` with an explicit `.Bytes` conversion.

## Events and records

`subscribe` / `subscribeWithOptions` own delivery queues and worker lifetimes. `JvEvent.Kind` distinguishes Pay, Weight, JockeyChange, Weather, CourseChange, Avoid and TimeChange while preserving `RawKey`. `parseEvent` and `toRealtimeRequest` retain the notification origin. See [event keys and delivery](../../docs/functional-events.md).

`Records.parse` dispatches all 38 official types; `parseWith` selects historical identifier/odds layouts. Unknown IDs and codes remain explicit. The 1,270-field independent inventory includes repeated fields and reserved spans; reserved spans are not represented as invented domain fields. See [record migration](../../docs/record-migration.md) and [contract fixtures](../../tests/Xanthos.UnitTests/Contracts/README.md).

## Evidence and remaining verification

The required managed suites cover native arguments, by-reference outputs, documented/unknown return codes, ownership, event origins and official data layouts. The final acceptance CI passed Fast (2,809 cases) and Coverage (2,749 cases) on all three operating systems, plus 26 SDK-free Windows cases, with no required skips. See [the accepted CI run](https://github.com/cariandrum22/Xanthos/actions/runs/34806298174). The x64 CLI uses public functions in COM mode; its explicit Stub suite is separate from real COM evidence.

Real x64 acquisition, both readers, same-session cancellation/reopen, subscription cleanup and recovery after consumer exceptions have succeeded. The full COM gate passed all 15 tests, including images and No Image. Interactive settings verification covered normal and exception recovery, restoring the original settings and confirming existing cache/data integrity. Playback was confirmed after pressing Play in the viewer. See [the accepted verification summary](https://github.com/cariandrum22/Xanthos/pull/31). Intermittent native SDK `-413` failures remain a known limitation; a successful run does not establish their resolution.

A real Weight notification completed unchanged-key retrieval, official parsing and cleanup through the x64 CLI. Live delivery of every notification origin has not been verified; deterministic event tests establish the seven-origin contract, not live service completeness. Historical record layouts use specification-based fixtures and have not been verified against real legacy captures. See [COM verification](../../tests/Xanthos.ComTests/README.md) and [record migration](../../docs/record-migration.md).

## Updating the contract

When an SDK revision changes the interface, update the specification inventories, public signatures, compiled examples and independent fixtures together. Confirm category discovery counts and fail on skipped required cases. Keep licensed SDK documents, captures and machine-specific evidence outside Git. Update this mapping and the changelog for breaking behavior changes.

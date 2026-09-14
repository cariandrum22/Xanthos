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

As of 2026-09-13, the Contract category has 690 passing cases with no skips. These cover native arguments, by-reference outputs, documented/unknown return codes, ownership, event origins and official data layouts. The x64 CLI uses public functions in COM mode; its explicit Stub suite is separate from real COM evidence.

Real x64 acquisition, both readers, same-session cancellation/reopen, subscription cleanup and recovery after consumer exceptions have succeeded. The full COM gate now passes all 15 tests, including images and No Image. An earlier SDK -413 did not recur in this run; its root cause remains unknown. The user confirmed video playback after pressing Play and closed the settings UI with Cancel; a fresh connection verified unchanged settings.

A real Weight notification completed unchanged-key retrieval, official parsing and cleanup through the x64 CLI. Collection of all seven actual notification origins remains pending in a separate 24-hour background probe; deterministic event tests do not establish live delivery. See [COM verification](../../tests/Xanthos.ComTests/README.md). Earlier 13-test image-excluding runs remain subset results.

## Updating the contract

When an SDK revision changes the interface, update the specification inventories, public signatures, compiled examples and independent fixtures together. Confirm category discovery counts and fail on skipped required cases. Keep licensed SDK documents, captures and machine-specific evidence outside Git. Update this mapping and the changelog for breaking behavior changes.

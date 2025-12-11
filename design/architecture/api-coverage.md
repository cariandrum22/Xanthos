# JV-Link API Coverage Matrix

Status legend: ✅ Implemented / ⚠️ Partial (workaround or property-based) / ❌ Not yet implemented

## Public Methods

| Method | Status | Use Case | Notes |
|--------|--------|----------|-------|
| JVInit | ✅ | Session start | Wrapped via `IJvLinkClient.Init` |
| JVSetUIProperties | ✅ | UI configuration | `JvLinkService.ShowConfigurationDialog` |
| JVSetServiceKey | ✅ | Auth setup | `JvLinkService.SetServiceKey` (calls `JVSetServiceKey`) |
| JVSetSaveFlag | ✅ | Cache control | `JvLinkService.SetSaveDownloadsEnabled` |
| JVSetSavePath | ✅ | Cache path | `JvLinkService.SetSavePath` (calls `JVSetSavePath`) |
| JVOpen | ✅ | Batch download | Implemented; returns file count |
| JVRTOpen | ✅ | Real-time odds | `StreamRealtimePayloads`, `StreamRealtimeAsync` (alias `StreamRealtimePayloadsAsync`) |
| JVStatus | ✅ | Progress check | Implemented via `IJvLinkClient.Status`; returns download % |
| JVRead | ✅ | Data retrieval | Implemented with Shift-JIS handling |
| JVGets | ✅ | Line read | SAFEARRAY byte extraction + Shift-JIS decode; avoids JV-Link internal Unicode conversion |
| JVSkip | ✅ | Skip file | Implemented via `IJvLinkClient.Skip` |
| JVCancel | ✅ | Abort download | Implemented via `IJvLinkClient.Cancel` |
| JVClose | ✅ | Session end | Implemented |
| JVFiledelete | ✅ | Cache cleanup | Implemented via `IJvLinkClient.DeleteFile` |
| JVFukuFile | ✅ | Image path | `JvLinkService.GenerateSilksFile` wraps `JVFukuFile` |
| JVFuku | ✅ | Image data | `JvLinkService.GetSilksBinary` wraps `JVFuku` |
| JVMVCheck | ✅ | Video available | `JvLinkService.CheckMovieAvailability` |
| JVMVCheckWithType | ✅ | Video type check | `JvLinkService.CheckMovieAvailability` overload |
| JVMVPlay | ✅ | Play video | `JvLinkService.PlayMovie` |
| JVMVPlayWithType | ✅ | Play specific | `JvLinkService.PlayMovie` overload |
| JVMVOpen | ✅ | Video stream | `JvLinkService.FetchWorkoutVideos` |
| JVMVRead | ✅ | Video read | `IJvLinkClient.MovieRead` + `WorkoutVideoListing` |
| JVCourseFile | ✅ | Course layout | Implemented via `IJvLinkClient.CourseFile` |
| JVCourseFile2 | ✅ | Course detail | Implemented via `IJvLinkClient.CourseFile2` |
| JVWatchEvent | ✅ | Live events | `JvLinkService.StartWatchEvents`; COM callbacks fan into `WatchEvents` observable. IID/DISPIDs verified from JVDTLabLib. |
| JVWatchEventClose | ✅ | Stop events | `JvLinkService.StopWatchEvents`; disposes callbacks. |

## API Details

This section provides expected return values, error codes, and specification references to guide implementation.

### Event APIs

| Method | Returns | Key Error Codes | Spec Reference |
|--------|---------|-----------------|----------------|
| JVWatchEvent | 0: success, -1: failure | -201 (invalid key), -403 (auth expired) | [events.md](../specs/events.md) |
| JVWatchEventClose | 0: success | N/A | [events.md](../specs/events.md) |

**Implementation notes**: Event callbacks receive raw keys (e.g., `0B1220240101010101`). `JvLinkService.StartWatchEvents` registers the COM callback via `ComEventsHelper.Combine`, normalizes payloads through `Serialization.parseWatchEvent`, and publishes results via the `WatchEvents` observable. Errors surface as `Result<WatchEvent, XanthosError>` entries. `StopWatchEvents` closes the COM watch thread.

**Verified**: IID `17E1E656-828B-4849-B043-FA62B92D9E41` and DISPIDs extracted from JVDTLabLib type library via OleView on Windows.

### Media APIs

| Method | Returns | Key Error Codes | Spec Reference |
|--------|---------|-----------------|----------------|
| JVFukuFile | File path string | -1 (not found), -201 (invalid params) | [methods.md](../specs/methods.md#jvfukufile) |
| JVFuku | Byte array | -1 (not found), -502 (download error) | [methods.md](../specs/methods.md#jvfuku) |
| JVMVCheck | 0: available, -1: unavailable | -403 (auth), -504 (maintenance) | [methods.md](../specs/methods.md#jvmvcheck) |
| JVMVCheckWithType | 0: available, -1: unavailable | -403 (auth), -504 (maintenance) | [methods.md](../specs/methods.md#jvmvcheckwithtype) |
| JVMVPlay | 0: success | -1 (not found), -502 (error) | [methods.md](../specs/methods.md#jvmvplay) |
| JVMVPlayWithType | 0: success | -1 (not found), -502 (error) | [methods.md](../specs/methods.md#jvmvplaywithtype) |
| JVMVOpen | Handle or 0 | -1 (not found), -403 (auth) | [methods.md](../specs/methods.md#jvmvopen) |
| JVMVRead | Bytes read | 0 (EOF), -1 (error) | [methods.md](../specs/methods.md#jvmvread) |

**Implementation notes**: Media APIs are exposed via `JvLinkService` (`GenerateSilksFile`, `GetSilksBinary`, `FetchWorkoutVideos`, `PlayMovie`, `CheckMovieAvailability`). `WorkoutVideoListing` normalises `JVMVRead` responses; further enhancements may add richer metadata parsing if specifications expand.

### Auxiliary Data APIs

| Method | Returns | Key Error Codes | Spec Reference |
|--------|---------|-----------------|----------------|
| JVCourseFile | File path + explanation | -1 (not found) | [methods.md](../specs/methods.md#jvcoursefile) |
| JVCourseFile2 | File path string | -1 (not found) | [methods.md](../specs/methods.md#jvcoursefile2) |

**Implementation notes**: Implemented in `JvLinkService.GetCourseDiagram` / `GetCourseDiagramBasic`.

## Exposed Properties

| Property | Status | Notes |
|----------|--------|-------|
| m_saveflag | ✅ | `JvLinkService.SetSaveDownloadsEnabled` / `GetSaveDownloadsEnabled` |
| m_savepath | ✅ | `JvLinkService.SetSavePath` / `GetSavePath` |
| m_servicekey | ✅ | `JvLinkService.SetServiceKey` / `GetServiceKey` |
| m_JVLinkVersion | ✅ | `JvLinkService.GetJVLinkVersion` |
| m_TotalReadFilesize | ✅ | `JvLinkService.GetTotalReadFileSize` |
| m_CurrentReadFilesize | ✅ | `JvLinkService.GetCurrentReadFileSize` |
| m_CurrentFileTimestamp | ✅ | `JvLinkService.GetCurrentFileTimestamp` |
| ParentHWnd | ✅ | `JvLinkService.SetParentWindowHandle` / `GetParentWindowHandle` (write-only in COM; read fails) |
| m_payflag | ✅ | `JvLinkService.SetPayoffDialogSuppressed` / `GetPayoffDialogSuppressed` (read-only in COM; write fails) |

## Update Guidelines

### When to Update This Matrix

Update this matrix whenever:
- A new method or property wrapper is implemented
- An existing implementation changes status (e.g., from ⚠️ to ✅)
- New tests are added for an API
- Documentation is completed for an API

### Checklist for Marking as ✅ Implemented

Before changing status to ✅, ensure:

- [ ] Core implementation complete in appropriate layer
- [ ] Unit tests written and passing
- [ ] Property-based tests (where applicable)
- [ ] Error handling follows `ComError` → `XanthosError` pattern
- [ ] XML documentation comments added
- [ ] Integration with `JvLinkStub` for testing

### Status Transition Rules

| From | To | Requirements |
|------|-----|-------------|
| ❌ | ⚠️ | Basic wrapper exists, may lack tests or full error handling |
| ⚠️ | ✅ | Full implementation with tests and documentation |
| ✅ | ⚠️ | Regression or incomplete refactoring (add note explaining) |

### Adding New APIs

When JV-Link specification updates add new methods/properties:

1. Add row to appropriate table with status ❌
2. Add note indicating specification version
3. Create tracking issue if using issue tracker
4. Update `design/specs` markdown with new API details

### Progress Tracking

| Category | Total | ✅ | ⚠️ | ❌ | Coverage |
|----------|-------|-----|-----|-----|----------|
| Methods | 26 | 26 | 0 | 0 | 100% |
| Properties | 9 | 9 | 0 | 0 | 100% |
| **Overall** | **35** | **35** | **0** | **0** | **100%** |

---

## Next Steps

1. Expand soak/integration testing for event-heavy scenarios (multiple concurrent `WatchEvents` subscribers, reconnection loops).
2. Update this matrix as each group ships (including tests and documentation links).

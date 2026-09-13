# SDK known limitations

## SDK-COM-413: intermittent native communication failure

**Disposition (2026-09-13): accepted known limitation; investigation closed for the SDK implementation and test-quality GAP plans by user decision.** This is not a root-cause fix or confirmation of a JRA-VAN server defect. Further network diagnosis is not a prerequisite for completing those plans.

### Observed behavior

JV-Link 5.0 x64 (`m_JVLinkVersion = 0500`, native DLL file version `1.1.8.0`) intermittently returned `-413` from `JVOpen` and `JVCourseFile`. Independent minimal C# programs reproduced the error without Xanthos, including a typed .NET Framework COM client after the notification collector had stopped. A wrapper-specific cause was not identified.

- On the fixed connection, some failures followed an inbound TCP reset. Native tracing showed WinINet error 12031, followed by SDK handle cleanup before `GetLastError`; the SDK then observed zero and returned `-413`.
- On the cellular connection, the additional 1,000-attempt course-file run stopped at attempt 643: 642 successes, one failure, 357 not run. The failed request received no data or acknowledgement of its request bytes; no inbound reset was observed. The reset/drop location and this attempt's WinINet error remain unknown.
- A separate formal cellular COM run passed all 15 cases with no skips. That result and the subsequent repeated-test failure remain separate evidence.

`-413` alone does not establish an actual HTTP 403 response, TCP reset, Wi-Fi loss, or a particular WinINet error. Catalog descriptions are SDK mappings, not packet observations. Xanthos cannot recover a native error already lost inside the SDK.

### Caller contract

The functional API returns `Error` with `Kind = JvErrorKind.Sdk`, the original `Api` and `Code = Some -413`; retain meaningful `Outputs` where supplied. Handle the failure explicitly. Do not consume failed outputs as successful data, replace failure with an empty result, or silently switch to the stub.

Record the API, code, time and SDK version without logging service keys. This code does not promise retryability. Any caller-directed retry needs a bounded policy, operation-specific safety and valid session/resource state. This disposition adds no automatic retry or error reclassification.

### GAP acceptance and reopening

The closure requires this API limitation, implementation comments preserving numeric errors, and matching local GAP/handoff status. Existing failed and unexecuted results remain unchanged; required regression checks and other DoDs still apply. Do not globally ignore `-413` in tests or rerun until a failure disappears. If a later required run encounters it, record that run as failed and reference this limitation without automatically reopening network diagnosis.

Reopen investigation when new evidence identifies a Xanthos contract/ownership violation, a relevant SDK fix becomes available for verification, or the user explicitly requests further diagnosis. Raw diagnostics, local GAP plans and SDK documents remain Git-excluded.

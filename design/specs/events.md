# JV-Link Event Callbacks

Extracted from the `JVWatchEvent` section of `JV-Link4901`.

## ✅ Implementation Status: VERIFIED

The COM event infrastructure is fully implemented in `src/Xanthos/Interop/ComEvents.fs`.

### Verified Values

Extracted from JVDTLabLib type library via OleView on Windows.
Last verified: 2025 (JV-Link version 4.x)

| Item | Value |
|------|-------|
| **IID** | `17E1E656-828B-4849-B043-FA62B92D9E41` |
| JVEvtPay | DISPID 1 |
| JVEvtJockeyChange | DISPID 2 |
| JVEvtWeather | DISPID 3 |
| JVEvtCourseChange | DISPID 4 |
| JVEvtAvoid | DISPID 5 |
| JVEvtTimeChange | DISPID 6 |
| JVEvtWeight | DISPID 7 |

### Re-verification Procedure

If JV-Link is updated and events stop working, re-verify the IID/DISPIDs:

1. **OleView (recommended)**:
   - Open OleView.exe (Windows SDK)
   - Navigate to: Type Libraries → JVDTLabLib
   - Find `_IJVLinkEvents` dispinterface
   - IID: `[uuid(xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx)]`
   - DISPIDs: `[id(N)]` on each method

2. **Runtime verification**:
   - If `StartWatchEvents()` returns `Ok` → IID is correct
   - If it fails with `E_NOINTERFACE` → re-verify IID
   - If events don't fire → re-verify DISPIDs

Update values in `src/Xanthos/Interop/ComEvents.fs` if needed.

### Components

| Component | Location | Status |
|-----------|----------|--------|
| `JvLinkEventSink` | `src/Xanthos/Interop/ComEvents.fs` | ✅ Implemented |
| `ComEventConnection` | `src/Xanthos/Interop/ComEvents.fs` | ✅ Verified IID/DISPIDs |
| `JvLinkService.StartWatchEvents` | `src/Xanthos/Runtime/JvLinkService.fs` | ✅ Implemented |
| `WatchEvent` parsing | `src/Xanthos/Core/Serialization.fs` | ✅ Implemented |

### Usage

```fsharp
// Create client using tryCreate (returns Result)
match ComClientFactory.tryCreate None with
| Ok client ->
    let service = JvLinkService(client, config)

    // Subscribe to events
    use subscription = service.WatchEvents.Subscribe(fun result ->
        match result with
        | Ok event -> printfn "Event: %A" event.Event
        | Error err -> printfn "Error: %A" err
    )

    // Start watching
    match service.StartWatchEvents() with
    | Ok () -> printfn "Watching for events..."
    | Error e -> printfn "Failed: %A" e

    // ... later ...
    service.StopWatchEvents() |> ignore

| Error err -> printfn "Failed to create client: %A" err
```

### Notes

- Requires Windows with JV-Link COM component installed
- Events are push-only via COM connection points (no polling fallback)

## Event Types
| 種類 | イベントメソッド名 | 説明 |
| --- | --- | --- |
| 払戻確定 | JVEvtPay | 払戻確定が発表された際イベントを受理します。 |
| 騎手変更 | JVEvtJockeyChange | 騎手変更が発表された際イベントを受理します。 |
| 天候馬場状態変更 | JVEvtWeather | 天候馬場状態変更が発表された際イベントを受理します。 |
| コース変更 | JVEvtCourseChange | コース変更が発表された際イベントを受理します。 |
| 出走取消・競走除外 | JVEvtAvoid | 出走取消・競走除外が発表された際イベントを受理します。 |
| 発走時刻変更 | JVEvtTimeChange | 発走時刻変更が発表された際イベントを受理します。 |
| 馬体重発表 | JVEvtWeight | 馬体重が発表された際イベントを受理します。 |

## Callback Parameters
| イベントメソッド名 | パラメータ | 説明 |
| --- | --- | --- |
| JVEvtPay | “YYYYMMDDJJRR” | YYYY:開催年 |
| JVEvtWeight |  | MM  :開催月 DD  :開催日 JJ  :場コード RR  :レース番号 |
| JVEvtJockeyChange | “TTYYYYMMDDJJRRNNNNNNNNNNNNNN” | TT  :レコード種別 ID |
| JVEvtWeather |  | YYYY:開催年 |
| JVEvtCourseChange |  | MM  :開催月 DD  :開催日 |
| JVEvtAvoid | JJ  :場コード RR  :レース番号 |  |
| JVEvtTimeChange |  | NNNNNNNNNNNNNN:送信年月日 |

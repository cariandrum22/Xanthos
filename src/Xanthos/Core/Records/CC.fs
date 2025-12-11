namespace Xanthos.Core.Records

open System
open Xanthos.Core
open Xanthos.Core.Records.FieldDefinitions
open Xanthos.Core.Records.RecordParser
open Xanthos.Core.Records.CodeTables

/// CC Record: コース変更 (Course Change)
module CC =

    /// CC Record data
    type CCRecord =
        { RaceKey: string // レースキー
          OldTrackSurface: TrackSurfaceCode option // 変更前トラック種別
          NewTrackSurface: TrackSurfaceCode option // 変更後トラック種別
          OldDistance: int option // 変更前距離
          NewDistance: int option // 変更後距離
          UpdateTime: DateTime option } // 更新時刻

    /// CC record field specifications
    let private fieldSpecs =
        [ textRaw "RecordType" 0 2 // レコード種別
          text "RaceKey" 2 16 // レースキー
          code "OldTrackSurface" 18 1 "TrackSurfaceCode" // 変更前トラック種別
          code "NewTrackSurface" 19 1 "TrackSurfaceCode" // 変更後トラック種別
          int "OldDistance" 20 4 // 変更前距離
          int "NewDistance" 24 4 // 変更後距離
          date "UpdateTime" 28 12 "yyyyMMddHHmm" ] // 更新時刻

    /// Parse CC record from raw bytes
    let parse (data: byte[]) : Result<CCRecord, XanthosError> =
        parseRecord {
            let! fields = parseFieldsXanthos data fieldSpecs
            let! raceKey = requireText fields "RaceKey"

            return
                { RaceKey = raceKey
                  OldTrackSurface = getCode<TrackSurfaceCode> fields "OldTrackSurface"
                  NewTrackSurface = getCode<TrackSurfaceCode> fields "NewTrackSurface"
                  OldDistance = getInt fields "OldDistance"
                  NewDistance = getInt fields "NewDistance"
                  UpdateTime = getDate fields "UpdateTime" }
        }

namespace Xanthos.Core.Records

open System
open Xanthos.Core
open Xanthos.Core.Records.FieldDefinitions
open Xanthos.Core.Records.RecordParser
open Xanthos.Core.Records.CodeTables

/// AV Record: 馬場状態変更 (Track Condition Change)
module AV =

    /// AV Record data
    type AVRecord =
        { RaceKey: string // レースキー
          OldTrackCondition: TrackConditionCode option // 変更前馬場状態
          NewTrackCondition: TrackConditionCode option // 変更後馬場状態
          UpdateTime: DateTime option } // 更新時刻

    /// AV record field specifications
    let private fieldSpecs =
        [ textRaw "RecordType" 0 2 // レコード種別
          text "RaceKey" 2 16 // レースキー
          code "OldTrackCondition" 18 1 "TrackConditionCode" // 変更前馬場状態
          code "NewTrackCondition" 19 1 "TrackConditionCode" // 変更後馬場状態
          date "UpdateTime" 20 12 "yyyyMMddHHmm" ] // 更新時刻

    /// Parse AV record from raw bytes
    let parse (data: byte[]) : Result<AVRecord, XanthosError> =
        parseRecord {
            let! fields = parseFieldsXanthos data fieldSpecs
            let! raceKey = requireText fields "RaceKey"

            return
                { RaceKey = raceKey
                  OldTrackCondition = getCode<TrackConditionCode> fields "OldTrackCondition"
                  NewTrackCondition = getCode<TrackConditionCode> fields "NewTrackCondition"
                  UpdateTime = getDate fields "UpdateTime" }
        }

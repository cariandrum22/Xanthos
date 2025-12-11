namespace Xanthos.Core.Records

open System
open Xanthos.Core
open Xanthos.Core.Records.FieldDefinitions
open Xanthos.Core.Records.RecordParser

/// TC Record: タイム型調教 (Training Time)
module TC =

    /// TC Record data
    type TCRecord =
        { RaceKey: string // レースキー
          HorseNumber: int option // 馬番
          TrainingType: string option // 調教タイプ
          TrainingTime: string option // 調教タイム
          UpdateTime: DateTime option } // 更新時刻

    /// TC record field specifications
    let private fieldSpecs =
        [ textRaw "RecordType" 0 2 // レコード種別
          text "RaceKey" 2 16 // レースキー
          int "HorseNumber" 18 2 // 馬番
          text "TrainingType" 20 10 // 調教タイプ
          text "TrainingTime" 30 8 // 調教タイム
          date "UpdateTime" 38 12 "yyyyMMddHHmm" ] // 更新時刻

    /// Parse TC record from raw bytes
    let parse (data: byte[]) : Result<TCRecord, XanthosError> =
        parseRecord {
            let! fields = parseFieldsXanthos data fieldSpecs
            let! raceKey = requireText fields "RaceKey"

            return
                { RaceKey = raceKey
                  HorseNumber = getInt fields "HorseNumber"
                  TrainingType = getText fields "TrainingType"
                  TrainingTime = getText fields "TrainingTime"
                  UpdateTime = getDate fields "UpdateTime" }
        }

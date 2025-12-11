namespace Xanthos.Core.Records

open System
open Xanthos.Core
open Xanthos.Core.Records.FieldDefinitions
open Xanthos.Core.Records.RecordParser

/// WF Record: 馬体重 (Horse Weight)
module WF =

    /// WF Record data
    type WFRecord =
        { RaceKey: string // レースキー
          HorseNumber: int option // 馬番
          Weight: int option // 馬体重
          WeightDiff: int option // 馬体重増減
          UpdateTime: DateTime option } // 更新時刻

    /// WF record field specifications
    let private fieldSpecs =
        [ textRaw "RecordType" 0 2 // レコード種別
          text "RaceKey" 2 16 // レースキー
          int "HorseNumber" 18 2 // 馬番
          int "Weight" 20 3 // 馬体重
          int "WeightDiff" 23 3 // 馬体重増減
          date "UpdateTime" 26 12 "yyyyMMddHHmm" ] // 更新時刻

    /// Parse WF record from raw bytes
    let parse (data: byte[]) : Result<WFRecord, XanthosError> =
        parseRecord {
            let! fields = parseFieldsXanthos data fieldSpecs
            let! raceKey = requireText fields "RaceKey"

            return
                { RaceKey = raceKey
                  HorseNumber = getInt fields "HorseNumber"
                  Weight = getInt fields "Weight"
                  WeightDiff = getInt fields "WeightDiff"
                  UpdateTime = getDate fields "UpdateTime" }
        }

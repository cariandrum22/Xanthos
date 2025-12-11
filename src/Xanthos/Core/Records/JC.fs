namespace Xanthos.Core.Records

open System
open Xanthos.Core
open Xanthos.Core.Records.FieldDefinitions
open Xanthos.Core.Records.RecordParser
open Xanthos.Core.Records.CodeTables

/// JC Record: 騎手変更 (Jockey Change)
module JC =

    /// JC Record data
    type JCRecord =
        { RaceKey: string // レースキー
          HorseNumber: int option // 馬番
          OldJockeyName: string option // 変更前騎手名
          NewJockeyName: string option // 変更後騎手名
          ChangeReason: string option // 変更理由
          UpdateTime: DateTime option } // 更新時刻

    /// JC record field specifications
    let private fieldSpecs =
        [ textRaw "RecordType" 0 2 // レコード種別
          text "RaceKey" 2 16 // レースキー
          int "HorseNumber" 18 2 // 馬番
          text "OldJockeyName" 20 34 // 変更前騎手名
          text "NewJockeyName" 54 34 // 変更後騎手名
          text "ChangeReason" 88 40 // 変更理由
          date "UpdateTime" 128 12 "yyyyMMddHHmm" ] // 更新時刻

    /// Parse JC record from raw bytes
    let parse (data: byte[]) : Result<JCRecord, XanthosError> =
        parseRecord {
            let! fields = parseFieldsXanthos data fieldSpecs
            let! raceKey = requireText fields "RaceKey"

            return
                { RaceKey = raceKey
                  HorseNumber = getInt fields "HorseNumber"
                  OldJockeyName = getText fields "OldJockeyName"
                  NewJockeyName = getText fields "NewJockeyName"
                  ChangeReason = getText fields "ChangeReason"
                  UpdateTime = getDate fields "UpdateTime" }
        }

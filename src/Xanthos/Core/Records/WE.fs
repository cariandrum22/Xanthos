namespace Xanthos.Core.Records

open System
open Xanthos.Core
open Xanthos.Core.Records.FieldDefinitions
open Xanthos.Core.Records.RecordParser

/// WE Record: 天候変更 (Weather Change)
module WE =

    /// WE Record data
    type WERecord =
        { RaceKey: string // レースキー
          OldWeather: string option // 変更前天候
          NewWeather: string option // 変更後天候
          UpdateTime: DateTime option } // 更新時刻

    /// WE record field specifications
    let private fieldSpecs =
        [ textRaw "RecordType" 0 2 // レコード種別
          text "RaceKey" 2 16 // レースキー
          text "OldWeather" 18 1 // 変更前天候
          text "NewWeather" 19 1 // 変更後天候
          date "UpdateTime" 20 12 "yyyyMMddHHmm" ] // 更新時刻

    /// Parse WE record from raw bytes
    let parse (data: byte[]) : Result<WERecord, XanthosError> =
        parseRecord {
            let! fields = parseFieldsXanthos data fieldSpecs
            let! raceKey = requireText fields "RaceKey"

            return
                { RaceKey = raceKey
                  OldWeather = getText fields "OldWeather"
                  NewWeather = getText fields "NewWeather"
                  UpdateTime = getDate fields "UpdateTime" }
        }

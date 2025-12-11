namespace Xanthos.Core.Records

open System
open Xanthos.Core
open Xanthos.Core.Records.FieldDefinitions
open Xanthos.Core.Records.RecordParser

/// O3 Record: 枠連オッズ (Bracket Quinella Odds)
module O3 =

    /// O3 Record data
    type O3Record =
        { RaceKey: string // レースキー
          BracketNumber1: int option // 枠番1
          BracketNumber2: int option // 枠番2
          Odds: decimal option // 枠連オッズ
          Popularity: int option // 人気順
          UpdateTime: DateTime option } // オッズ更新時刻

    /// O3 record field specifications
    let private fieldSpecs =
        [ textRaw "RecordType" 0 2 // レコード種別
          text "RaceKey" 2 16 // レースキー
          int "BracketNumber1" 18 1 // 枠番1
          int "BracketNumber2" 19 1 // 枠番2
          decimal "Odds" 20 5 1 // 枠連オッズ
          int "Popularity" 25 2 // 人気順
          date "UpdateTime" 27 12 "yyyyMMddHHmm" ] // 更新時刻

    /// Parse O3 record from raw bytes
    let parse (data: byte[]) : Result<O3Record, XanthosError> =
        parseRecord {
            let! fields = parseFieldsXanthos data fieldSpecs
            let! raceKey = requireText fields "RaceKey"

            return
                { RaceKey = raceKey
                  BracketNumber1 = getInt fields "BracketNumber1"
                  BracketNumber2 = getInt fields "BracketNumber2"
                  Odds = getDecimal fields "Odds"
                  Popularity = getInt fields "Popularity"
                  UpdateTime = getDate fields "UpdateTime" }
        }

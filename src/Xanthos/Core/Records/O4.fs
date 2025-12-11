namespace Xanthos.Core.Records

open System
open Xanthos.Core
open Xanthos.Core.Records.FieldDefinitions
open Xanthos.Core.Records.RecordParser

/// O4 Record: 馬連オッズ (Quinella Odds)
module O4 =

    /// O4 Record data
    type O4Record =
        { RaceKey: string // レースキー
          HorseNumber1: int option // 馬番1
          HorseNumber2: int option // 馬番2
          Odds: decimal option // 馬連オッズ
          Popularity: int option // 人気順
          UpdateTime: DateTime option } // オッズ更新時刻

    /// O4 record field specifications
    let private fieldSpecs =
        [ textRaw "RecordType" 0 2 // レコード種別
          text "RaceKey" 2 16 // レースキー
          int "HorseNumber1" 18 2 // 馬番1
          int "HorseNumber2" 20 2 // 馬番2
          decimal "Odds" 22 6 1 // 馬連オッズ
          int "Popularity" 28 3 // 人気順
          date "UpdateTime" 31 12 "yyyyMMddHHmm" ] // 更新時刻

    /// Parse O4 record from raw bytes
    let parse (data: byte[]) : Result<O4Record, XanthosError> =
        parseRecord {
            let! fields = parseFieldsXanthos data fieldSpecs
            let! raceKey = requireText fields "RaceKey"

            return
                { RaceKey = raceKey
                  HorseNumber1 = getInt fields "HorseNumber1"
                  HorseNumber2 = getInt fields "HorseNumber2"
                  Odds = getDecimal fields "Odds"
                  Popularity = getInt fields "Popularity"
                  UpdateTime = getDate fields "UpdateTime" }
        }

namespace Xanthos.Core.Records

open System
open Xanthos.Core
open Xanthos.Core.Records.FieldDefinitions
open Xanthos.Core.Records.RecordParser

/// O6 Record: 馬単オッズ (Exacta Odds)
module O6 =

    /// O6 Record data
    type O6Record =
        { RaceKey: string // レースキー
          HorseNumber1: int option // 1着馬番
          HorseNumber2: int option // 2着馬番
          Odds: decimal option // 馬単オッズ
          Popularity: int option // 人気順
          UpdateTime: DateTime option } // オッズ更新時刻

    /// O6 record field specifications
    let private fieldSpecs =
        [ textRaw "RecordType" 0 2 // レコード種別
          text "RaceKey" 2 16 // レースキー
          int "HorseNumber1" 18 2 // 1着馬番
          int "HorseNumber2" 20 2 // 2着馬番
          decimal "Odds" 22 6 1 // 馬単オッズ
          int "Popularity" 28 3 // 人気順
          date "UpdateTime" 31 12 "yyyyMMddHHmm" ] // 更新時刻

    /// Parse O6 record from raw bytes
    let parse (data: byte[]) : Result<O6Record, XanthosError> =
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

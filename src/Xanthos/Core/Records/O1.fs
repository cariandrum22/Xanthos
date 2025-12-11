namespace Xanthos.Core.Records

open System
open Xanthos.Core
open Xanthos.Core.Records.FieldDefinitions
open Xanthos.Core.Records.RecordParser

/// O1 Record: 単勝オッズ (Win Odds)
module O1 =

    /// O1 Record data
    type O1Record =
        { RaceKey: string // レースキー
          HorseNumber: int option // 馬番
          Odds: decimal option // 単勝オッズ
          Popularity: int option // 人気順
          UpdateTime: DateTime option } // オッズ更新時刻

    /// O1 record field specifications
    let private fieldSpecs =
        [ textRaw "RecordType" 0 2 // レコード種別
          text "RaceKey" 2 16 // レースキー
          int "HorseNumber" 18 2 // 馬番
          decimal "Odds" 20 4 1 // 単勝オッズ
          int "Popularity" 24 2 // 人気順
          date "UpdateTime" 26 12 "yyyyMMddHHmm" ] // 更新時刻

    /// Parse O1 record from raw bytes
    let parse (data: byte[]) : Result<O1Record, XanthosError> =
        parseRecord {
            let! fields = parseFieldsXanthos data fieldSpecs
            let! raceKey = requireText fields "RaceKey"

            return
                { RaceKey = raceKey
                  HorseNumber = getInt fields "HorseNumber"
                  Odds = getDecimal fields "Odds"
                  Popularity = getInt fields "Popularity"
                  UpdateTime = getDate fields "UpdateTime" }
        }

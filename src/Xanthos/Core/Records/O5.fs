namespace Xanthos.Core.Records

open System
open Xanthos.Core
open Xanthos.Core.Records.FieldDefinitions
open Xanthos.Core.Records.RecordParser

/// O5 Record: ワイドオッズ (Wide Odds)
module O5 =

    /// O5 Record data
    type O5Record =
        { RaceKey: string // レースキー
          HorseNumber1: int option // 馬番1
          HorseNumber2: int option // 馬番2
          OddsMin: decimal option // ワイドオッズ下限
          OddsMax: decimal option // ワイドオッズ上限
          Popularity: int option // 人気順
          UpdateTime: DateTime option } // オッズ更新時刻

    /// O5 record field specifications
    let private fieldSpecs =
        [ textRaw "RecordType" 0 2 // レコード種別
          text "RaceKey" 2 16 // レースキー
          int "HorseNumber1" 18 2 // 馬番1
          int "HorseNumber2" 20 2 // 馬番2
          decimal "OddsMin" 22 5 1 // ワイドオッズ下限
          decimal "OddsMax" 27 5 1 // ワイドオッズ上限
          int "Popularity" 32 3 // 人気順
          date "UpdateTime" 35 12 "yyyyMMddHHmm" ] // 更新時刻

    /// Parse O5 record from raw bytes
    let parse (data: byte[]) : Result<O5Record, XanthosError> =
        parseRecord {
            let! fields = parseFieldsXanthos data fieldSpecs
            let! raceKey = requireText fields "RaceKey"

            return
                { RaceKey = raceKey
                  HorseNumber1 = getInt fields "HorseNumber1"
                  HorseNumber2 = getInt fields "HorseNumber2"
                  OddsMin = getDecimal fields "OddsMin"
                  OddsMax = getDecimal fields "OddsMax"
                  Popularity = getInt fields "Popularity"
                  UpdateTime = getDate fields "UpdateTime" }
        }

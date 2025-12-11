namespace Xanthos.Core.Records

open System
open Xanthos.Core
open Xanthos.Core.Records.FieldDefinitions
open Xanthos.Core.Records.RecordParser

/// O2 Record: 複勝オッズ (Place Odds)
module O2 =

    /// O2 Record data
    type O2Record =
        { RaceKey: string // レースキー
          HorseNumber: int option // 馬番
          OddsMin: decimal option // 複勝オッズ下限
          OddsMax: decimal option // 複勝オッズ上限
          Popularity: int option // 人気順
          UpdateTime: DateTime option } // オッズ更新時刻

    /// O2 record field specifications
    let private fieldSpecs =
        [ textRaw "RecordType" 0 2 // レコード種別
          text "RaceKey" 2 16 // レースキー
          int "HorseNumber" 18 2 // 馬番
          decimal "OddsMin" 20 4 1 // 複勝オッズ下限
          decimal "OddsMax" 24 4 1 // 複勝オッズ上限
          int "Popularity" 28 2 // 人気順
          date "UpdateTime" 30 12 "yyyyMMddHHmm" ] // 更新時刻

    /// Parse O2 record from raw bytes
    let parse (data: byte[]) : Result<O2Record, XanthosError> =
        parseRecord {
            let! fields = parseFieldsXanthos data fieldSpecs
            let! raceKey = requireText fields "RaceKey"

            return
                { RaceKey = raceKey
                  HorseNumber = getInt fields "HorseNumber"
                  OddsMin = getDecimal fields "OddsMin"
                  OddsMax = getDecimal fields "OddsMax"
                  Popularity = getInt fields "Popularity"
                  UpdateTime = getDate fields "UpdateTime" }
        }

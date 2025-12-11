namespace Xanthos.Core.Records

open System
open Xanthos.Core
open Xanthos.Core.Records.FieldDefinitions
open Xanthos.Core.Records.RecordParser
open Xanthos.Core.Records.CodeTables

/// SE Record: 馬毎レース情報 (Runner Race Information)
module SE =

    /// SE Record data
    type SERecord =
        { RaceKey: string // レースキー
          HorseId: string // 馬ID
          HorseName: string // 馬名
          GateNumber: int option // 枠番
          HorseNumber: int option // 馬番
          JockeyName: string option // 騎手名
          JockeyWeight: decimal option // 騎手の負担重量
          Sex: SexCode option // 性別
          Age: int option // 馬齢
          Weight: int option // 馬体重
          WeightDiff: int option // 馬体重増減
          Odds: decimal option // オッズ
          Popularity: int option // 人気
          FinishPosition: int option // 着順
          RunningStyle: RunningStyleCode option // 脚質
          Time: string option // タイム
          TrainerName: string option } // 調教師名

    /// SE record field specifications
    let private fieldSpecs =
        [ textRaw "RecordType" 0 2 // レコード種別
          text "RaceKey" 2 16 // レースキー
          text "HorseId" 18 10 // 馬ID
          text "HorseName" 28 36 // 馬名
          int "GateNumber" 64 1 // 枠番
          int "HorseNumber" 65 2 // 馬番
          text "JockeyName" 67 34 // 騎手名
          decimal "JockeyWeight" 101 3 1 // 騎手負担重量
          code "Sex" 104 1 "SexCode" // 性別
          int "Age" 105 2 // 馬齢
          int "Weight" 107 3 // 馬体重
          int "WeightDiff" 110 3 // 馬体重増減
          decimal "Odds" 113 5 1 // オッズ
          int "Popularity" 118 2 // 人気
          int "FinishPosition" 120 2 // 着順
          code "RunningStyle" 122 1 "RunningStyleCode" // 脚質
          text "Time" 123 7 // タイム
          text "TrainerName" 130 34 ] // 調教師名

    /// Parse SE record from raw bytes
    let parse (data: byte[]) : Result<SERecord, XanthosError> =
        parseRecord {
            let! fields = parseFieldsXanthos data fieldSpecs
            let! raceKey = requireText fields "RaceKey"
            let! horseId = requireText fields "HorseId"
            let! horseName = requireText fields "HorseName"

            return
                { RaceKey = raceKey
                  HorseId = horseId
                  HorseName = horseName
                  GateNumber = getInt fields "GateNumber"
                  HorseNumber = getInt fields "HorseNumber"
                  JockeyName = getText fields "JockeyName"
                  JockeyWeight = getDecimal fields "JockeyWeight"
                  Sex = getCode<SexCode> fields "Sex"
                  Age = getInt fields "Age"
                  Weight = getInt fields "Weight"
                  WeightDiff = getInt fields "WeightDiff"
                  Odds = getDecimal fields "Odds"
                  Popularity = getInt fields "Popularity"
                  FinishPosition = getInt fields "FinishPosition"
                  RunningStyle = getCode<RunningStyleCode> fields "RunningStyle"
                  Time = getText fields "Time"
                  TrainerName = getText fields "TrainerName" }
        }

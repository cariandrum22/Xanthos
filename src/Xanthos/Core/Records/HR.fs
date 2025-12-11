namespace Xanthos.Core.Records

open System
open Xanthos.Core
open Xanthos.Core.Records.FieldDefinitions
open Xanthos.Core.Records.RecordParser

/// HR Record: 払戻 (Payoff)
module HR =

    /// HR Record data
    type HRRecord =
        { RaceKey: string // レースキー
          BetType: int option // 式別 (1=単勝, 2=複勝, 3=枠連, 4=馬連, 5=ワイド, 6=馬単, 7=3連複, 8=3連単)
          HorseNumber1: int option // 馬番1
          HorseNumber2: int option // 馬番2
          HorseNumber3: int option // 馬番3
          Payoff: int option // 払戻金
          Popularity: int option } // 人気順

    /// HR record field specifications
    let private fieldSpecs =
        [ textRaw "RecordType" 0 2 // レコード種別
          text "RaceKey" 2 16 // レースキー
          int "BetType" 18 1 // 式別
          int "HorseNumber1" 19 2 // 馬番1
          int "HorseNumber2" 21 2 // 馬番2
          int "HorseNumber3" 23 2 // 馬番3
          int "Payoff" 25 9 // 払戻金
          int "Popularity" 34 4 ] // 人気順

    /// Parse HR record from raw bytes
    let parse (data: byte[]) : Result<HRRecord, XanthosError> =
        parseRecord {
            let! fields = parseFieldsXanthos data fieldSpecs
            let! raceKey = requireText fields "RaceKey"

            return
                { RaceKey = raceKey
                  BetType = getInt fields "BetType"
                  HorseNumber1 = getInt fields "HorseNumber1"
                  HorseNumber2 = getInt fields "HorseNumber2"
                  HorseNumber3 = getInt fields "HorseNumber3"
                  Payoff = getInt fields "Payoff"
                  Popularity = getInt fields "Popularity" }
        }

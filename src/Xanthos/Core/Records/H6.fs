namespace Xanthos.Core.Records

open System
open Xanthos.Core
open Xanthos.Core.Records.FieldDefinitions
open Xanthos.Core.Records.RecordParser

/// H6 Record: 3連複払戻 (Trio Payoff)
module H6 =

    /// H6 Record data
    type H6Record =
        { RaceKey: string // レースキー
          HorseNumber1: int option // 1着馬番
          HorseNumber2: int option // 2着馬番
          HorseNumber3: int option // 3着馬番
          Payoff: int option // 払戻金
          Popularity: int option } // 人気順

    /// H6 record field specifications
    let private fieldSpecs =
        [ textRaw "RecordType" 0 2 // レコード種別
          text "RaceKey" 2 16 // レースキー
          int "HorseNumber1" 18 2 // 1着馬番
          int "HorseNumber2" 20 2 // 2着馬番
          int "HorseNumber3" 22 2 // 3着馬番
          int "Payoff" 24 8 // 払戻金
          int "Popularity" 32 3 ] // 人気順

    /// Parse H6 record from raw bytes
    let parse (data: byte[]) : Result<H6Record, XanthosError> =
        parseRecord {
            let! fields = parseFieldsXanthos data fieldSpecs
            let! raceKey = requireText fields "RaceKey"

            return
                { RaceKey = raceKey
                  HorseNumber1 = getInt fields "HorseNumber1"
                  HorseNumber2 = getInt fields "HorseNumber2"
                  HorseNumber3 = getInt fields "HorseNumber3"
                  Payoff = getInt fields "Payoff"
                  Popularity = getInt fields "Popularity" }
        }

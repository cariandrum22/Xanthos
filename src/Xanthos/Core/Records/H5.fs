namespace Xanthos.Core.Records

open System
open Xanthos.Core
open Xanthos.Core.Records.FieldDefinitions
open Xanthos.Core.Records.RecordParser

/// H5 Record: 5連複払戻 (Quintet Payoff)
module H5 =

    /// H5 Record data
    type H5Record =
        { RaceKey: string // レースキー
          HorseNumber1: int option // 1着馬番
          HorseNumber2: int option // 2着馬番
          HorseNumber3: int option // 3着馬番
          HorseNumber4: int option // 4着馬番
          HorseNumber5: int option // 5着馬番
          Payoff: int option // 払戻金
          Popularity: int option } // 人気順

    /// H5 record field specifications
    let private fieldSpecs =
        [ textRaw "RecordType" 0 2 // レコード種別
          text "RaceKey" 2 16 // レースキー
          int "HorseNumber1" 18 2 // 1着馬番
          int "HorseNumber2" 20 2 // 2着馬番
          int "HorseNumber3" 22 2 // 3着馬番
          int "HorseNumber4" 24 2 // 4着馬番
          int "HorseNumber5" 26 2 // 5着馬番
          int "Payoff" 28 9 // 払戻金
          int "Popularity" 37 4 ] // 人気順

    /// Parse H5 record from raw bytes
    let parse (data: byte[]) : Result<H5Record, XanthosError> =
        parseRecord {
            let! fields = parseFieldsXanthos data fieldSpecs
            let! raceKey = requireText fields "RaceKey"

            return
                { RaceKey = raceKey
                  HorseNumber1 = getInt fields "HorseNumber1"
                  HorseNumber2 = getInt fields "HorseNumber2"
                  HorseNumber3 = getInt fields "HorseNumber3"
                  HorseNumber4 = getInt fields "HorseNumber4"
                  HorseNumber5 = getInt fields "HorseNumber5"
                  Payoff = getInt fields "Payoff"
                  Popularity = getInt fields "Popularity" }
        }

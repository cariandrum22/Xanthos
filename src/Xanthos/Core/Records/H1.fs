namespace Xanthos.Core.Records

open System
open Xanthos.Core
open Xanthos.Core.Records.FieldDefinitions
open Xanthos.Core.Records.RecordParser

/// H1 Record: 単勝・複勝払戻 (Win/Place Payoff)
module H1 =

    /// H1 Record data
    type H1Record =
        { RaceKey: string // レースキー
          BetType: int option // 式別 (1=単勝, 2=複勝)
          HorseNumber: int option // 馬番
          Payoff: int option // 払戻金
          Popularity: int option } // 人気順

    /// H1 record field specifications
    let private fieldSpecs =
        [ textRaw "RecordType" 0 2 // レコード種別
          text "RaceKey" 2 16 // レースキー
          int "BetType" 18 1 // 式別
          int "HorseNumber" 19 2 // 馬番
          int "Payoff" 21 6 // 払戻金
          int "Popularity" 27 2 ] // 人気順

    /// Parse H1 record from raw bytes
    let parse (data: byte[]) : Result<H1Record, XanthosError> =
        parseRecord {
            let! fields = parseFieldsXanthos data fieldSpecs
            let! raceKey = requireText fields "RaceKey"

            return
                { RaceKey = raceKey
                  BetType = getInt fields "BetType"
                  HorseNumber = getInt fields "HorseNumber"
                  Payoff = getInt fields "Payoff"
                  Popularity = getInt fields "Popularity" }
        }

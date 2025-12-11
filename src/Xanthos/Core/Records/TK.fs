namespace Xanthos.Core.Records

open System
open Xanthos.Core
open Xanthos.Core.Records.FieldDefinitions
open Xanthos.Core.Records.RecordParser
open Xanthos.Core.Records.CodeTables

/// TK Record: 特別登録馬 (Special Registration Horse)
module TK =

    /// TK Record data
    type TKRecord =
        { RaceKey: string // レースキー
          HorseId: string // 馬ID
          HorseName: string // 馬名
          Sex: SexCode option // 性別
          HairColor: HairColorCode option // 毛色
          FatherName: string option // 父馬名
          MotherName: string option // 母馬名
          MotherFatherName: string option // 母父馬名
          BirthYear: int option // 生年
          TrainerName: string option // 調教師名
          OwnerName: string option // 馬主名
          BreederName: string option } // 生産者名

    /// TK record field specifications
    let private fieldSpecs =
        [ textRaw "RecordType" 0 2 // レコード種別
          text "RaceKey" 2 16 // レースキー
          text "HorseId" 18 10 // 馬ID
          text "HorseName" 28 36 // 馬名
          code "Sex" 64 1 "SexCode" // 性別コード
          code "HairColor" 65 1 "HairColorCode" // 毛色コード
          text "FatherName" 66 36 // 父馬名
          text "MotherName" 102 36 // 母馬名
          text "MotherFatherName" 138 36 // 母父馬名
          int "BirthYear" 174 4 // 生年
          text "TrainerName" 178 34 // 調教師名
          text "OwnerName" 212 64 // 馬主名
          text "BreederName" 276 70 ] // 生産者名

    /// Parse TK record from raw bytes
    let parse (data: byte[]) : Result<TKRecord, XanthosError> =
        parseRecord {
            let! fields = parseFieldsXanthos data fieldSpecs
            let! raceKey = requireText fields "RaceKey"
            let! horseId = requireText fields "HorseId"
            let! horseName = requireText fields "HorseName"

            return
                { RaceKey = raceKey
                  HorseId = horseId
                  HorseName = horseName
                  Sex = getCode<SexCode> fields "Sex"
                  HairColor = getCode<HairColorCode> fields "HairColor"
                  FatherName = getText fields "FatherName"
                  MotherName = getText fields "MotherName"
                  MotherFatherName = getText fields "MotherFatherName"
                  BirthYear = getInt fields "BirthYear"
                  TrainerName = getText fields "TrainerName"
                  OwnerName = getText fields "OwnerName"
                  BreederName = getText fields "BreederName" }
        }

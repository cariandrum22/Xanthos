namespace Xanthos.Core.Records

open System
open Xanthos.Core
open Xanthos.Core.Records.FieldDefinitions
open Xanthos.Core.Records.RecordParser
open Xanthos.Core.Records.CodeTables

/// UM Record: 馬マスタ (Horse Master)
module UM =

    /// UM Record data
    type UMRecord =
        { HorseId: string // 馬ID
          HorseName: string // 馬名
          Sex: SexCode option // 性別
          HairColor: HairColorCode option // 毛色
          BirthDate: DateTime option // 生年月日
          FatherName: string option // 父馬名
          MotherName: string option // 母馬名
          MotherFatherName: string option // 母父馬名
          TrainerName: string option // 調教師名
          OwnerName: string option // 馬主名
          BreederName: string option // 生産者名
          ProductionArea: string option } // 産地

    /// UM record field specifications
    let private fieldSpecs =
        [ textRaw "RecordType" 0 2 // レコード種別
          text "HorseId" 2 10 // 馬ID
          text "HorseName" 12 36 // 馬名
          code "Sex" 48 1 "SexCode" // 性別
          code "HairColor" 49 1 "HairColorCode" // 毛色
          date "BirthDate" 50 8 "yyyyMMdd" // 生年月日
          text "FatherName" 58 36 // 父馬名
          text "MotherName" 94 36 // 母馬名
          text "MotherFatherName" 130 36 // 母父馬名
          text "TrainerName" 166 34 // 調教師名
          text "OwnerName" 200 64 // 馬主名
          text "BreederName" 264 70 // 生産者名
          text "ProductionArea" 334 20 ] // 産地

    /// Parse UM record from raw bytes
    let parse (data: byte[]) : Result<UMRecord, XanthosError> =
        parseRecord {
            let! fields = parseFieldsXanthos data fieldSpecs
            let! horseId = requireText fields "HorseId"
            let! horseName = requireText fields "HorseName"

            return
                { HorseId = horseId
                  HorseName = horseName
                  Sex = getCode<SexCode> fields "Sex"
                  HairColor = getCode<HairColorCode> fields "HairColor"
                  BirthDate = getDate fields "BirthDate"
                  FatherName = getText fields "FatherName"
                  MotherName = getText fields "MotherName"
                  MotherFatherName = getText fields "MotherFatherName"
                  TrainerName = getText fields "TrainerName"
                  OwnerName = getText fields "OwnerName"
                  BreederName = getText fields "BreederName"
                  ProductionArea = getText fields "ProductionArea" }
        }

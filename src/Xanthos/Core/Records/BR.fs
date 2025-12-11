namespace Xanthos.Core.Records

open System
open Xanthos.Core
open Xanthos.Core.Records.FieldDefinitions
open Xanthos.Core.Records.RecordParser
open Xanthos.Core.Records.CodeTables

/// BR Record: 繁殖牝馬マスタ (Broodmare Master)
module BR =

    /// BR Record data
    type BRRecord =
        { HorseId: string // 馬ID
          HorseName: string // 馬名
          HairColor: HairColorCode option // 毛色
          BirthYear: int option // 生年
          FatherName: string option // 父馬名
          MotherName: string option // 母馬名
          MotherFatherName: string option // 母父馬名
          BreederName: string option // 生産者名
          ProductionArea: string option } // 産地

    /// BR record field specifications
    let private fieldSpecs =
        [ textRaw "RecordType" 0 2 // レコード種別
          text "HorseId" 2 10 // 馬ID
          text "HorseName" 12 36 // 馬名
          code "HairColor" 48 1 "HairColorCode" // 毛色
          int "BirthYear" 49 4 // 生年
          text "FatherName" 53 36 // 父馬名
          text "MotherName" 89 36 // 母馬名
          text "MotherFatherName" 125 36 // 母父馬名
          text "BreederName" 161 70 // 生産者名
          text "ProductionArea" 231 20 ] // 産地

    /// Parse BR record from raw bytes
    let parse (data: byte[]) : Result<BRRecord, XanthosError> =
        parseRecord {
            let! fields = parseFieldsXanthos data fieldSpecs
            let! horseId = requireText fields "HorseId"
            let! horseName = requireText fields "HorseName"

            return
                { HorseId = horseId
                  HorseName = horseName
                  HairColor = getCode<HairColorCode> fields "HairColor"
                  BirthYear = getInt fields "BirthYear"
                  FatherName = getText fields "FatherName"
                  MotherName = getText fields "MotherName"
                  MotherFatherName = getText fields "MotherFatherName"
                  BreederName = getText fields "BreederName"
                  ProductionArea = getText fields "ProductionArea" }
        }

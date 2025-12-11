namespace Xanthos.Core.Records

open System
open Xanthos.Core
open Xanthos.Core.Records.FieldDefinitions
open Xanthos.Core.Records.RecordParser
open Xanthos.Core.Records.CodeTables

/// HN Record: 繁殖馬マスタ (Breeding Horse Master)
/// Record Length: 251 bytes
module HN =

    /// 繁殖馬持込区分 (Import Category)
    type ImportCategory =
        | Domestic = 0 // 内国産
        | BroughtIn = 1 // 持込
        | ImportedDomestic = 2 // 輸入内国産扱い
        | Imported = 3 // 輸入
        | Other = 9 // その他

    /// HN Record data
    type HNRecord =
        { BreedingRegNum: string // 繁殖登録番号
          PedigreeRegNum: string // 血統登録番号
          HorseName: string // 馬名
          HorseNameKana: string // 馬名半角カナ
          HorseNameEn: string // 馬名欧字
          BirthYear: int option // 生年
          SexCode: SexCode option // 性別コード
          HairColorCode: HairColorCode option // 毛色コード
          ImportCategory: ImportCategory option // 繁殖馬持込区分
          ImportYear: int option // 輸入年
          Birthplace: string // 産地名
          FatherBreedingRegNum: string // 父馬繁殖登録番号
          MotherBreedingRegNum: string // 母馬繁殖登録番号
          DataCategory: int option // データ区分
          CreatedDate: DateTime option } // データ作成年月日

    /// HN record field specifications (positions are 0-based byte offsets)
    let private fieldSpecs =
        [ textRaw "RecordType" 0 2 // レコード種別ID (position 1)
          int "DataCategory" 2 1 // データ区分 (position 3)
          date "CreatedDate" 3 8 "yyyyMMdd" // データ作成年月日 (position 4)
          textRaw "BreedingRegNum" 11 10 // 繁殖登録番号 (position 12)
          textRaw "PedigreeRegNum" 29 10 // 血統登録番号 (position 30)
          text "HorseName" 40 36 // 馬名 (position 41)
          textRaw "HorseNameKana" 76 40 // 馬名半角カナ (position 77)
          text "HorseNameEn" 116 80 // 馬名欧字 (position 117)
          int "BirthYear" 196 4 // 生年 (position 197)
          code "SexCode" 200 1 "SexCode" // 性別コード (position 201)
          code "HairColorCode" 202 2 "HairColorCode" // 毛色コード (position 203)
          code "ImportCategory" 204 1 "ImportCategory" // 繁殖馬持込区分 (position 205)
          int "ImportYear" 205 4 // 輸入年 (position 206)
          text "Birthplace" 209 20 // 産地名 (position 210)
          textRaw "FatherBreedingRegNum" 229 10 // 父馬繁殖登録番号 (position 230)
          textRaw "MotherBreedingRegNum" 239 10 ] // 母馬繁殖登録番号 (position 240)

    /// Parse HN record from raw bytes
    let parse (data: byte[]) : Result<HNRecord, XanthosError> =
        parseRecord {
            let! fields = parseFieldsXanthos data fieldSpecs
            let! breedingRegNum = requireText fields "BreedingRegNum"

            return
                { BreedingRegNum = breedingRegNum
                  PedigreeRegNum = getText fields "PedigreeRegNum" |> Option.defaultValue ""
                  HorseName = getText fields "HorseName" |> Option.defaultValue ""
                  HorseNameKana = getText fields "HorseNameKana" |> Option.defaultValue ""
                  HorseNameEn = getText fields "HorseNameEn" |> Option.defaultValue ""
                  BirthYear = getInt fields "BirthYear"
                  SexCode = getCode<SexCode> fields "SexCode"
                  HairColorCode = getCode<HairColorCode> fields "HairColorCode"
                  ImportCategory = getCode<ImportCategory> fields "ImportCategory"
                  ImportYear = getInt fields "ImportYear"
                  Birthplace = getText fields "Birthplace" |> Option.defaultValue ""
                  FatherBreedingRegNum = getText fields "FatherBreedingRegNum" |> Option.defaultValue ""
                  MotherBreedingRegNum = getText fields "MotherBreedingRegNum" |> Option.defaultValue ""
                  DataCategory = getInt fields "DataCategory"
                  CreatedDate = getDate fields "CreatedDate" }
        }

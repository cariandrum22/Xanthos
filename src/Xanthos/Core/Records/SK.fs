namespace Xanthos.Core.Records

open System
open Xanthos.Core
open Xanthos.Core.Records.FieldDefinitions
open Xanthos.Core.Records.RecordParser
open Xanthos.Core.Records.CodeTables

/// SK Record: 産駒マスタ (Offspring Master)
/// Record Length: 208 bytes
module SK =

    /// 産駒持込区分 (Import Category for offspring)
    type OffspringImportCategory =
        | Domestic = 0 // 内国産
        | BroughtIn = 1 // 持込
        | ImportedDomestic = 2 // 輸入内国産扱い
        | Imported = 3 // 輸入

    /// 品種コード (Breed Code)
    type BreedCode =
        | Thoroughbred = 1 // サラ系
        | Arab = 2 // アラ系

    /// 3代血統情報 (Three Generation Pedigree)
    type ThreeGenerationPedigree =
        { Father: string // 父
          Mother: string // 母
          FatherFather: string // 父父
          FatherMother: string // 父母
          MotherFather: string // 母父
          MotherMother: string // 母母
          FatherFatherFather: string // 父父父
          FatherFatherMother: string // 父父母
          FatherMotherFather: string // 父母父
          FatherMotherMother: string // 父母母
          MotherFatherFather: string // 母父父
          MotherFatherMother: string // 母父母
          MotherMotherFather: string // 母母父
          MotherMotherMother: string } // 母母母

    /// SK Record data
    type SKRecord =
        { PedigreeRegNum: string // 血統登録番号
          BirthDate: DateTime option // 生年月日
          SexCode: SexCode option // 性別コード
          BreedCode: BreedCode option // 品種コード
          HairColorCode: HairColorCode option // 毛色コード
          ImportCategory: OffspringImportCategory option // 産駒持込区分
          ImportYear: int option // 輸入年
          ProducerCode: string // 生産者コード
          Birthplace: string // 産地名
          Pedigree: ThreeGenerationPedigree // 3代血統
          DataCategory: int option // データ区分
          CreatedDate: DateTime option } // データ作成年月日

    /// SK record field specifications (positions are 0-based byte offsets)
    let private fieldSpecs =
        [ textRaw "RecordType" 0 2 // レコード種別ID (position 1)
          int "DataCategory" 2 1 // データ区分 (position 3)
          date "CreatedDate" 3 8 "yyyyMMdd" // データ作成年月日 (position 4)
          textRaw "PedigreeRegNum" 11 10 // 血統登録番号 (position 12)
          date "BirthDate" 21 8 "yyyyMMdd" // 生年月日 (position 22)
          code "SexCode" 29 1 "SexCode" // 性別コード (position 30)
          code "BreedCode" 30 1 "BreedCode" // 品種コード (position 31)
          code "HairColorCode" 31 2 "HairColorCode" // 毛色コード (position 32)
          code "ImportCategory" 33 1 "OffspringImportCategory" // 産駒持込区分 (position 34)
          int "ImportYear" 34 4 // 輸入年 (position 35)
          textRaw "ProducerCode" 38 8 // 生産者コード (position 39)
          text "Birthplace" 46 20 // 産地名 (position 47)
          // 3代血統 - 繁殖登録番号 (14 x 10 bytes = 140 bytes, position 67)
          textRaw "Father" 66 10 // 父
          textRaw "Mother" 76 10 // 母
          textRaw "FatherFather" 86 10 // 父父
          textRaw "FatherMother" 96 10 // 父母
          textRaw "MotherFather" 106 10 // 母父
          textRaw "MotherMother" 116 10 // 母母
          textRaw "FatherFatherFather" 126 10 // 父父父
          textRaw "FatherFatherMother" 136 10 // 父父母
          textRaw "FatherMotherFather" 146 10 // 父母父
          textRaw "FatherMotherMother" 156 10 // 父母母
          textRaw "MotherFatherFather" 166 10 // 母父父
          textRaw "MotherFatherMother" 176 10 // 母父母
          textRaw "MotherMotherFather" 186 10 // 母母父
          textRaw "MotherMotherMother" 196 10 ] // 母母母

    /// Parse SK record from raw bytes
    let parse (data: byte[]) : Result<SKRecord, XanthosError> =
        parseRecord {
            let! fields = parseFieldsXanthos data fieldSpecs
            let! pedigreeRegNum = requireText fields "PedigreeRegNum"

            let pedigree =
                { Father = getText fields "Father" |> Option.defaultValue ""
                  Mother = getText fields "Mother" |> Option.defaultValue ""
                  FatherFather = getText fields "FatherFather" |> Option.defaultValue ""
                  FatherMother = getText fields "FatherMother" |> Option.defaultValue ""
                  MotherFather = getText fields "MotherFather" |> Option.defaultValue ""
                  MotherMother = getText fields "MotherMother" |> Option.defaultValue ""
                  FatherFatherFather = getText fields "FatherFatherFather" |> Option.defaultValue ""
                  FatherFatherMother = getText fields "FatherFatherMother" |> Option.defaultValue ""
                  FatherMotherFather = getText fields "FatherMotherFather" |> Option.defaultValue ""
                  FatherMotherMother = getText fields "FatherMotherMother" |> Option.defaultValue ""
                  MotherFatherFather = getText fields "MotherFatherFather" |> Option.defaultValue ""
                  MotherFatherMother = getText fields "MotherFatherMother" |> Option.defaultValue ""
                  MotherMotherFather = getText fields "MotherMotherFather" |> Option.defaultValue ""
                  MotherMotherMother = getText fields "MotherMotherMother" |> Option.defaultValue "" }

            return
                { PedigreeRegNum = pedigreeRegNum
                  BirthDate = getDate fields "BirthDate"
                  SexCode = getCode<SexCode> fields "SexCode"
                  BreedCode = getCode<BreedCode> fields "BreedCode"
                  HairColorCode = getCode<HairColorCode> fields "HairColorCode"
                  ImportCategory = getCode<OffspringImportCategory> fields "ImportCategory"
                  ImportYear = getInt fields "ImportYear"
                  ProducerCode = getText fields "ProducerCode" |> Option.defaultValue ""
                  Birthplace = getText fields "Birthplace" |> Option.defaultValue ""
                  Pedigree = pedigree
                  DataCategory = getInt fields "DataCategory"
                  CreatedDate = getDate fields "CreatedDate" }
        }

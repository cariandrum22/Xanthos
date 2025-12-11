namespace Xanthos.Core.Records

open System
open Xanthos.Core
open Xanthos.Core.Records.FieldDefinitions
open Xanthos.Core.Records.RecordParser

/// HS Record: 競走馬市場取引価格 (Horse Market Transaction Price)
/// Record Length: 200 bytes
module HS =

    /// HS Record data
    type HSRecord =
        { PedigreeRegNum: string // 血統登録番号
          FatherBreedingRegNum: string // 父馬繁殖登録番号
          MotherBreedingRegNum: string // 母馬繁殖登録番号
          BirthYear: int option // 生年
          MarketCode: string // 主催者・市場コード
          OrganizerName: string // 主催者名称
          MarketName: string // 市場の名称
          MarketStartDate: DateTime option // 市場の開催期間(開始日)
          MarketEndDate: DateTime option // 市場の開催期間(終了日)
          HorseAge: int option // 取引時の競走馬の年齢
          TransactionPrice: string // 取引価格 (円) - raw string for large values
          DataCategory: int option // データ区分
          CreatedDate: DateTime option } // データ作成年月日

    /// HS record field specifications (positions are 0-based byte offsets)
    let private fieldSpecs =
        [ textRaw "RecordType" 0 2 // レコード種別ID (position 1)
          int "DataCategory" 2 1 // データ区分 (position 3)
          date "CreatedDate" 3 8 "yyyyMMdd" // データ作成年月日 (position 4)
          textRaw "PedigreeRegNum" 11 10 // 血統登録番号 (position 12)
          textRaw "FatherBreedingRegNum" 21 10 // 父馬繁殖登録番号 (position 22)
          textRaw "MotherBreedingRegNum" 31 10 // 母馬繁殖登録番号 (position 32)
          int "BirthYear" 41 4 // 生年 (position 42)
          textRaw "MarketCode" 45 6 // 主催者・市場コード (position 46)
          text "OrganizerName" 51 40 // 主催者名称 (position 52)
          text "MarketName" 91 80 // 市場の名称 (position 92)
          date "MarketStartDate" 171 8 "yyyyMMdd" // 市場開始日 (position 172)
          date "MarketEndDate" 179 8 "yyyyMMdd" // 市場終了日 (position 180)
          int "HorseAge" 187 1 // 取引時年齢 (position 188)
          textRaw "TransactionPrice" 188 10 ] // 取引価格 (position 189)

    /// Parse HS record from raw bytes
    let parse (data: byte[]) : Result<HSRecord, XanthosError> =
        parseRecord {
            let! fields = parseFieldsXanthos data fieldSpecs
            let! pedigreeRegNum = requireText fields "PedigreeRegNum"

            return
                { PedigreeRegNum = pedigreeRegNum
                  FatherBreedingRegNum = getText fields "FatherBreedingRegNum" |> Option.defaultValue ""
                  MotherBreedingRegNum = getText fields "MotherBreedingRegNum" |> Option.defaultValue ""
                  BirthYear = getInt fields "BirthYear"
                  MarketCode = getText fields "MarketCode" |> Option.defaultValue ""
                  OrganizerName = getText fields "OrganizerName" |> Option.defaultValue ""
                  MarketName = getText fields "MarketName" |> Option.defaultValue ""
                  MarketStartDate = getDate fields "MarketStartDate"
                  MarketEndDate = getDate fields "MarketEndDate"
                  HorseAge = getInt fields "HorseAge"
                  TransactionPrice = getText fields "TransactionPrice" |> Option.defaultValue ""
                  DataCategory = getInt fields "DataCategory"
                  CreatedDate = getDate fields "CreatedDate" }
        }

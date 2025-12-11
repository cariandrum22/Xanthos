namespace Xanthos.Core.Records

open System
open Xanthos.Core
open Xanthos.Core.Records.FieldDefinitions
open Xanthos.Core.Records.RecordParser

/// HY Record: 馬名の意味由来 (Horse Name Meaning/Origin)
/// Record Length: 123 bytes
module HY =

    /// HY Record data
    type HYRecord =
        { PedigreeRegNum: string // 血統登録番号
          HorseName: string // 馬名
          NameMeaning: string // 馬名の意味由来
          DataCategory: int option // データ区分
          CreatedDate: DateTime option } // データ作成年月日

    /// HY record field specifications (positions are 0-based byte offsets)
    let private fieldSpecs =
        [ textRaw "RecordType" 0 2 // レコード種別ID (position 1)
          int "DataCategory" 2 1 // データ区分 (position 3)
          date "CreatedDate" 3 8 "yyyyMMdd" // データ作成年月日 (position 4)
          textRaw "PedigreeRegNum" 11 10 // 血統登録番号 (position 12)
          text "HorseName" 21 36 // 馬名 (position 22)
          text "NameMeaning" 57 64 ] // 馬名の意味由来 (position 58)

    /// Parse HY record from raw bytes
    let parse (data: byte[]) : Result<HYRecord, XanthosError> =
        parseRecord {
            let! fields = parseFieldsXanthos data fieldSpecs
            let! pedigreeRegNum = requireText fields "PedigreeRegNum"

            return
                { PedigreeRegNum = pedigreeRegNum
                  HorseName = getText fields "HorseName" |> Option.defaultValue ""
                  NameMeaning = getText fields "NameMeaning" |> Option.defaultValue ""
                  DataCategory = getInt fields "DataCategory"
                  CreatedDate = getDate fields "CreatedDate" }
        }

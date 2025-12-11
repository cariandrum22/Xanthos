namespace Xanthos.Core.Records

open System
open Xanthos.Core
open Xanthos.Core.Records.FieldDefinitions
open Xanthos.Core.Records.RecordParser

/// BT Record: 系統情報 (Bloodline/Lineage Information)
/// Record Length: 6889 bytes
module BT =

    /// BT Record data
    type BTRecord =
        { BreedingRegNum: string // 繁殖登録番号
          LineageId: string // 系統ID
          LineageName: string // 系統名
          LineageDescription: string // 系統説明
          DataCategory: int option // データ区分
          CreatedDate: DateTime option } // データ作成年月日

    /// BT record field specifications (positions are 0-based byte offsets)
    let private fieldSpecs =
        [ textRaw "RecordType" 0 2 // レコード種別ID (position 1)
          int "DataCategory" 2 1 // データ区分 (position 3)
          date "CreatedDate" 3 8 "yyyyMMdd" // データ作成年月日 (position 4)
          textRaw "BreedingRegNum" 11 10 // 繁殖登録番号 (position 12)
          textRaw "LineageId" 21 30 // 系統ID (position 22)
          text "LineageName" 51 36 // 系統名 (position 52)
          text "LineageDescription" 87 6800 ] // 系統説明 (position 88)

    /// Parse BT record from raw bytes
    let parse (data: byte[]) : Result<BTRecord, XanthosError> =
        parseRecord {
            let! fields = parseFieldsXanthos data fieldSpecs
            let! breedingRegNum = requireText fields "BreedingRegNum"

            return
                { BreedingRegNum = breedingRegNum
                  LineageId = getText fields "LineageId" |> Option.defaultValue ""
                  LineageName = getText fields "LineageName" |> Option.defaultValue ""
                  LineageDescription = getText fields "LineageDescription" |> Option.defaultValue ""
                  DataCategory = getInt fields "DataCategory"
                  CreatedDate = getDate fields "CreatedDate" }
        }

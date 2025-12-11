namespace Xanthos.Core.Records

open System
open Xanthos.Core
open Xanthos.Core.Records.FieldDefinitions
open Xanthos.Core.Records.RecordParser
open Xanthos.Core.Records.CodeTables

/// JG Record: 競走馬除外情報 (Horse Exclusion Info)
/// Record Length: 80 bytes
module JG =

    /// 出走区分 (Entry Category)
    type EntryCategory =
        | VotingHorse = 1 // 投票馬
        | ExcludedAtDeadline = 2 // 締切での除外馬
        | ReVotingHorse = 4 // 再投票馬
        | ReVotingExcluded = 5 // 再投票除外馬
        | ScratchedNoNumber = 6 // 馬番を付さない出走取消馬
        | Scratched = 9 // 取消馬

    /// 除外状態区分 (Exclusion Status)
    type ExclusionStatus =
        | NotDrawn = 1 // 非抽選馬
        | NotWon = 2 // 非当選馬

    /// JG Record data
    type JGRecord =
        { Year: int // 開催年
          MonthDay: string // 開催月日 (mmdd)
          RacecourseCode: RacecourseCode option // 競馬場コード
          Kai: int option // 開催回
          Day: int option // 開催日目
          RaceNumber: int option // レース番号
          PedigreeRegNum: string // 血統登録番号
          HorseName: string // 馬名
          VotingOrder: int option // 出馬投票受付順番
          EntryCategory: EntryCategory option // 出走区分
          ExclusionStatus: ExclusionStatus option // 除外状態区分
          DataCategory: int option // データ区分
          CreatedDate: DateTime option } // データ作成年月日

    /// JG record field specifications (positions are 0-based byte offsets)
    let private fieldSpecs =
        [ textRaw "RecordType" 0 2 // レコード種別ID (position 1)
          int "DataCategory" 2 1 // データ区分 (position 3)
          date "CreatedDate" 3 8 "yyyyMMdd" // データ作成年月日 (position 4)
          int "Year" 11 4 // 開催年 (position 12)
          textRaw "MonthDay" 15 4 // 開催月日 (position 16)
          code "RacecourseCode" 19 2 "RacecourseCode" // 競馬場コード (position 20)
          int "Kai" 21 2 // 開催回 (position 22)
          int "Day" 23 2 // 開催日目 (position 24)
          int "RaceNumber" 25 2 // レース番号 (position 26)
          textRaw "PedigreeRegNum" 27 10 // 血統登録番号 (position 28)
          text "HorseName" 37 36 // 馬名 (position 38)
          int "VotingOrder" 73 3 // 出馬投票受付順番 (position 74)
          code "EntryCategory" 76 1 "EntryCategory" // 出走区分 (position 77)
          code "ExclusionStatus" 77 1 "ExclusionStatus" ] // 除外状態区分 (position 78)

    /// Parse JG record from raw bytes
    let parse (data: byte[]) : Result<JGRecord, XanthosError> =
        parseRecord {
            let! fields = parseFieldsXanthos data fieldSpecs
            let! year = requireInt fields "Year"
            let! pedigreeRegNum = requireText fields "PedigreeRegNum"

            return
                { Year = year
                  MonthDay = getText fields "MonthDay" |> Option.defaultValue ""
                  RacecourseCode = getCode<RacecourseCode> fields "RacecourseCode"
                  Kai = getInt fields "Kai"
                  Day = getInt fields "Day"
                  RaceNumber = getInt fields "RaceNumber"
                  PedigreeRegNum = pedigreeRegNum
                  HorseName = getText fields "HorseName" |> Option.defaultValue ""
                  VotingOrder = getInt fields "VotingOrder"
                  EntryCategory = getCode<EntryCategory> fields "EntryCategory"
                  ExclusionStatus = getCode<ExclusionStatus> fields "ExclusionStatus"
                  DataCategory = getInt fields "DataCategory"
                  CreatedDate = getDate fields "CreatedDate" }
        }

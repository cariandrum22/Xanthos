namespace Xanthos.Core.Records

open System
open Xanthos.Core
open Xanthos.Core.Records.FieldDefinitions
open Xanthos.Core.Records.RecordParser
open Xanthos.Core.Records.CodeTables

/// CS Record: コース情報 (Course Information)
/// Record Length: 6829 bytes
module CS =

    /// CS Record data
    type CSRecord =
        { RacecourseCode: RacecourseCode option // 競馬場コード
          Distance: int option // 距離 (メートル)
          TrackCode: string // トラックコード
          CourseRevisionDate: DateTime option // コース改修年月日
          CourseDescription: string // コース説明
          DataCategory: int option // データ区分
          CreatedDate: DateTime option } // データ作成年月日

    /// CS record field specifications (positions are 0-based byte offsets)
    let private fieldSpecs =
        [ textRaw "RecordType" 0 2 // レコード種別ID (position 1)
          int "DataCategory" 2 1 // データ区分 (position 3)
          date "CreatedDate" 3 8 "yyyyMMdd" // データ作成年月日 (position 4)
          code "RacecourseCode" 11 2 "RacecourseCode" // 競馬場コード (position 12)
          int "Distance" 13 4 // 距離 (position 14)
          textRaw "TrackCode" 17 2 // トラックコード (position 18)
          date "CourseRevisionDate" 19 8 "yyyyMMdd" // コース改修年月日 (position 20)
          text "CourseDescription" 27 6800 ] // コース説明 (position 28)

    /// Parse CS record from raw bytes
    let parse (data: byte[]) : Result<CSRecord, XanthosError> =
        parseRecord {
            let! fields = parseFieldsXanthos data fieldSpecs

            return
                { RacecourseCode = getCode<RacecourseCode> fields "RacecourseCode"
                  Distance = getInt fields "Distance"
                  TrackCode = getText fields "TrackCode" |> Option.defaultValue ""
                  CourseRevisionDate = getDate fields "CourseRevisionDate"
                  CourseDescription = getText fields "CourseDescription" |> Option.defaultValue ""
                  DataCategory = getInt fields "DataCategory"
                  CreatedDate = getDate fields "CreatedDate" }
        }

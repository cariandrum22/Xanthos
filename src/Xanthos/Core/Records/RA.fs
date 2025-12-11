namespace Xanthos.Core.Records

open System
open Xanthos.Core
open Xanthos.Core.Records.FieldDefinitions
open Xanthos.Core.Records.RecordParser
open Xanthos.Core.Records.CodeTables

/// RA Record: レース詳細 (Race Details)
module RA =

    /// RA Record data
    type RARecord =
        { RaceKey: string // レースキー
          RaceName: string // レース名
          RacecourseCode: RacecourseCode option // 競馬場コード
          TrackSurface: TrackSurfaceCode option // トラック種別
          TrackCondition: TrackConditionCode option // 馬場状態
          Distance: int option // 距離
          Grade: GradeCode option // グレード
          RaceCondition: RaceConditionCode option // レース条件
          StartTime: DateTime option // 発走時刻
          EntryCount: int option // 出走頭数
          Weather: string option // 天候
          CourseType: string option // コース種別
          Prize1st: int option // 1着賞金
          Prize2nd: int option // 2着賞金
          Prize3rd: int option } // 3着賞金

    /// RA record field specifications
    let private fieldSpecs =
        [ textRaw "RecordType" 0 2 // レコード種別
          text "RaceKey" 2 16 // レースキー
          text "RaceName" 18 50 // レース名
          code "Racecourse" 68 2 "RacecourseCode" // 競馬場コード
          code "TrackSurface" 70 1 "TrackSurfaceCode" // トラック種別
          code "TrackCondition" 71 1 "TrackConditionCode" // 馬場状態
          int "Distance" 72 4 // 距離
          code "Grade" 76 1 "GradeCode" // グレード
          code "RaceCondition" 77 2 "RaceConditionCode" // レース条件
          date "StartTime" 79 12 "yyyyMMddHHmm" // 発走時刻
          int "EntryCount" 91 2 // 出走頭数
          text "Weather" 93 1 // 天候
          text "CourseType" 94 2 // コース種別
          int "Prize1st" 96 8 // 1着賞金
          int "Prize2nd" 104 8 // 2着賞金
          int "Prize3rd" 112 8 ] // 3着賞金

    /// Parse RA record from raw bytes
    let parse (data: byte[]) : Result<RARecord, XanthosError> =
        parseRecord {
            let! fields = parseFieldsXanthos data fieldSpecs
            let! raceKey = requireText fields "RaceKey"
            let! raceName = requireText fields "RaceName"

            return
                { RaceKey = raceKey
                  RaceName = raceName
                  RacecourseCode = getCode<RacecourseCode> fields "Racecourse"
                  TrackSurface = getCode<TrackSurfaceCode> fields "TrackSurface"
                  TrackCondition = getCode<TrackConditionCode> fields "TrackCondition"
                  Distance = getInt fields "Distance"
                  Grade = getCode<GradeCode> fields "Grade"
                  RaceCondition = getCode<RaceConditionCode> fields "RaceCondition"
                  StartTime = getDate fields "StartTime"
                  EntryCount = getInt fields "EntryCount"
                  Weather = getText fields "Weather"
                  CourseType = getText fields "CourseType"
                  Prize1st = getInt fields "Prize1st"
                  Prize2nd = getInt fields "Prize2nd"
                  Prize3rd = getInt fields "Prize3rd" }
        }

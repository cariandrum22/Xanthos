namespace Xanthos.Core.Records

open System
open Xanthos.Core
open Xanthos.Core.Records.FieldDefinitions
open Xanthos.Core.Records.RecordParser
open Xanthos.Core.Records.CodeTables

/// RC Record: レースコードマスタ (Race Code Master)
module RC =

    /// RC Record data
    type RCRecord =
        { RaceCode: string // レースコード
          RaceName: string // レース名
          RaceNameShort: string option // レース名略称
          Grade: GradeCode option // グレード
          RaceCondition: RaceConditionCode option // レース条件
          Distance: int option // 距離
          TrackSurface: TrackSurfaceCode option } // トラック種別

    /// RC record field specifications
    let private fieldSpecs =
        [ textRaw "RecordType" 0 2 // レコード種別
          text "RaceCode" 2 4 // レースコード
          text "RaceName" 6 50 // レース名
          text "RaceNameShort" 56 20 // レース名略称
          code "Grade" 76 1 "GradeCode" // グレード
          code "RaceCondition" 77 2 "RaceConditionCode" // レース条件
          int "Distance" 79 4 // 距離
          code "TrackSurface" 83 1 "TrackSurfaceCode" ] // トラック種別

    /// Parse RC record from raw bytes
    let parse (data: byte[]) : Result<RCRecord, XanthosError> =
        parseRecord {
            let! fields = parseFieldsXanthos data fieldSpecs
            let! raceCode = requireText fields "RaceCode"
            let! raceName = requireText fields "RaceName"

            return
                { RaceCode = raceCode
                  RaceName = raceName
                  RaceNameShort = getText fields "RaceNameShort"
                  Grade = getCode<GradeCode> fields "Grade"
                  RaceCondition = getCode<RaceConditionCode> fields "RaceCondition"
                  Distance = getInt fields "Distance"
                  TrackSurface = getCode<TrackSurfaceCode> fields "TrackSurface" }
        }

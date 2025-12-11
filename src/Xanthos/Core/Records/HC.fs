namespace Xanthos.Core.Records

open System
open Xanthos.Core
open Xanthos.Core.Records.FieldDefinitions
open Xanthos.Core.Records.RecordParser

/// HC Record: 坂路調教 (Slope Training)
/// Record Length: 60 bytes
module HC =

    /// トレセン区分 (Training Center Category)
    type TrainingCenterCode =
        | Miho = 0 // 美浦
        | Ritto = 1 // 栗東

    /// HC Record data
    type HCRecord =
        { TrainingCenter: TrainingCenterCode option // トレセン区分
          TrainingDate: DateTime option // 調教年月日
          TrainingTime: string // 調教時刻 (HHmm)
          PedigreeRegNum: string // 血統登録番号
          FurlongTime4: int option // 4ハロンタイム合計 (800M~0M) 単位:0.1秒
          LapTime800to600: int option // ラップタイム (800M~600M) 単位:0.1秒
          FurlongTime3: int option // 3ハロンタイム合計 (600M~0M) 単位:0.1秒
          LapTime600to400: int option // ラップタイム (600M~400M) 単位:0.1秒
          FurlongTime2: int option // 2ハロンタイム合計 (400M~0M) 単位:0.1秒
          LapTime400to200: int option // ラップタイム (400M~200M) 単位:0.1秒
          LapTime200to0: int option // ラップタイム (200M~0M) 単位:0.1秒
          DataCategory: int option // データ区分
          CreatedDate: DateTime option } // データ作成年月日

    /// HC record field specifications (positions are 0-based byte offsets)
    let private fieldSpecs =
        [ textRaw "RecordType" 0 2 // レコード種別ID (position 1)
          int "DataCategory" 2 1 // データ区分 (position 3)
          date "CreatedDate" 3 8 "yyyyMMdd" // データ作成年月日 (position 4)
          code "TrainingCenter" 11 1 "TrainingCenterCode" // トレセン区分 (position 12)
          date "TrainingDate" 12 8 "yyyyMMdd" // 調教年月日 (position 13)
          textRaw "TrainingTime" 20 4 // 調教時刻 (position 21)
          textRaw "PedigreeRegNum" 24 10 // 血統登録番号 (position 25)
          int "FurlongTime4" 34 4 // 4ハロンタイム合計 (position 35)
          int "LapTime800to600" 38 3 // ラップタイム800-600 (position 39)
          int "FurlongTime3" 41 4 // 3ハロンタイム合計 (position 42)
          int "LapTime600to400" 45 3 // ラップタイム600-400 (position 46)
          int "FurlongTime2" 48 4 // 2ハロンタイム合計 (position 49)
          int "LapTime400to200" 52 3 // ラップタイム400-200 (position 53)
          int "LapTime200to0" 55 3 ] // ラップタイム200-0 (position 56)

    /// Parse HC record from raw bytes
    let parse (data: byte[]) : Result<HCRecord, XanthosError> =
        parseRecord {
            let! fields = parseFieldsXanthos data fieldSpecs
            let! pedigreeRegNum = requireText fields "PedigreeRegNum"

            return
                { TrainingCenter = getCode<TrainingCenterCode> fields "TrainingCenter"
                  TrainingDate = getDate fields "TrainingDate"
                  TrainingTime = getText fields "TrainingTime" |> Option.defaultValue ""
                  PedigreeRegNum = pedigreeRegNum
                  FurlongTime4 = getInt fields "FurlongTime4"
                  LapTime800to600 = getInt fields "LapTime800to600"
                  FurlongTime3 = getInt fields "FurlongTime3"
                  LapTime600to400 = getInt fields "LapTime600to400"
                  FurlongTime2 = getInt fields "FurlongTime2"
                  LapTime400to200 = getInt fields "LapTime400to200"
                  LapTime200to0 = getInt fields "LapTime200to0"
                  DataCategory = getInt fields "DataCategory"
                  CreatedDate = getDate fields "CreatedDate" }
        }

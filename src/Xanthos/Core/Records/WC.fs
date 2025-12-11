namespace Xanthos.Core.Records

open System
open Xanthos.Core
open Xanthos.Core.Records.FieldDefinitions
open Xanthos.Core.Records.RecordParser

/// WC Record: ウッドチップ調教 (Wood Chip Training)
/// Record Length: 105 bytes
module WC =

    /// トレセン区分 (Training Center Category)
    type TrainingCenterCode =
        | Miho = 0 // 美浦
        | Ritto = 1 // 栗東

    /// コース (Course)
    type CourseCode =
        | A = 0
        | B = 1
        | C = 2
        | D = 3
        | E = 4

    /// 馬場周り (Track Direction)
    type TrackDirection =
        | Right = 0 // 右
        | Left = 1 // 左

    /// WC Record data
    type WCRecord =
        { TrainingCenter: TrainingCenterCode option // トレセン区分
          TrainingDate: DateTime option // 調教年月日
          TrainingTime: string // 調教時刻 (HHmm)
          PedigreeRegNum: string // 血統登録番号
          Course: CourseCode option // コース
          TrackDirection: TrackDirection option // 馬場周り
          // 10ハロン (2000M)
          FurlongTime10: int option // 10ハロンタイム合計 単位:0.1秒
          LapTime2000to1800: int option // ラップタイム (2000M~1800M)
          // 9ハロン (1800M)
          FurlongTime9: int option // 9ハロンタイム合計
          LapTime1800to1600: int option // ラップタイム (1800M~1600M)
          // 8ハロン (1600M)
          FurlongTime8: int option // 8ハロンタイム合計
          LapTime1600to1400: int option // ラップタイム (1600M~1400M)
          // 7ハロン (1400M)
          FurlongTime7: int option // 7ハロンタイム合計
          LapTime1400to1200: int option // ラップタイム (1400M~1200M)
          // 6ハロン (1200M)
          FurlongTime6: int option // 6ハロンタイム合計
          LapTime1200to1000: int option // ラップタイム (1200M~1000M)
          // 5ハロン (1000M)
          FurlongTime5: int option // 5ハロンタイム合計
          LapTime1000to800: int option // ラップタイム (1000M~800M)
          // 4ハロン (800M)
          FurlongTime4: int option // 4ハロンタイム合計
          LapTime800to600: int option // ラップタイム (800M~600M)
          // 3ハロン (600M)
          FurlongTime3: int option // 3ハロンタイム合計
          LapTime600to400: int option // ラップタイム (600M~400M)
          // 2ハロン (400M)
          FurlongTime2: int option // 2ハロンタイム合計
          LapTime400to200: int option // ラップタイム (400M~200M)
          // 1ハロン (200M)
          LapTime200to0: int option // ラップタイム (200M~0M)
          DataCategory: int option // データ区分
          CreatedDate: DateTime option } // データ作成年月日

    /// WC record field specifications (positions are 0-based byte offsets)
    let private fieldSpecs =
        [ textRaw "RecordType" 0 2 // レコード種別ID (position 1)
          int "DataCategory" 2 1 // データ区分 (position 3)
          date "CreatedDate" 3 8 "yyyyMMdd" // データ作成年月日 (position 4)
          code "TrainingCenter" 11 1 "TrainingCenterCode" // トレセン区分 (position 12)
          date "TrainingDate" 12 8 "yyyyMMdd" // 調教年月日 (position 13)
          textRaw "TrainingTime" 20 4 // 調教時刻 (position 21)
          textRaw "PedigreeRegNum" 24 10 // 血統登録番号 (position 25)
          code "Course" 34 1 "CourseCode" // コース (position 35)
          code "TrackDirection" 35 1 "TrackDirection" // 馬場周り (position 36)
          // 10ハロン
          int "FurlongTime10" 37 4 // 10ハロンタイム合計 (position 38)
          int "LapTime2000to1800" 41 3 // ラップタイム2000-1800 (position 42)
          // 9ハロン
          int "FurlongTime9" 44 4 // 9ハロンタイム合計 (position 45)
          int "LapTime1800to1600" 48 3 // ラップタイム1800-1600 (position 49)
          // 8ハロン
          int "FurlongTime8" 51 4 // 8ハロンタイム合計 (position 52)
          int "LapTime1600to1400" 55 3 // ラップタイム1600-1400 (position 56)
          // 7ハロン
          int "FurlongTime7" 58 4 // 7ハロンタイム合計 (position 59)
          int "LapTime1400to1200" 62 3 // ラップタイム1400-1200 (position 63)
          // 6ハロン
          int "FurlongTime6" 65 4 // 6ハロンタイム合計 (position 66)
          int "LapTime1200to1000" 69 3 // ラップタイム1200-1000 (position 70)
          // 5ハロン
          int "FurlongTime5" 72 4 // 5ハロンタイム合計 (position 73)
          int "LapTime1000to800" 76 3 // ラップタイム1000-800 (position 77)
          // 4ハロン
          int "FurlongTime4" 79 4 // 4ハロンタイム合計 (position 80)
          int "LapTime800to600" 83 3 // ラップタイム800-600 (position 84)
          // 3ハロン
          int "FurlongTime3" 86 4 // 3ハロンタイム合計 (position 87)
          int "LapTime600to400" 90 3 // ラップタイム600-400 (position 91)
          // 2ハロン
          int "FurlongTime2" 93 4 // 2ハロンタイム合計 (position 94)
          int "LapTime400to200" 97 3 // ラップタイム400-200 (position 98)
          // 1ハロン
          int "LapTime200to0" 100 3 ] // ラップタイム200-0 (position 101)

    /// Parse WC record from raw bytes
    let parse (data: byte[]) : Result<WCRecord, XanthosError> =
        parseRecord {
            let! fields = parseFieldsXanthos data fieldSpecs
            let! pedigreeRegNum = requireText fields "PedigreeRegNum"

            return
                { TrainingCenter = getCode<TrainingCenterCode> fields "TrainingCenter"
                  TrainingDate = getDate fields "TrainingDate"
                  TrainingTime = getText fields "TrainingTime" |> Option.defaultValue ""
                  PedigreeRegNum = pedigreeRegNum
                  Course = getCode<CourseCode> fields "Course"
                  TrackDirection = getCode<TrackDirection> fields "TrackDirection"
                  FurlongTime10 = getInt fields "FurlongTime10"
                  LapTime2000to1800 = getInt fields "LapTime2000to1800"
                  FurlongTime9 = getInt fields "FurlongTime9"
                  LapTime1800to1600 = getInt fields "LapTime1800to1600"
                  FurlongTime8 = getInt fields "FurlongTime8"
                  LapTime1600to1400 = getInt fields "LapTime1600to1400"
                  FurlongTime7 = getInt fields "FurlongTime7"
                  LapTime1400to1200 = getInt fields "LapTime1400to1200"
                  FurlongTime6 = getInt fields "FurlongTime6"
                  LapTime1200to1000 = getInt fields "LapTime1200to1000"
                  FurlongTime5 = getInt fields "FurlongTime5"
                  LapTime1000to800 = getInt fields "LapTime1000to800"
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

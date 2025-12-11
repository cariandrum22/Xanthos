namespace Xanthos.Core.Records

open System
open Xanthos.Core
open Xanthos.Core.Text
open Xanthos.Core.Records.FieldDefinitions
open Xanthos.Core.Records.RecordParser
open Xanthos.Core.Records.CodeTables

/// DM Record: タイム型データマイニング予想 (Time-based Data Mining Prediction)
/// Record Length: 303 bytes
module DM =

    /// Time mining prediction entry (one per horse)
    type TimePredictionEntry =
        { HorseNumber: int option // 馬番
          PredictedTime: string // 予想走破タイム (M分SS秒SS)
          ErrorPlus: string // 予想誤差(+) 秒
          ErrorMinus: string } // 予想誤差(-) 秒

    /// DM Record data
    type DMRecord =
        { Year: int // 開催年
          MonthDay: string // 開催月日 (mmdd)
          RacecourseCode: RacecourseCode option // 競馬場コード
          Kai: int option // 開催回
          Day: int option // 開催日目
          RaceNumber: int option // レース番号
          CreatedTime: string // データ作成時分 (HHmm)
          Predictions: TimePredictionEntry list // マイニング予想 (最大18頭)
          DataCategory: int option // データ区分
          CreatedDate: DateTime option } // データ作成年月日

    /// DM record header field specifications (positions are 0-based byte offsets)
    let private headerFieldSpecs =
        [ textRaw "RecordType" 0 2 // レコード種別ID (position 1)
          int "DataCategory" 2 1 // データ区分 (position 3)
          date "CreatedDate" 3 8 "yyyyMMdd" // データ作成年月日 (position 4)
          int "Year" 11 4 // 開催年 (position 12)
          textRaw "MonthDay" 15 4 // 開催月日 (position 16)
          code "RacecourseCode" 19 2 "RacecourseCode" // 競馬場コード (position 20)
          int "Kai" 21 2 // 開催回 (position 22)
          int "Day" 23 2 // 開催日目 (position 24)
          int "RaceNumber" 25 2 // レース番号 (position 26)
          textRaw "CreatedTime" 27 4 ] // データ作成時分 (position 28)

    /// Parse a single time prediction entry from data at given offset
    let private parsePredictionEntry (data: byte[]) (baseOffset: int) : TimePredictionEntry option =
        if baseOffset + 15 > data.Length then
            None
        else
            let entryData = data.[baseOffset .. baseOffset + 14]

            let horseNumber =
                let text = decodeShiftJis entryData.[0..1] |> fun s -> s.Trim()

                if String.IsNullOrWhiteSpace text then
                    None
                else
                    match Int32.TryParse text with
                    | true, v when v > 0 && v <= 18 -> Some v
                    | _ -> None

            // Skip entries without horse number (empty slot)
            match horseNumber with
            | None -> None
            | Some _ ->
                let predictedTime = decodeShiftJis entryData.[2..6] |> fun s -> s.Trim()
                let errorPlus = decodeShiftJis entryData.[7..10] |> fun s -> s.Trim()
                let errorMinus = decodeShiftJis entryData.[11..14] |> fun s -> s.Trim()

                Some
                    { HorseNumber = horseNumber
                      PredictedTime = predictedTime
                      ErrorPlus = errorPlus
                      ErrorMinus = errorMinus }

    /// Parse DM record from raw bytes
    let parse (data: byte[]) : Result<DMRecord, XanthosError> =
        parseRecord {
            let! fields = parseFieldsXanthos data headerFieldSpecs
            let! year = requireInt fields "Year"

            // Parse repeating prediction entries (18 entries starting at offset 31, each 15 bytes)
            let predictions =
                [ 0..17 ]
                |> List.map (fun i -> parsePredictionEntry data (31 + i * 15))
                |> List.choose id

            return
                { Year = year
                  MonthDay = getText fields "MonthDay" |> Option.defaultValue ""
                  RacecourseCode = getCode<RacecourseCode> fields "RacecourseCode"
                  Kai = getInt fields "Kai"
                  Day = getInt fields "Day"
                  RaceNumber = getInt fields "RaceNumber"
                  CreatedTime = getText fields "CreatedTime" |> Option.defaultValue ""
                  Predictions = predictions
                  DataCategory = getInt fields "DataCategory"
                  CreatedDate = getDate fields "CreatedDate" }
        }

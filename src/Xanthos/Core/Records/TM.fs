namespace Xanthos.Core.Records

open System
open Xanthos.Core
open Xanthos.Core.Text
open Xanthos.Core.Records.FieldDefinitions
open Xanthos.Core.Records.RecordParser
open Xanthos.Core.Records.CodeTables

/// TM Record: 対戦型データマイニング予想 (Match-based Data Mining Prediction)
/// Record Length: 141 bytes
module TM =

    /// Mining prediction entry (one per horse)
    type MiningPredictionEntry =
        { HorseNumber: int option // 馬番
          PredictionScore: decimal option } // 予測スコア (0.0-100.0)

    /// TM Record data
    type TMRecord =
        { Year: int // 開催年
          MonthDay: string // 開催月日 (mmdd)
          RacecourseCode: RacecourseCode option // 競馬場コード
          Kai: int option // 開催回
          Day: int option // 開催日目
          RaceNumber: int option // レース番号
          CreatedTime: string // データ作成時分 (HHmm)
          Predictions: MiningPredictionEntry list // マイニング予想 (最大18頭)
          DataCategory: int option // データ区分
          CreatedDate: DateTime option } // データ作成年月日

    /// TM record header field specifications (positions are 0-based byte offsets)
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

    /// Parse a single mining prediction entry from data at given offset
    let private parsePredictionEntry (data: byte[]) (baseOffset: int) : MiningPredictionEntry option =
        if baseOffset + 6 > data.Length then
            None
        else
            let entryData = data.[baseOffset .. baseOffset + 5]

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
                let score =
                    let text = decodeShiftJis entryData.[2..5] |> fun s -> s.Trim()

                    if String.IsNullOrWhiteSpace text then
                        None
                    else
                        // Score is formatted as 4 digits with implied decimal (e.g., "1000" = 100.0)
                        match Decimal.TryParse text with
                        | true, v -> Some(v / 10m)
                        | _ -> None

                Some
                    { HorseNumber = horseNumber
                      PredictionScore = score }

    /// Parse TM record from raw bytes
    let parse (data: byte[]) : Result<TMRecord, XanthosError> =
        parseRecord {
            let! fields = parseFieldsXanthos data headerFieldSpecs
            let! year = requireInt fields "Year"

            // Parse repeating prediction entries (18 entries starting at offset 31, each 6 bytes)
            let predictions =
                [ 0..17 ]
                |> List.map (fun i -> parsePredictionEntry data (31 + i * 6))
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

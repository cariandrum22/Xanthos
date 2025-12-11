namespace Xanthos.Core.Records

open System
open Xanthos.Core
open Xanthos.Core.Text
open Xanthos.Core.Records.FieldDefinitions
open Xanthos.Core.Records.RecordParser
open Xanthos.Core.Records.CodeTables

/// WH Record: 馬体重 (Horse Weight)
/// Record Length: 847 bytes
module WH =

    /// Horse weight entry (one per horse in race)
    type HorseWeightEntry =
        { HorseNumber: int option // 馬番
          HorseName: string // 馬名
          Weight: int option // 馬体重 (kg)
          WeightSign: string // 増減符号 (+/-/space)
          WeightChange: int option } // 増減差 (kg)

    /// WH Record data
    type WHRecord =
        { Year: int // 開催年
          MonthDay: string // 開催月日 (mmdd)
          RacecourseCode: RacecourseCode option // 競馬場コード
          Kai: int option // 開催回
          Day: int option // 開催日目
          RaceNumber: int option // レース番号
          AnnouncedTime: string // 発表月日時分 (MMddHHmm)
          HorseWeights: HorseWeightEntry list // 馬体重情報 (最大18頭)
          DataCategory: int option // データ区分
          CreatedDate: DateTime option } // データ作成年月日

    /// WH record header field specifications (positions are 0-based byte offsets)
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
          textRaw "AnnouncedTime" 27 8 ] // 発表月日時分 (position 28)

    /// Parse a single horse weight entry from data at given offset
    let private parseHorseWeightEntry (data: byte[]) (baseOffset: int) : HorseWeightEntry option =
        if baseOffset + 45 > data.Length then
            None
        else
            let entryData = data.[baseOffset .. baseOffset + 44]

            let horseNumber =
                let bytes = entryData.[0..1]
                let text = decodeShiftJis bytes |> fun s -> s.Trim()

                if String.IsNullOrWhiteSpace text then
                    None
                else
                    match Int32.TryParse text with
                    | true, v when v > 0 -> Some v
                    | _ -> None

            // Skip entries without horse number (empty slot)
            match horseNumber with
            | None -> None
            | Some _ ->
                let horseName =
                    decodeShiftJis entryData.[2..37] |> normalizeJvText |> (fun s -> s.Trim())

                let weight =
                    let text = decodeShiftJis entryData.[38..40] |> fun s -> s.Trim()

                    if String.IsNullOrWhiteSpace text then
                        None
                    else
                        match Int32.TryParse text with
                        | true, v when v > 0 && v < 999 -> Some v
                        | _ -> None

                let weightSign = decodeShiftJis entryData.[41..41] |> fun s -> s.Trim()

                let weightChange =
                    let text = decodeShiftJis entryData.[42..44] |> fun s -> s.Trim()

                    if String.IsNullOrWhiteSpace text then
                        None
                    else
                        match Int32.TryParse text with
                        | true, v when v >= 0 && v < 999 -> Some v
                        | _ -> None

                Some
                    { HorseNumber = horseNumber
                      HorseName = horseName
                      Weight = weight
                      WeightSign = weightSign
                      WeightChange = weightChange }

    /// Parse WH record from raw bytes
    let parse (data: byte[]) : Result<WHRecord, XanthosError> =
        parseRecord {
            let! fields = parseFieldsXanthos data headerFieldSpecs
            let! year = requireInt fields "Year"

            // Parse repeating horse weight entries (18 entries starting at offset 35)
            let horseWeights =
                [ 0..17 ]
                |> List.map (fun i -> parseHorseWeightEntry data (35 + i * 45))
                |> List.choose id

            return
                { Year = year
                  MonthDay = getText fields "MonthDay" |> Option.defaultValue ""
                  RacecourseCode = getCode<RacecourseCode> fields "RacecourseCode"
                  Kai = getInt fields "Kai"
                  Day = getInt fields "Day"
                  RaceNumber = getInt fields "RaceNumber"
                  AnnouncedTime = getText fields "AnnouncedTime" |> Option.defaultValue ""
                  HorseWeights = horseWeights
                  DataCategory = getInt fields "DataCategory"
                  CreatedDate = getDate fields "CreatedDate" }
        }

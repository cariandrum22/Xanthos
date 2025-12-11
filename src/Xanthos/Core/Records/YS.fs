namespace Xanthos.Core.Records

open System
open Xanthos.Core
open Xanthos.Core.Text
open Xanthos.Core.Records.FieldDefinitions
open Xanthos.Core.Records.RecordParser
open Xanthos.Core.Records.CodeTables

/// YS Record: 開催スケジュール (Event Schedule)
/// Record Length: 382 bytes
module YS =

    /// 曜日コード (Day of Week Code)
    type DayOfWeekCode =
        | Sunday = 0
        | Monday = 1
        | Tuesday = 2
        | Wednesday = 3
        | Thursday = 4
        | Friday = 5
        | Saturday = 6

    /// 重賞案内 (Grade Race Info Entry)
    type GradeRaceEntry =
        { SpecialRaceNumber: int option // 特別競走番号
          RaceNameMain: string // 競走名本題
          RaceNameAbbr10: string // 競走名略称10文字
          RaceNameAbbr6: string // 競走名略称6文字
          RaceNameAbbr3: string // 競走名略称3文字
          GradeRaceCount: int option // 重賞回次[第N回]
          GradeCode: string // グレードコード
          RaceTypeCode: string // 競走種別コード
          RaceSymbolCode: string // 競走記号コード
          WeightTypeCode: string // 重量種別コード
          Distance: int option // 距離
          TrackCode: string } // トラックコード

    /// YS Record data
    type YSRecord =
        { Year: int // 開催年
          MonthDay: string // 開催月日 (mmdd)
          RacecourseCode: RacecourseCode option // 競馬場コード
          Kai: int option // 開催回
          Day: int option // 開催日目
          DayOfWeek: DayOfWeekCode option // 曜日コード
          GradeRaces: GradeRaceEntry list // 重賞案内 (最大3件)
          DataCategory: int option // データ区分
          CreatedDate: DateTime option } // データ作成年月日

    /// YS record header field specifications (positions are 0-based byte offsets)
    let private headerFieldSpecs =
        [ textRaw "RecordType" 0 2 // レコード種別ID (position 1)
          int "DataCategory" 2 1 // データ区分 (position 3)
          date "CreatedDate" 3 8 "yyyyMMdd" // データ作成年月日 (position 4)
          int "Year" 11 4 // 開催年 (position 12)
          textRaw "MonthDay" 15 4 // 開催月日 (position 16)
          code "RacecourseCode" 19 2 "RacecourseCode" // 競馬場コード (position 20)
          int "Kai" 21 2 // 開催回 (position 22)
          int "Day" 23 2 // 開催日目 (position 24)
          code "DayOfWeek" 25 1 "DayOfWeekCode" ] // 曜日コード (position 26)

    /// Parse a single grade race entry from data at given offset
    let private parseGradeRaceEntry (data: byte[]) (baseOffset: int) : GradeRaceEntry option =
        if baseOffset + 118 > data.Length then
            None
        else
            let entryData = data.[baseOffset .. baseOffset + 117]

            let specialRaceNumber =
                let text = decodeShiftJis entryData.[0..3] |> fun s -> s.Trim()

                if String.IsNullOrWhiteSpace text then
                    None
                else
                    match Int32.TryParse text with
                    | true, v when v > 0 -> Some v
                    | _ -> None

            // Skip entries without special race number (empty slot)
            match specialRaceNumber with
            | None -> None
            | Some _ ->
                let raceNameMain =
                    decodeShiftJis entryData.[4..63] |> normalizeJvText |> (fun s -> s.Trim())

                let raceNameAbbr10 =
                    decodeShiftJis entryData.[64..83] |> normalizeJvText |> (fun s -> s.Trim())

                let raceNameAbbr6 =
                    decodeShiftJis entryData.[84..95] |> normalizeJvText |> (fun s -> s.Trim())

                let raceNameAbbr3 =
                    decodeShiftJis entryData.[96..101] |> normalizeJvText |> (fun s -> s.Trim())

                let gradeRaceCount =
                    let text = decodeShiftJis entryData.[102..104] |> fun s -> s.Trim()

                    if String.IsNullOrWhiteSpace text then
                        None
                    else
                        match Int32.TryParse text with
                        | true, v when v > 0 -> Some v
                        | _ -> None

                let gradeCode = decodeShiftJis entryData.[105..105] |> fun s -> s.Trim()
                let raceTypeCode = decodeShiftJis entryData.[106..107] |> fun s -> s.Trim()
                let raceSymbolCode = decodeShiftJis entryData.[108..110] |> fun s -> s.Trim()
                let weightTypeCode = decodeShiftJis entryData.[111..111] |> fun s -> s.Trim()

                let distance =
                    let text = decodeShiftJis entryData.[112..115] |> fun s -> s.Trim()

                    if String.IsNullOrWhiteSpace text then
                        None
                    else
                        match Int32.TryParse text with
                        | true, v when v > 0 -> Some v
                        | _ -> None

                let trackCode = decodeShiftJis entryData.[116..117] |> fun s -> s.Trim()

                Some
                    { SpecialRaceNumber = specialRaceNumber
                      RaceNameMain = raceNameMain
                      RaceNameAbbr10 = raceNameAbbr10
                      RaceNameAbbr6 = raceNameAbbr6
                      RaceNameAbbr3 = raceNameAbbr3
                      GradeRaceCount = gradeRaceCount
                      GradeCode = gradeCode
                      RaceTypeCode = raceTypeCode
                      RaceSymbolCode = raceSymbolCode
                      WeightTypeCode = weightTypeCode
                      Distance = distance
                      TrackCode = trackCode }

    /// Parse YS record from raw bytes
    let parse (data: byte[]) : Result<YSRecord, XanthosError> =
        parseRecord {
            let! fields = parseFieldsXanthos data headerFieldSpecs
            let! year = requireInt fields "Year"

            // Parse repeating grade race entries (3 entries starting at offset 26, each 118 bytes)
            let gradeRaces =
                [ 0..2 ]
                |> List.map (fun i -> parseGradeRaceEntry data (26 + i * 118))
                |> List.choose id

            return
                { Year = year
                  MonthDay = getText fields "MonthDay" |> Option.defaultValue ""
                  RacecourseCode = getCode<RacecourseCode> fields "RacecourseCode"
                  Kai = getInt fields "Kai"
                  Day = getInt fields "Day"
                  DayOfWeek = getCode<DayOfWeekCode> fields "DayOfWeek"
                  GradeRaces = gradeRaces
                  DataCategory = getInt fields "DataCategory"
                  CreatedDate = getDate fields "CreatedDate" }
        }

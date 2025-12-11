namespace Xanthos.Core.Records

open System
open Xanthos.Core
open Xanthos.Core.Text
open Xanthos.Core.Records.FieldDefinitions
open Xanthos.Core.Records.RecordParser
open Xanthos.Core.Records.CodeTables

/// CK Record: 出走別着度数 (Race Results by Start)
/// Record Length: 6870 bytes
module CK =

    /// 着回数 (Placement counts: 1st through 5th + outside)
    type PlacementCounts =
        { First: int option
          Second: int option
          Third: int option
          Fourth: int option
          Fifth: int option
          Outside: int option }

    /// 脚質傾向 (Running style tendency)
    type RunningStyleTendency =
        { Runaway: int option // 逃げ回数
          Front: int option // 先行回数
          Mid: int option // 差し回数
          Back: int option } // 追込回数

    /// 馬場別着回数 (Placement by track type)
    type TrackPlacements =
        { TurfStraight: PlacementCounts
          TurfRight: PlacementCounts
          TurfLeft: PlacementCounts
          DirtStraight: PlacementCounts
          DirtRight: PlacementCounts
          DirtLeft: PlacementCounts
          Jump: PlacementCounts }

    /// 馬場状態別着回数 (Placement by track condition)
    type ConditionPlacements =
        { TurfGood: PlacementCounts
          TurfSlightlyHeavy: PlacementCounts
          TurfHeavy: PlacementCounts
          TurfBad: PlacementCounts
          DirtGood: PlacementCounts
          DirtSlightlyHeavy: PlacementCounts
          DirtHeavy: PlacementCounts
          DirtBad: PlacementCounts
          JumpGood: PlacementCounts
          JumpSlightlyHeavy: PlacementCounts
          JumpHeavy: PlacementCounts
          JumpBad: PlacementCounts }

    /// 距離別着回数 (Placement by distance)
    type DistancePlacements =
        { Turf1200Under: PlacementCounts
          Turf1201to1400: PlacementCounts
          Turf1401to1600: PlacementCounts
          Turf1601to1800: PlacementCounts
          Turf1801to2000: PlacementCounts
          Turf2001to2200: PlacementCounts
          Turf2201to2400: PlacementCounts
          Turf2401to2800: PlacementCounts
          Turf2801Over: PlacementCounts
          Dirt1200Under: PlacementCounts
          Dirt1201to1400: PlacementCounts
          Dirt1401to1600: PlacementCounts
          Dirt1601to1800: PlacementCounts
          Dirt1801to2000: PlacementCounts
          Dirt2001to2200: PlacementCounts
          Dirt2201to2400: PlacementCounts
          Dirt2401to2800: PlacementCounts
          Dirt2801Over: PlacementCounts }

    /// 競馬場別着回数 (Placement by racecourse)
    type RacecoursePlacements =
        { SapporoTurf: PlacementCounts
          HakodateTurf: PlacementCounts
          FukushimaTurf: PlacementCounts
          NiigataTurf: PlacementCounts
          TokyoTurf: PlacementCounts
          NakayamaTurf: PlacementCounts
          ChukyoTurf: PlacementCounts
          KyotoTurf: PlacementCounts
          HanshinTurf: PlacementCounts
          KokuraTurf: PlacementCounts
          SapporoDirt: PlacementCounts
          HakodateDirt: PlacementCounts
          FukushimaDirt: PlacementCounts
          NiigataDirt: PlacementCounts
          TokyoDirt: PlacementCounts
          NakayamaDirt: PlacementCounts
          ChukyoDirt: PlacementCounts
          KyotoDirt: PlacementCounts
          HanshinDirt: PlacementCounts
          KokuraDirt: PlacementCounts
          SapporoJump: PlacementCounts
          HakodateJump: PlacementCounts
          FukushimaJump: PlacementCounts
          NiigataJump: PlacementCounts
          TokyoJump: PlacementCounts
          NakayamaJump: PlacementCounts
          ChukyoJump: PlacementCounts
          KyotoJump: PlacementCounts
          HanshinJump: PlacementCounts
          KokuraJump: PlacementCounts }

    /// 騎手/調教師 年度成績 (Jockey/Trainer year stats)
    type PersonYearStats =
        { Year: int option
          FlatPrize: string
          JumpPrize: string
          FlatBonus: string
          JumpBonus: string
          TurfPlacements: PlacementCounts
          DirtPlacements: PlacementCounts
          JumpPlacements: PlacementCounts
          DistancePlacements: DistancePlacements
          RacecoursePlacements: RacecoursePlacements }

    /// 騎手情報 (Jockey info)
    type JockeyInfo =
        { Code: string
          Name: string
          CurrentYearStats: PersonYearStats
          CumulativeStats: PersonYearStats }

    /// 調教師情報 (Trainer info)
    type TrainerInfo =
        { Code: string
          Name: string
          CurrentYearStats: PersonYearStats
          CumulativeStats: PersonYearStats }

    /// 馬主年度成績 (Owner year stats)
    type OwnerYearStats =
        { Year: int option
          Prize: string
          Bonus: string
          Placements: PlacementCounts }

    /// 馬主情報 (Owner info)
    type OwnerInfo =
        { Code: string
          NameWithCorp: string
          NameNoCorp: string
          CurrentYearStats: OwnerYearStats
          CumulativeStats: OwnerYearStats }

    /// 生産者情報 (Producer info)
    type ProducerInfo =
        { Code: string
          NameWithCorp: string
          NameNoCorp: string
          CurrentYearStats: OwnerYearStats
          CumulativeStats: OwnerYearStats }

    /// CK Record data
    type CKRecord =
        { Year: int // 開催年
          MonthDay: string // 開催月日 (mmdd)
          RacecourseCode: RacecourseCode option // 競馬場コード
          Kai: int option // 開催回
          Day: int option // 開催日目
          RaceNumber: int option // レース番号
          PedigreeRegNum: string // 血統登録番号
          HorseName: string // 馬名
          // Prize money totals
          FlatPrize: string // 平地本賞金累計 (百円)
          JumpPrize: string // 障害本賞金累計
          FlatBonus: string // 平地付加賞金累計
          JumpBonus: string // 障害付加賞金累計
          FlatEarned: string // 平地収得賞金累計
          JumpEarned: string // 障害収得賞金累計
          // Horse placements
          TotalPlacements: PlacementCounts // 総合着回数
          JraPlacements: PlacementCounts // 中央合計着回数
          TrackPlacements: TrackPlacements // 馬場別着回数
          ConditionPlacements: ConditionPlacements // 馬場状態別着回数
          DistancePlacements: DistancePlacements // 距離別着回数
          RacecoursePlacements: RacecoursePlacements // 競馬場別着回数
          RunningStyle: RunningStyleTendency // 脚質傾向
          RegisteredRaces: int option // 登録レース数
          // Related person info
          Jockey: JockeyInfo
          Trainer: TrainerInfo
          Owner: OwnerInfo
          Producer: ProducerInfo
          DataCategory: int option // データ区分
          CreatedDate: DateTime option } // データ作成年月日

    /// CK record header field specifications (positions are 0-based byte offsets)
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
          textRaw "PedigreeRegNum" 27 10 // 血統登録番号 (position 28)
          text "HorseName" 37 36 ] // 馬名 (position 38)

    // Helper to parse a single int from bytes
    let private parseInt (data: byte[]) (offset: int) (len: int) : int option =
        if offset + len > data.Length then
            None
        else
            let text = decodeShiftJis data.[offset .. offset + len - 1] |> fun s -> s.Trim()

            if String.IsNullOrWhiteSpace text then
                None
            else
                match Int32.TryParse text with
                | true, v -> Some v
                | _ -> None

    // Helper to parse text from bytes
    let private parseText (data: byte[]) (offset: int) (len: int) : string =
        if offset + len > data.Length then
            ""
        else
            decodeShiftJis data.[offset .. offset + len - 1] |> fun s -> s.Trim()

    // Helper to parse placement counts (6 repeating values)
    let private parsePlacements (data: byte[]) (offset: int) (width: int) : PlacementCounts =
        { First = parseInt data offset width
          Second = parseInt data (offset + width) width
          Third = parseInt data (offset + 2 * width) width
          Fourth = parseInt data (offset + 3 * width) width
          Fifth = parseInt data (offset + 4 * width) width
          Outside = parseInt data (offset + 5 * width) width }

    // Parse jockey/trainer stats at given offset (1220 bytes block)
    let private parsePersonYearStats (data: byte[]) (baseOffset: int) : PersonYearStats =
        { Year = parseInt data baseOffset 4
          FlatPrize = parseText data (baseOffset + 4) 10
          JumpPrize = parseText data (baseOffset + 14) 10
          FlatBonus = parseText data (baseOffset + 24) 10
          JumpBonus = parseText data (baseOffset + 34) 10
          TurfPlacements = parsePlacements data (baseOffset + 44) 5
          DirtPlacements = parsePlacements data (baseOffset + 74) 5
          JumpPlacements = parsePlacements data (baseOffset + 104) 4
          DistancePlacements =
            { Turf1200Under = parsePlacements data (baseOffset + 128) 4
              Turf1201to1400 = parsePlacements data (baseOffset + 152) 4
              Turf1401to1600 = parsePlacements data (baseOffset + 176) 4
              Turf1601to1800 = parsePlacements data (baseOffset + 200) 4
              Turf1801to2000 = parsePlacements data (baseOffset + 224) 4
              Turf2001to2200 = parsePlacements data (baseOffset + 248) 4
              Turf2201to2400 = parsePlacements data (baseOffset + 272) 4
              Turf2401to2800 = parsePlacements data (baseOffset + 296) 4
              Turf2801Over = parsePlacements data (baseOffset + 320) 4
              Dirt1200Under = parsePlacements data (baseOffset + 344) 4
              Dirt1201to1400 = parsePlacements data (baseOffset + 368) 4
              Dirt1401to1600 = parsePlacements data (baseOffset + 392) 4
              Dirt1601to1800 = parsePlacements data (baseOffset + 416) 4
              Dirt1801to2000 = parsePlacements data (baseOffset + 440) 4
              Dirt2001to2200 = parsePlacements data (baseOffset + 464) 4
              Dirt2201to2400 = parsePlacements data (baseOffset + 488) 4
              Dirt2401to2800 = parsePlacements data (baseOffset + 512) 4
              Dirt2801Over = parsePlacements data (baseOffset + 536) 4 }
          RacecoursePlacements =
            { SapporoTurf = parsePlacements data (baseOffset + 560) 4
              HakodateTurf = parsePlacements data (baseOffset + 584) 4
              FukushimaTurf = parsePlacements data (baseOffset + 608) 4
              NiigataTurf = parsePlacements data (baseOffset + 632) 4
              TokyoTurf = parsePlacements data (baseOffset + 656) 4
              NakayamaTurf = parsePlacements data (baseOffset + 680) 4
              ChukyoTurf = parsePlacements data (baseOffset + 704) 4
              KyotoTurf = parsePlacements data (baseOffset + 728) 4
              HanshinTurf = parsePlacements data (baseOffset + 752) 4
              KokuraTurf = parsePlacements data (baseOffset + 776) 4
              SapporoDirt = parsePlacements data (baseOffset + 800) 4
              HakodateDirt = parsePlacements data (baseOffset + 824) 4
              FukushimaDirt = parsePlacements data (baseOffset + 848) 4
              NiigataDirt = parsePlacements data (baseOffset + 872) 4
              TokyoDirt = parsePlacements data (baseOffset + 896) 4
              NakayamaDirt = parsePlacements data (baseOffset + 920) 4
              ChukyoDirt = parsePlacements data (baseOffset + 944) 4
              KyotoDirt = parsePlacements data (baseOffset + 968) 4
              HanshinDirt = parsePlacements data (baseOffset + 992) 4
              KokuraDirt = parsePlacements data (baseOffset + 1016) 4
              SapporoJump = parsePlacements data (baseOffset + 1040) 3
              HakodateJump = parsePlacements data (baseOffset + 1058) 3
              FukushimaJump = parsePlacements data (baseOffset + 1076) 3
              NiigataJump = parsePlacements data (baseOffset + 1094) 3
              TokyoJump = parsePlacements data (baseOffset + 1112) 3
              NakayamaJump = parsePlacements data (baseOffset + 1130) 3
              ChukyoJump = parsePlacements data (baseOffset + 1148) 3
              KyotoJump = parsePlacements data (baseOffset + 1166) 3
              HanshinJump = parsePlacements data (baseOffset + 1184) 3
              KokuraJump = parsePlacements data (baseOffset + 1202) 3 } }

    // Parse owner/producer stats (60 bytes block)
    let private parseOwnerYearStats (data: byte[]) (baseOffset: int) : OwnerYearStats =
        { Year = parseInt data baseOffset 4
          Prize = parseText data (baseOffset + 4) 10
          Bonus = parseText data (baseOffset + 14) 10
          Placements = parsePlacements data (baseOffset + 24) 6 }

    /// Parse CK record from raw bytes
    let parse (data: byte[]) : Result<CKRecord, XanthosError> =
        parseRecord {
            let! fields = parseFieldsXanthos data headerFieldSpecs
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
                  // Prize money (positions 74-127)
                  FlatPrize = parseText data 73 9
                  JumpPrize = parseText data 82 9
                  FlatBonus = parseText data 91 9
                  JumpBonus = parseText data 100 9
                  FlatEarned = parseText data 109 9
                  JumpEarned = parseText data 118 9
                  // Horse placements
                  TotalPlacements = parsePlacements data 127 3
                  JraPlacements = parsePlacements data 145 3
                  TrackPlacements =
                    { TurfStraight = parsePlacements data 163 3
                      TurfRight = parsePlacements data 181 3
                      TurfLeft = parsePlacements data 199 3
                      DirtStraight = parsePlacements data 217 3
                      DirtRight = parsePlacements data 235 3
                      DirtLeft = parsePlacements data 253 3
                      Jump = parsePlacements data 271 3 }
                  ConditionPlacements =
                    { TurfGood = parsePlacements data 289 3
                      TurfSlightlyHeavy = parsePlacements data 307 3
                      TurfHeavy = parsePlacements data 325 3
                      TurfBad = parsePlacements data 343 3
                      DirtGood = parsePlacements data 361 3
                      DirtSlightlyHeavy = parsePlacements data 379 3
                      DirtHeavy = parsePlacements data 397 3
                      DirtBad = parsePlacements data 415 3
                      JumpGood = parsePlacements data 433 3
                      JumpSlightlyHeavy = parsePlacements data 451 3
                      JumpHeavy = parsePlacements data 469 3
                      JumpBad = parsePlacements data 487 3 }
                  DistancePlacements =
                    { Turf1200Under = parsePlacements data 505 3
                      Turf1201to1400 = parsePlacements data 523 3
                      Turf1401to1600 = parsePlacements data 541 3
                      Turf1601to1800 = parsePlacements data 559 3
                      Turf1801to2000 = parsePlacements data 577 3
                      Turf2001to2200 = parsePlacements data 595 3
                      Turf2201to2400 = parsePlacements data 613 3
                      Turf2401to2800 = parsePlacements data 631 3
                      Turf2801Over = parsePlacements data 649 3
                      Dirt1200Under = parsePlacements data 667 3
                      Dirt1201to1400 = parsePlacements data 685 3
                      Dirt1401to1600 = parsePlacements data 703 3
                      Dirt1601to1800 = parsePlacements data 721 3
                      Dirt1801to2000 = parsePlacements data 739 3
                      Dirt2001to2200 = parsePlacements data 757 3
                      Dirt2201to2400 = parsePlacements data 775 3
                      Dirt2401to2800 = parsePlacements data 793 3
                      Dirt2801Over = parsePlacements data 811 3 }
                  RacecoursePlacements =
                    { SapporoTurf = parsePlacements data 829 3
                      HakodateTurf = parsePlacements data 847 3
                      FukushimaTurf = parsePlacements data 865 3
                      NiigataTurf = parsePlacements data 883 3
                      TokyoTurf = parsePlacements data 901 3
                      NakayamaTurf = parsePlacements data 919 3
                      ChukyoTurf = parsePlacements data 937 3
                      KyotoTurf = parsePlacements data 955 3
                      HanshinTurf = parsePlacements data 973 3
                      KokuraTurf = parsePlacements data 991 3
                      SapporoDirt = parsePlacements data 1009 3
                      HakodateDirt = parsePlacements data 1027 3
                      FukushimaDirt = parsePlacements data 1045 3
                      NiigataDirt = parsePlacements data 1063 3
                      TokyoDirt = parsePlacements data 1081 3
                      NakayamaDirt = parsePlacements data 1099 3
                      ChukyoDirt = parsePlacements data 1117 3
                      KyotoDirt = parsePlacements data 1135 3
                      HanshinDirt = parsePlacements data 1153 3
                      KokuraDirt = parsePlacements data 1171 3
                      SapporoJump = parsePlacements data 1189 3
                      HakodateJump = parsePlacements data 1207 3
                      FukushimaJump = parsePlacements data 1225 3
                      NiigataJump = parsePlacements data 1243 3
                      TokyoJump = parsePlacements data 1261 3
                      NakayamaJump = parsePlacements data 1279 3
                      ChukyoJump = parsePlacements data 1297 3
                      KyotoJump = parsePlacements data 1315 3
                      HanshinJump = parsePlacements data 1333 3
                      KokuraJump = parsePlacements data 1351 3 }
                  RunningStyle =
                    { Runaway = parseInt data 1369 3
                      Front = parseInt data 1372 3
                      Mid = parseInt data 1375 3
                      Back = parseInt data 1378 3 }
                  RegisteredRaces = parseInt data 1381 3
                  // Jockey info (starts at position 1385, 0-indexed: 1384)
                  Jockey =
                    { Code = parseText data 1384 5
                      Name = decodeShiftJis data.[1389..1422] |> normalizeJvText |> (fun s -> s.Trim())
                      CurrentYearStats = parsePersonYearStats data 1423
                      CumulativeStats = parsePersonYearStats data 2643 }
                  // Trainer info (starts at position 3864, 0-indexed: 3863)
                  Trainer =
                    { Code = parseText data 3863 5
                      Name = decodeShiftJis data.[3868..3901] |> normalizeJvText |> (fun s -> s.Trim())
                      CurrentYearStats = parsePersonYearStats data 3902
                      CumulativeStats = parsePersonYearStats data 5122 }
                  // Owner info (starts at position 6343, 0-indexed: 6342)
                  Owner =
                    { Code = parseText data 6342 6
                      NameWithCorp = decodeShiftJis data.[6348..6411] |> normalizeJvText |> (fun s -> s.Trim())
                      NameNoCorp = decodeShiftJis data.[6412..6475] |> normalizeJvText |> (fun s -> s.Trim())
                      CurrentYearStats = parseOwnerYearStats data 6476
                      CumulativeStats = parseOwnerYearStats data 6536 }
                  // Producer info (starts at position 6597, 0-indexed: 6596)
                  Producer =
                    { Code = parseText data 6596 8
                      NameWithCorp = decodeShiftJis data.[6604..6675] |> normalizeJvText |> (fun s -> s.Trim())
                      NameNoCorp = decodeShiftJis data.[6676..6747] |> normalizeJvText |> (fun s -> s.Trim())
                      CurrentYearStats = parseOwnerYearStats data 6748
                      CumulativeStats = parseOwnerYearStats data 6808 }
                  DataCategory = getInt fields "DataCategory"
                  CreatedDate = getDate fields "CreatedDate" }
        }

namespace Xanthos.Data

open Xanthos

/// Current-year or lifetime results at entry time. Counts are first through fifth, then lower placings.
type EntryCareerPerformance =
    { Year: Sourced<int option>
      FlatBasePrizeHundredYen: Sourced<decimal option>
      JumpBasePrizeHundredYen: Sourced<decimal option>
      FlatAddedPrizeHundredYen: Sourced<decimal option>
      JumpAddedPrizeHundredYen: Sourced<decimal option>
      Turf: Sourced<int option> array
      Dirt: Sourced<int option> array
      Jump: Sourced<int option> array
      TurfUpTo1200: Sourced<int option> array
      Turf1201To1400: Sourced<int option> array
      Turf1401To1600: Sourced<int option> array
      Turf1601To1800: Sourced<int option> array
      Turf1801To2000: Sourced<int option> array
      Turf2001To2200: Sourced<int option> array
      Turf2201To2400: Sourced<int option> array
      Turf2401To2800: Sourced<int option> array
      TurfOver2800: Sourced<int option> array
      DirtUpTo1200: Sourced<int option> array
      Dirt1201To1400: Sourced<int option> array
      Dirt1401To1600: Sourced<int option> array
      Dirt1601To1800: Sourced<int option> array
      Dirt1801To2000: Sourced<int option> array
      Dirt2001To2200: Sourced<int option> array
      Dirt2201To2400: Sourced<int option> array
      Dirt2401To2800: Sourced<int option> array
      DirtOver2800: Sourced<int option> array
      SapporoTurf: Sourced<int option> array
      HakodateTurf: Sourced<int option> array
      FukushimaTurf: Sourced<int option> array
      NiigataTurf: Sourced<int option> array
      TokyoTurf: Sourced<int option> array
      NakayamaTurf: Sourced<int option> array
      ChukyoTurf: Sourced<int option> array
      KyotoTurf: Sourced<int option> array
      HanshinTurf: Sourced<int option> array
      KokuraTurf: Sourced<int option> array
      SapporoDirt: Sourced<int option> array
      HakodateDirt: Sourced<int option> array
      FukushimaDirt: Sourced<int option> array
      NiigataDirt: Sourced<int option> array
      TokyoDirt: Sourced<int option> array
      NakayamaDirt: Sourced<int option> array
      ChukyoDirt: Sourced<int option> array
      KyotoDirt: Sourced<int option> array
      HanshinDirt: Sourced<int option> array
      KokuraDirt: Sourced<int option> array
      SapporoJump: Sourced<int option> array
      HakodateJump: Sourced<int option> array
      FukushimaJump: Sourced<int option> array
      NiigataJump: Sourced<int option> array
      TokyoJump: Sourced<int option> array
      NakayamaJump: Sourced<int option> array
      ChukyoJump: Sourced<int option> array
      KyotoJump: Sourced<int option> array
      HanshinJump: Sourced<int option> array
      KokuraJump: Sourced<int option> array }

/// A horse and its connections, with results as of this race entry.
type RaceFrequency =
    {
        Header: RecordHeader
        Format: IdentifierFormat
        Identity: RaceIdentity
        PedigreeId: string
        Name: string
        FlatBasePrizeHundredYen: Sourced<decimal option>
        JumpBasePrizeHundredYen: Sourced<decimal option>
        FlatAddedPrizeHundredYen: Sourced<decimal option>
        JumpAddedPrizeHundredYen: Sourced<decimal option>
        FlatEarningsHundredYen: Sourced<decimal option>
        JumpEarningsHundredYen: Sourced<decimal option>
        Overall: Sourced<int option> array
        Central: Sourced<int option> array
        TurfStraight: Sourced<int option> array
        TurfRight: Sourced<int option> array
        TurfLeft: Sourced<int option> array
        DirtStraight: Sourced<int option> array
        DirtRight: Sourced<int option> array
        DirtLeft: Sourced<int option> array
        Jump: Sourced<int option> array
        TurfFirm: Sourced<int option> array
        TurfGood: Sourced<int option> array
        TurfSoft: Sourced<int option> array
        TurfHeavy: Sourced<int option> array
        DirtFirm: Sourced<int option> array
        DirtGood: Sourced<int option> array
        DirtSoft: Sourced<int option> array
        DirtHeavy: Sourced<int option> array
        JumpFirm: Sourced<int option> array
        JumpGood: Sourced<int option> array
        JumpSoft: Sourced<int option> array
        JumpHeavy: Sourced<int option> array
        TurfUpTo1200: Sourced<int option> array
        Turf1201To1400: Sourced<int option> array
        Turf1401To1600: Sourced<int option> array
        Turf1601To1800: Sourced<int option> array
        Turf1801To2000: Sourced<int option> array
        Turf2001To2200: Sourced<int option> array
        Turf2201To2400: Sourced<int option> array
        Turf2401To2800: Sourced<int option> array
        TurfOver2800: Sourced<int option> array
        DirtUpTo1200: Sourced<int option> array
        Dirt1201To1400: Sourced<int option> array
        Dirt1401To1600: Sourced<int option> array
        Dirt1601To1800: Sourced<int option> array
        Dirt1801To2000: Sourced<int option> array
        Dirt2001To2200: Sourced<int option> array
        Dirt2201To2400: Sourced<int option> array
        Dirt2401To2800: Sourced<int option> array
        DirtOver2800: Sourced<int option> array
        SapporoTurf: Sourced<int option> array
        HakodateTurf: Sourced<int option> array
        FukushimaTurf: Sourced<int option> array
        NiigataTurf: Sourced<int option> array
        TokyoTurf: Sourced<int option> array
        NakayamaTurf: Sourced<int option> array
        ChukyoTurf: Sourced<int option> array
        KyotoTurf: Sourced<int option> array
        HanshinTurf: Sourced<int option> array
        KokuraTurf: Sourced<int option> array
        SapporoDirt: Sourced<int option> array
        HakodateDirt: Sourced<int option> array
        FukushimaDirt: Sourced<int option> array
        NiigataDirt: Sourced<int option> array
        TokyoDirt: Sourced<int option> array
        NakayamaDirt: Sourced<int option> array
        ChukyoDirt: Sourced<int option> array
        KyotoDirt: Sourced<int option> array
        HanshinDirt: Sourced<int option> array
        KokuraDirt: Sourced<int option> array
        SapporoJump: Sourced<int option> array
        HakodateJump: Sourced<int option> array
        FukushimaJump: Sourced<int option> array
        NiigataJump: Sourced<int option> array
        TokyoJump: Sourced<int option> array
        NakayamaJump: Sourced<int option> array
        ChukyoJump: Sourced<int option> array
        KyotoJump: Sourced<int option> array
        HanshinJump: Sourced<int option> array
        KokuraJump: Sourced<int option> array
        /// Leading, forward, midfield and closing styles, in that order.
        RunningStyle: Sourced<int option> array
        RecordedRaceCount: Sourced<int option>
        JockeyId: string
        JockeyName: string
        /// Current year, then lifetime.
        JockeyPerformances: EntryCareerPerformance array
        TrainerId: string
        TrainerName: string
        /// Current year, then lifetime.
        TrainerPerformances: EntryCareerPerformance array
        OwnerId: string
        OwnerName: string
        OwnerNameWithoutLegalForm: string
        /// Current year, then lifetime.
        OwnerPerformances: OwnershipPerformance array
        BreederId: string
        BreederName: string
        BreederNameWithoutLegalForm: string
        /// Current year, then lifetime.
        BreederPerformances: OwnershipPerformance array
        Raw: byte[]
    }

module internal RaceFrequencyParser =
    let private career (r: Reader) name start =
        Array.init 2 (fun i ->
            let p = start + i * 1220
            let n = $"{name}[{i}]."

            let counts suffix offset width =
                Array.init 6 (fun j -> r.Int ($"{n}{suffix}[{j}]") (p + offset + j * width) width)

            { Year = r.Int (n + "Year") (p + 0) 4
              FlatBasePrizeHundredYen = r.Number (n + "FlatBasePrizeHundredYen") (p + 4) 10 1M
              JumpBasePrizeHundredYen = r.Number (n + "JumpBasePrizeHundredYen") (p + 14) 10 1M
              FlatAddedPrizeHundredYen = r.Number (n + "FlatAddedPrizeHundredYen") (p + 24) 10 1M
              JumpAddedPrizeHundredYen = r.Number (n + "JumpAddedPrizeHundredYen") (p + 34) 10 1M
              Turf = counts "Turf" 44 5
              Dirt = counts "Dirt" 74 5
              Jump = counts "Jump" 104 4
              TurfUpTo1200 = counts "TurfUpTo1200" 128 4
              Turf1201To1400 = counts "Turf1201To1400" 152 4
              Turf1401To1600 = counts "Turf1401To1600" 176 4
              Turf1601To1800 = counts "Turf1601To1800" 200 4
              Turf1801To2000 = counts "Turf1801To2000" 224 4
              Turf2001To2200 = counts "Turf2001To2200" 248 4
              Turf2201To2400 = counts "Turf2201To2400" 272 4
              Turf2401To2800 = counts "Turf2401To2800" 296 4
              TurfOver2800 = counts "TurfOver2800" 320 4
              DirtUpTo1200 = counts "DirtUpTo1200" 344 4
              Dirt1201To1400 = counts "Dirt1201To1400" 368 4
              Dirt1401To1600 = counts "Dirt1401To1600" 392 4
              Dirt1601To1800 = counts "Dirt1601To1800" 416 4
              Dirt1801To2000 = counts "Dirt1801To2000" 440 4
              Dirt2001To2200 = counts "Dirt2001To2200" 464 4
              Dirt2201To2400 = counts "Dirt2201To2400" 488 4
              Dirt2401To2800 = counts "Dirt2401To2800" 512 4
              DirtOver2800 = counts "DirtOver2800" 536 4
              SapporoTurf = counts "SapporoTurf" 560 4
              HakodateTurf = counts "HakodateTurf" 584 4
              FukushimaTurf = counts "FukushimaTurf" 608 4
              NiigataTurf = counts "NiigataTurf" 632 4
              TokyoTurf = counts "TokyoTurf" 656 4
              NakayamaTurf = counts "NakayamaTurf" 680 4
              ChukyoTurf = counts "ChukyoTurf" 704 4
              KyotoTurf = counts "KyotoTurf" 728 4
              HanshinTurf = counts "HanshinTurf" 752 4
              KokuraTurf = counts "KokuraTurf" 776 4
              SapporoDirt = counts "SapporoDirt" 800 4
              HakodateDirt = counts "HakodateDirt" 824 4
              FukushimaDirt = counts "FukushimaDirt" 848 4
              NiigataDirt = counts "NiigataDirt" 872 4
              TokyoDirt = counts "TokyoDirt" 896 4
              NakayamaDirt = counts "NakayamaDirt" 920 4
              ChukyoDirt = counts "ChukyoDirt" 944 4
              KyotoDirt = counts "KyotoDirt" 968 4
              HanshinDirt = counts "HanshinDirt" 992 4
              KokuraDirt = counts "KokuraDirt" 1016 4
              SapporoJump = counts "SapporoJump" 1040 3
              HakodateJump = counts "HakodateJump" 1058 3
              FukushimaJump = counts "FukushimaJump" 1076 3
              NiigataJump = counts "NiigataJump" 1094 3
              TokyoJump = counts "TokyoJump" 1112 3
              NakayamaJump = counts "NakayamaJump" 1130 3
              ChukyoJump = counts "ChukyoJump" 1148 3
              KyotoJump = counts "KyotoJump" 1166 3
              HanshinJump = counts "HanshinJump" 1184 3
              KokuraJump = counts "KokuraJump" 1202 3 }
            : EntryCareerPerformance)

    let private ownership (r: Reader) name start =
        Array.init 2 (fun i ->
            let p = start + i * 60
            let n = $"{name}[{i}]."

            { Year = r.Int (n + "Year") p 4
              BasePrizeHundredYen = r.Number (n + "BasePrizeHundredYen") (p + 4) 10 1M
              AddedPrizeHundredYen = r.Number (n + "AddedPrizeHundredYen") (p + 14) 10 1M
              FinishCounts = Array.init 6 (fun j -> r.Int ($"{n}FinishCounts[{j}]") (p + 24 + j * 6) 6) })

    let parse format data =
        let widths =
            if format = IdentifierFormat.Legacy then
                [ 6597, 8, 6; 6605, 72, 70; 6677, 72, 70 ]
            else
                []

        Reader.parseWithWidths
            widths
            "CK"
            6870
            [ "0"; "1"; "2" ]
            (fun r header ->
                let counts name start =
                    Array.init 6 (fun i -> r.Int ($"{name}[{i}]") (start + i * 3) 3)

                { Header = header
                  Format = format
                  Identity = r.Identity 12
                  PedigreeId = r.Digits "PedigreeId" 28 10
                  Name = r.Text "Name" 38 36
                  FlatBasePrizeHundredYen = r.Number "FlatBasePrizeHundredYen" 74 9 1M
                  JumpBasePrizeHundredYen = r.Number "JumpBasePrizeHundredYen" 83 9 1M
                  FlatAddedPrizeHundredYen = r.Number "FlatAddedPrizeHundredYen" 92 9 1M
                  JumpAddedPrizeHundredYen = r.Number "JumpAddedPrizeHundredYen" 101 9 1M
                  FlatEarningsHundredYen = r.Number "FlatEarningsHundredYen" 110 9 1M
                  JumpEarningsHundredYen = r.Number "JumpEarningsHundredYen" 119 9 1M
                  Overall = counts "Overall" 128
                  Central = counts "Central" 146
                  TurfStraight = counts "TurfStraight" 164
                  TurfRight = counts "TurfRight" 182
                  TurfLeft = counts "TurfLeft" 200
                  DirtStraight = counts "DirtStraight" 218
                  DirtRight = counts "DirtRight" 236
                  DirtLeft = counts "DirtLeft" 254
                  Jump = counts "Jump" 272
                  TurfFirm = counts "TurfFirm" 290
                  TurfGood = counts "TurfGood" 308
                  TurfSoft = counts "TurfSoft" 326
                  TurfHeavy = counts "TurfHeavy" 344
                  DirtFirm = counts "DirtFirm" 362
                  DirtGood = counts "DirtGood" 380
                  DirtSoft = counts "DirtSoft" 398
                  DirtHeavy = counts "DirtHeavy" 416
                  JumpFirm = counts "JumpFirm" 434
                  JumpGood = counts "JumpGood" 452
                  JumpSoft = counts "JumpSoft" 470
                  JumpHeavy = counts "JumpHeavy" 488
                  TurfUpTo1200 = counts "TurfUpTo1200" 506
                  Turf1201To1400 = counts "Turf1201To1400" 524
                  Turf1401To1600 = counts "Turf1401To1600" 542
                  Turf1601To1800 = counts "Turf1601To1800" 560
                  Turf1801To2000 = counts "Turf1801To2000" 578
                  Turf2001To2200 = counts "Turf2001To2200" 596
                  Turf2201To2400 = counts "Turf2201To2400" 614
                  Turf2401To2800 = counts "Turf2401To2800" 632
                  TurfOver2800 = counts "TurfOver2800" 650
                  DirtUpTo1200 = counts "DirtUpTo1200" 668
                  Dirt1201To1400 = counts "Dirt1201To1400" 686
                  Dirt1401To1600 = counts "Dirt1401To1600" 704
                  Dirt1601To1800 = counts "Dirt1601To1800" 722
                  Dirt1801To2000 = counts "Dirt1801To2000" 740
                  Dirt2001To2200 = counts "Dirt2001To2200" 758
                  Dirt2201To2400 = counts "Dirt2201To2400" 776
                  Dirt2401To2800 = counts "Dirt2401To2800" 794
                  DirtOver2800 = counts "DirtOver2800" 812
                  SapporoTurf = counts "SapporoTurf" 830
                  HakodateTurf = counts "HakodateTurf" 848
                  FukushimaTurf = counts "FukushimaTurf" 866
                  NiigataTurf = counts "NiigataTurf" 884
                  TokyoTurf = counts "TokyoTurf" 902
                  NakayamaTurf = counts "NakayamaTurf" 920
                  ChukyoTurf = counts "ChukyoTurf" 938
                  KyotoTurf = counts "KyotoTurf" 956
                  HanshinTurf = counts "HanshinTurf" 974
                  KokuraTurf = counts "KokuraTurf" 992
                  SapporoDirt = counts "SapporoDirt" 1010
                  HakodateDirt = counts "HakodateDirt" 1028
                  FukushimaDirt = counts "FukushimaDirt" 1046
                  NiigataDirt = counts "NiigataDirt" 1064
                  TokyoDirt = counts "TokyoDirt" 1082
                  NakayamaDirt = counts "NakayamaDirt" 1100
                  ChukyoDirt = counts "ChukyoDirt" 1118
                  KyotoDirt = counts "KyotoDirt" 1136
                  HanshinDirt = counts "HanshinDirt" 1154
                  KokuraDirt = counts "KokuraDirt" 1172
                  SapporoJump = counts "SapporoJump" 1190
                  HakodateJump = counts "HakodateJump" 1208
                  FukushimaJump = counts "FukushimaJump" 1226
                  NiigataJump = counts "NiigataJump" 1244
                  TokyoJump = counts "TokyoJump" 1262
                  NakayamaJump = counts "NakayamaJump" 1280
                  ChukyoJump = counts "ChukyoJump" 1298
                  KyotoJump = counts "KyotoJump" 1316
                  HanshinJump = counts "HanshinJump" 1334
                  KokuraJump = counts "KokuraJump" 1352
                  RunningStyle = Array.init 4 (fun i -> r.Int ($"RunningStyle[{i}]") (1370 + i * 3) 3)
                  RecordedRaceCount = r.Int "RecordedRaceCount" 1382 3
                  JockeyId = r.Digits "JockeyId" 1385 5
                  JockeyName = r.Text "JockeyName" 1390 34
                  JockeyPerformances = career r "JockeyPerformances" 1424
                  TrainerId = r.Digits "TrainerId" 3864 5
                  TrainerName = r.Text "TrainerName" 3869 34
                  TrainerPerformances = career r "TrainerPerformances" 3903
                  OwnerId = r.Digits "OwnerId" 6343 6
                  OwnerName = r.Text "OwnerName" 6349 64
                  OwnerNameWithoutLegalForm = r.Text "OwnerNameWithoutLegalForm" 6413 64
                  OwnerPerformances = ownership r "OwnerPerformances" 6477
                  BreederId = r.Digits "BreederId" 6597 8
                  BreederName = r.Text "BreederName" 6605 72
                  BreederNameWithoutLegalForm = r.Text "BreederNameWithoutLegalForm" 6677 72
                  BreederPerformances = ownership r "BreederPerformances" 6749
                  Raw = Array.copy data })
            data

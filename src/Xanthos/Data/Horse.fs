namespace Xanthos.Data

open System
open Xanthos

type Ancestor = { BreedingId: string; Name: string }

/// Each array contains first through fifth place, then all lower placings.
type HorseFinishCounts =
    { Overall: Sourced<int option> array
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
      TurfYielding: Sourced<int option> array
      TurfSoft: Sourced<int option> array
      DirtFirm: Sourced<int option> array
      DirtGood: Sourced<int option> array
      DirtYielding: Sourced<int option> array
      DirtSoft: Sourced<int option> array
      JumpFirm: Sourced<int option> array
      JumpGood: Sourced<int option> array
      JumpYielding: Sourced<int option> array
      JumpSoft: Sourced<int option> array
      TurfUpTo1600: Sourced<int option> array
      Turf1601To2200: Sourced<int option> array
      TurfOver2200: Sourced<int option> array
      DirtUpTo1600: Sourced<int option> array
      Dirt1601To2200: Sourced<int option> array
      DirtOver2200: Sourced<int option> array }

type Horse =
    {
        Header: RecordHeader
        Format: IdentifierFormat
        PedigreeId: string
        Deregistered: Sourced<bool option>
        RegisteredDate: Sourced<DateOnly option>
        DeregisteredDate: Sourced<DateOnly option>
        BirthDate: Sourced<DateOnly option>
        Name: string
        KanaName: string
        EuropeanName: string
        AtJraFacility: Sourced<bool option>
        Symbol: OfficialCode
        Sex: OfficialCode
        Breed: OfficialCode
        Coat: OfficialCode
        /// Father, mother, then each generation from the paternal to maternal side.
        Ancestors: Ancestor array
        Affiliation: OfficialCode
        TrainerId: string
        TrainerAbbreviation: string
        InvitationRegion: string
        BreederId: string
        BreederName: string
        Birthplace: string
        OwnerId: string
        OwnerName: string
        FlatBasePrizeHundredYen: Sourced<decimal option>
        JumpBasePrizeHundredYen: Sourced<decimal option>
        FlatAddedPrizeHundredYen: Sourced<decimal option>
        JumpAddedPrizeHundredYen: Sourced<decimal option>
        FlatEarnedPrizeHundredYen: Sourced<decimal option>
        JumpEarnedPrizeHundredYen: Sourced<decimal option>
        FinishCounts: HorseFinishCounts
        /// Front running, early position, late charge, then closing from the rear.
        RunningStyleCounts: Sourced<int option> array
        RecordedRaceCount: Sourced<int option>
        Raw: byte[]
    }

module internal HorseParser =
    let parseWithFormat format data =
        let widths =
            match format with
            | IdentifierFormat.Expanded -> []
            | IdentifierFormat.Legacy ->
                [ for i in 0..13 do
                      yield 205 + i * 46, 10, 8
                  yield 883, 8, 6
                  yield 891, 72, 70 ]

        Reader.parseWithWidths
            widths
            "UM"
            1609
            [ "0"; "1"; "2"; "3"; "4"; "9" ]
            (fun r header ->
                let counts name p =
                    Array.init 6 (fun i -> r.Int ($"FinishCounts.{name}[{i}]") (p + i * 3) 3)

                { Header = header
                  Format = format
                  PedigreeId = r.Digits "PedigreeId" 12 10
                  Deregistered = BettingReader.flag r "Deregistered" 22
                  RegisteredDate = r.Date "RegisteredDate" 23
                  DeregisteredDate = r.Date "DeregisteredDate" 31
                  BirthDate = r.Date "BirthDate" 39
                  Name = r.Text "Name" 47 36
                  KanaName = r.Text "KanaName" 83 36
                  EuropeanName = r.Text "EuropeanName" 119 60
                  AtJraFacility = BettingReader.flag r "AtJraFacility" 179
                  Symbol = r.Code CodeTable.HorseSymbol "Symbol" 199
                  Sex = r.Code CodeTable.Sex "Sex" 201
                  Breed = r.Code CodeTable.Breed "Breed" 202
                  Coat = r.Code CodeTable.Coat "Coat" 203
                  Ancestors =
                    Array.init 14 (fun i ->
                        let p = 205 + i * 46

                        { BreedingId = r.Digits ($"Ancestors[{i}].BreedingId") p 10
                          Name = r.Text ($"Ancestors[{i}].Name") (p + 10) 36 })
                  Affiliation = r.Code CodeTable.Affiliation "Affiliation" 849
                  TrainerId = r.Digits "TrainerId" 850 5
                  TrainerAbbreviation = r.Text "TrainerAbbreviation" 855 8
                  InvitationRegion = r.Text "InvitationRegion" 863 20
                  BreederId = r.Digits "BreederId" 883 8
                  BreederName = r.Text "BreederName" 891 72
                  Birthplace = r.Text "Birthplace" 963 20
                  OwnerId = r.Digits "OwnerId" 983 6
                  OwnerName = r.Text "OwnerName" 989 64
                  FlatBasePrizeHundredYen = r.Number "FlatBasePrizeHundredYen" 1053 9 1M
                  JumpBasePrizeHundredYen = r.Number "JumpBasePrizeHundredYen" 1062 9 1M
                  FlatAddedPrizeHundredYen = r.Number "FlatAddedPrizeHundredYen" 1071 9 1M
                  JumpAddedPrizeHundredYen = r.Number "JumpAddedPrizeHundredYen" 1080 9 1M
                  FlatEarnedPrizeHundredYen = r.Number "FlatEarnedPrizeHundredYen" 1089 9 1M
                  JumpEarnedPrizeHundredYen = r.Number "JumpEarnedPrizeHundredYen" 1098 9 1M
                  FinishCounts =
                    { Overall = counts "Overall" 1107
                      Central = counts "Central" 1125
                      TurfStraight = counts "TurfStraight" 1143
                      TurfRight = counts "TurfRight" 1161
                      TurfLeft = counts "TurfLeft" 1179
                      DirtStraight = counts "DirtStraight" 1197
                      DirtRight = counts "DirtRight" 1215
                      DirtLeft = counts "DirtLeft" 1233
                      Jump = counts "Jump" 1251
                      TurfFirm = counts "TurfFirm" 1269
                      TurfGood = counts "TurfGood" 1287
                      TurfYielding = counts "TurfYielding" 1305
                      TurfSoft = counts "TurfSoft" 1323
                      DirtFirm = counts "DirtFirm" 1341
                      DirtGood = counts "DirtGood" 1359
                      DirtYielding = counts "DirtYielding" 1377
                      DirtSoft = counts "DirtSoft" 1395
                      JumpFirm = counts "JumpFirm" 1413
                      JumpGood = counts "JumpGood" 1431
                      JumpYielding = counts "JumpYielding" 1449
                      JumpSoft = counts "JumpSoft" 1467
                      TurfUpTo1600 = counts "TurfUpTo1600" 1485
                      Turf1601To2200 = counts "Turf1601To2200" 1503
                      TurfOver2200 = counts "TurfOver2200" 1521
                      DirtUpTo1600 = counts "DirtUpTo1600" 1539
                      Dirt1601To2200 = counts "Dirt1601To2200" 1557
                      DirtOver2200 = counts "DirtOver2200" 1575 }
                  RunningStyleCounts = Array.init 4 (fun i -> r.Int ($"RunningStyleCounts[{i}]") (1593 + i * 3) 3)
                  RecordedRaceCount = r.Int "RecordedRaceCount" 1605 3
                  Raw = Array.copy data })
            data

    let parse data =
        parseWithFormat IdentifierFormat.Expanded data

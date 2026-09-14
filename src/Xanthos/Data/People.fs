namespace Xanthos.Data

open System
open Xanthos

[<RequireQualifiedAccess>]
type PersonSex =
    | Male
    | Female
    | Unknown of string

/// Arrays contain first through fifth place, then all lower placings.
type DisciplineFinishCounts =
    { Flat: Sourced<int option> array
      Jump: Sourced<int option> array }

type CentralCourseResults =
    { Sapporo: DisciplineFinishCounts
      Hakodate: DisciplineFinishCounts
      Fukushima: DisciplineFinishCounts
      Niigata: DisciplineFinishCounts
      Tokyo: DisciplineFinishCounts
      Nakayama: DisciplineFinishCounts
      Chukyo: DisciplineFinishCounts
      Kyoto: DisciplineFinishCounts
      Hanshin: DisciplineFinishCounts
      Kokura: DisciplineFinishCounts }

type CareerPerformance =
    { Year: Sourced<int option>
      FlatBasePrizeHundredYen: Sourced<decimal option>
      JumpBasePrizeHundredYen: Sourced<decimal option>
      FlatAddedPrizeHundredYen: Sourced<decimal option>
      JumpAddedPrizeHundredYen: Sourced<decimal option>
      Overall: DisciplineFinishCounts
      Courses: CentralCourseResults
      TurfUpTo1600: Sourced<int option> array
      Turf1601To2200: Sourced<int option> array
      TurfOver2200: Sourced<int option> array
      DirtUpTo1600: Sourced<int option> array
      Dirt1601To2200: Sourced<int option> array
      DirtOver2200: Sourced<int option> array }

type GradedWin =
    { Identity: RaceIdentity
      Title: string
      Abbreviation10: string
      Abbreviation6: string
      Abbreviation3: string
      Grade: OfficialCode
      RunnerCount: Sourced<int option>
      PedigreeId: string
      HorseName: string }

type FirstRide =
    { Identity: RaceIdentity
      RunnerCount: Sourced<int option>
      PedigreeId: string
      HorseName: string
      FinishPosition: Sourced<int option>
      Abnormality: OfficialCode }

type FirstWin =
    { Identity: RaceIdentity
      RunnerCount: Sourced<int option>
      PedigreeId: string
      HorseName: string }

type Jockey =
    {
        Header: RecordHeader
        JockeyId: string
        Deregistered: Sourced<bool option>
        LicensedDate: Sourced<DateOnly option>
        DeregisteredDate: Sourced<DateOnly option>
        BirthDate: Sourced<DateOnly option>
        Name: string
        KanaName: string
        Abbreviation: string
        EuropeanName: string
        Sex: Sourced<PersonSex>
        Qualification: OfficialCode
        Apprentice: OfficialCode
        Affiliation: OfficialCode
        InvitationRegion: string
        TrainerId: string
        TrainerAbbreviation: string
        /// Flat, then jump racing.
        FirstRides: FirstRide array
        /// Flat, then jump racing.
        FirstWins: FirstWin array
        /// Most recent first; three slots.
        RecentGradedWins: GradedWin array
        /// Current year (retirement year if retired), preceding year, lifetime.
        Performances: CareerPerformance array
        Raw: byte[]
    }

type Trainer =
    {
        Header: RecordHeader
        TrainerId: string
        Deregistered: Sourced<bool option>
        LicensedDate: Sourced<DateOnly option>
        DeregisteredDate: Sourced<DateOnly option>
        BirthDate: Sourced<DateOnly option>
        Name: string
        KanaName: string
        Abbreviation: string
        EuropeanName: string
        Sex: Sourced<PersonSex>
        Affiliation: OfficialCode
        InvitationRegion: string
        /// Most recent first; three slots.
        RecentGradedWins: GradedWin array
        /// Current year (retirement year if retired), preceding year, lifetime.
        Performances: CareerPerformance array
        Raw: byte[]
    }

module internal PeopleParser =
    let private sex (r: Reader) p =
        let raw = r.Ascii "Sex" p 1

        { Raw = raw
          Value =
            match raw with
            | "1" -> PersonSex.Male
            | "2" -> PersonSex.Female
            | raw -> PersonSex.Unknown raw }

    let private wins (r: Reader) start =
        Array.init 3 (fun i ->
            let p = start + i * 163
            let name = $"RecentGradedWins[{i}]"

            { Identity = r.Identity p
              Title = r.Text (name + ".Title") (p + 16) 60
              Abbreviation10 = r.Text (name + ".Abbreviation10") (p + 76) 20
              Abbreviation6 = r.Text (name + ".Abbreviation6") (p + 96) 12
              Abbreviation3 = r.Text (name + ".Abbreviation3") (p + 108) 6
              Grade = r.Code CodeTable.Grade (name + ".Grade") (p + 114)
              RunnerCount = r.Int (name + ".RunnerCount") (p + 115) 2
              PedigreeId = r.Identifier true (name + ".PedigreeId") (p + 117) 10
              HorseName = r.Text (name + ".HorseName") (p + 127) 36 })

    let private performances (r: Reader) start =
        Array.init 3 (fun i ->
            let p = start + i * 1052
            let name = $"Performances[{i}]"

            let counts suffix offset =
                Array.init 6 (fun j -> r.Int ($"{name}.{suffix}[{j}]") (p + offset + j * 6) 6)

            let pair suffix offset =
                { Flat = counts (suffix + ".Flat") offset
                  Jump = counts (suffix + ".Jump") (offset + 36) }

            { Year = r.Int (name + ".Year") p 4
              FlatBasePrizeHundredYen = r.Number (name + ".FlatBasePrizeHundredYen") (p + 4) 10 1M
              JumpBasePrizeHundredYen = r.Number (name + ".JumpBasePrizeHundredYen") (p + 14) 10 1M
              FlatAddedPrizeHundredYen = r.Number (name + ".FlatAddedPrizeHundredYen") (p + 24) 10 1M
              JumpAddedPrizeHundredYen = r.Number (name + ".JumpAddedPrizeHundredYen") (p + 34) 10 1M
              Overall = pair "Overall" 44
              Courses =
                { Sapporo = pair "Courses.Sapporo" 116
                  Hakodate = pair "Courses.Hakodate" 188
                  Fukushima = pair "Courses.Fukushima" 260
                  Niigata = pair "Courses.Niigata" 332
                  Tokyo = pair "Courses.Tokyo" 404
                  Nakayama = pair "Courses.Nakayama" 476
                  Chukyo = pair "Courses.Chukyo" 548
                  Kyoto = pair "Courses.Kyoto" 620
                  Hanshin = pair "Courses.Hanshin" 692
                  Kokura = pair "Courses.Kokura" 764 }
              TurfUpTo1600 = counts "TurfUpTo1600" 836
              Turf1601To2200 = counts "Turf1601To2200" 872
              TurfOver2200 = counts "TurfOver2200" 908
              DirtUpTo1600 = counts "DirtUpTo1600" 944
              Dirt1601To2200 = counts "Dirt1601To2200" 980
              DirtOver2200 = counts "DirtOver2200" 1016 })

    let parseKS data =
        Reader.parse
            "KS"
            4173
            [ "0"; "1"; "2" ]
            (fun r header ->
                { Header = header
                  JockeyId = r.Digits "JockeyId" 12 5
                  Deregistered = BettingReader.flag r "Deregistered" 17
                  LicensedDate = r.Date "LicensedDate" 18
                  DeregisteredDate = r.Date "DeregisteredDate" 26
                  BirthDate = r.Date "BirthDate" 34
                  Name = r.Text "Name" 42 34
                  KanaName = r.Text "KanaName" 110 30
                  Abbreviation = r.Text "Abbreviation" 140 8
                  EuropeanName = r.Text "EuropeanName" 148 80
                  Sex = sex r 228
                  Qualification = r.Code CodeTable.JockeyQualification "Qualification" 229
                  Apprentice = r.Code CodeTable.Apprentice "Apprentice" 230
                  Affiliation = r.Code CodeTable.Affiliation "Affiliation" 231
                  InvitationRegion = r.Text "InvitationRegion" 232 20
                  TrainerId = r.Digits "TrainerId" 252 5
                  TrainerAbbreviation = r.Text "TrainerAbbreviation" 257 8
                  FirstRides =
                    Array.init 2 (fun i ->
                        let p = 265 + i * 67
                        let name = $"FirstRides[{i}]"

                        { Identity = r.Identity p
                          RunnerCount = r.Int (name + ".RunnerCount") (p + 16) 2
                          PedigreeId = r.Identifier true (name + ".PedigreeId") (p + 18) 10
                          HorseName = r.Text (name + ".HorseName") (p + 28) 36
                          FinishPosition = r.Int (name + ".FinishPosition") (p + 64) 2
                          Abnormality = r.Code CodeTable.Abnormality (name + ".Abnormality") (p + 66) })
                  FirstWins =
                    Array.init 2 (fun i ->
                        let p = 399 + i * 64
                        let name = $"FirstWins[{i}]"

                        { Identity = r.Identity p
                          RunnerCount = r.Int (name + ".RunnerCount") (p + 16) 2
                          PedigreeId = r.Identifier true (name + ".PedigreeId") (p + 18) 10
                          HorseName = r.Text (name + ".HorseName") (p + 28) 36 })
                  RecentGradedWins = wins r 527
                  Performances = performances r 1016
                  Raw = Array.copy data }
                : Jockey)
            data

    let parseCH data =
        Reader.parse
            "CH"
            3862
            [ "0"; "1"; "2" ]
            (fun r header ->
                { Header = header
                  TrainerId = r.Digits "TrainerId" 12 5
                  Deregistered = BettingReader.flag r "Deregistered" 17
                  LicensedDate = r.Date "LicensedDate" 18
                  DeregisteredDate = r.Date "DeregisteredDate" 26
                  BirthDate = r.Date "BirthDate" 34
                  Name = r.Text "Name" 42 34
                  KanaName = r.Text "KanaName" 76 30
                  Abbreviation = r.Text "Abbreviation" 106 8
                  EuropeanName = r.Text "EuropeanName" 114 80
                  Sex = sex r 194
                  Affiliation = r.Code CodeTable.Affiliation "Affiliation" 195
                  InvitationRegion = r.Text "InvitationRegion" 196 20
                  RecentGradedWins = wins r 216
                  Performances = performances r 705
                  Raw = Array.copy data }
                : Trainer)
            data

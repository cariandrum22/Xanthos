namespace Xanthos.Data

open System
open Xanthos

type ScheduledRace =
    { SpecialRaceNumber: Sourced<int option>
      Title: string
      Abbreviation10: string
      Abbreviation6: string
      Abbreviation3: string
      Edition: Sourced<int option>
      Grade: OfficialCode
      RaceKind: OfficialCode
      RaceSymbol: OfficialCode
      WeightRule: OfficialCode
      DistanceMetres: Sourced<int option>
      Track: OfficialCode }

type Schedule =
    { Header: RecordHeader
      Identity: MeetingIdentity
      Weekday: OfficialCode
      GradedRaces: ScheduledRace array
      Raw: byte[] }

type Lineage =
    {
        Header: RecordHeader
        Format: IdentifierFormat
        BreedingId: string
        /// Fifteen two-character ancestry levels, retained in source order.
        LineageId: string
        Name: string
        Description: string
        Raw: byte[]
    }

type Course =
    { Header: RecordHeader
      Racecourse: OfficialCode
      DistanceMetres: Sourced<int option>
      Track: OfficialCode
      RenovationDate: Sourced<DateOnly option>
      Description: string
      Raw: byte[] }

module internal ReferenceDataParser =
    let parseYS data =
        Reader.parse
            "YS"
            382
            [ "0"; "1"; "2"; "3"; "9" ]
            (fun r header ->
                let date = r.Date "Identity.Date" 12

                { Header = header
                  Identity =
                    { Raw = r.Ascii "Identity" 12 14
                      Year = r.Int "Identity.Year" 12 4
                      MonthDay = r.Digits "Identity.MonthDay" 16 4
                      Date = date.Value
                      Racecourse = r.Code CodeTable.Racecourse "Identity.Racecourse" 20
                      Meeting = r.Int "Identity.Meeting" 22 2
                      Day = r.Int "Identity.Day" 24 2 }
                  Weekday = r.Code CodeTable.Weekday "Weekday" 26
                  GradedRaces =
                    Array.init 3 (fun i ->
                        let p = 27 + i * 118
                        let n = $"GradedRaces[{i}]."

                        { SpecialRaceNumber = r.Int (n + "SpecialRaceNumber") p 4
                          Title = r.Text (n + "Title") (p + 4) 60
                          Abbreviation10 = r.Text (n + "Abbreviation10") (p + 64) 20
                          Abbreviation6 = r.Text (n + "Abbreviation6") (p + 84) 12
                          Abbreviation3 = r.Text (n + "Abbreviation3") (p + 96) 6
                          Edition = r.Int (n + "Edition") (p + 102) 3
                          Grade = r.Code CodeTable.Grade (n + "Grade") (p + 105)
                          RaceKind = r.Code CodeTable.RaceKind (n + "RaceKind") (p + 106)
                          RaceSymbol = r.Code CodeTable.RaceSymbol (n + "RaceSymbol") (p + 108)
                          WeightRule = r.Code CodeTable.WeightRule (n + "WeightRule") (p + 111)
                          DistanceMetres = r.Int (n + "DistanceMetres") (p + 112) 4
                          Track = r.Code CodeTable.Track (n + "Track") (p + 116) })
                  Raw = Array.copy data })
            data

    let parseBT format data =
        let widths =
            if format = IdentifierFormat.Legacy then
                [ 12, 10, 8 ]
            else
                []

        Reader.parseWithWidths
            widths
            "BT"
            6889
            [ "0"; "1"; "2" ]
            (fun r header ->
                { Header = header
                  Format = format
                  BreedingId = r.Digits "BreedingId" 12 10
                  LineageId = r.Ascii "LineageId" 22 30
                  Name = r.Text "Name" 52 36
                  Description = r.Text "Description" 88 6800
                  Raw = Array.copy data })
            data

    let parseCS data =
        Reader.parse
            "CS"
            6829
            [ "0"; "1"; "2" ]
            (fun r header ->
                { Header = header
                  Racecourse = r.Code CodeTable.Racecourse "Racecourse" 12
                  DistanceMetres = r.Int "DistanceMetres" 14 4
                  Track = r.Code CodeTable.Track "Track" 18
                  RenovationDate = r.Date "RenovationDate" 20
                  Description = r.Text "Description" 28 6800
                  Raw = Array.copy data })
            data

namespace Xanthos.Data

open System
open Xanthos

type MeetingIdentity =
    { Raw: string
      Year: Sourced<int option>
      MonthDay: string
      Date: DateOnly option
      Racecourse: OfficialCode
      Meeting: Sourced<int option>
      Day: Sourced<int option> }

[<RequireQualifiedAccess>]
type WeatherChangeKind =
    | Initial
    | Weather
    | TrackCondition
    | Unknown of string

type WeatherState =
    { Weather: OfficialCode
      Turf: OfficialCode
      Dirt: OfficialCode }

type WeatherChange =
    { Header: RecordHeader
      Identity: MeetingIdentity
      Announcement: Sourced<AnnouncementTime option>
      Change: Sourced<WeatherChangeKind>
      After: WeatherState
      Before: WeatherState
      Raw: byte[] }

[<RequireQualifiedAccess>]
type WithdrawalReason =
    | Unspecified
    | Illness
    | Accident
    | Other
    | Unknown of string

type Withdrawal =
    { Header: RecordHeader
      Identity: RaceIdentity
      Announcement: Sourced<AnnouncementTime option>
      HorseNumber: Sourced<int option>
      HorseName: string
      Reason: Sourced<WithdrawalReason>
      Raw: byte[] }

type AssignedJockey =
    { WeightKilograms: Sourced<decimal option>
      JockeyId: string
      Name: string
      Apprentice: OfficialCode }

type JockeyChange =
    { Header: RecordHeader
      Identity: RaceIdentity
      Announcement: Sourced<AnnouncementTime option>
      HorseNumber: Sourced<int option>
      HorseName: string
      After: AssignedJockey
      Before: AssignedJockey
      Raw: byte[] }

type TimeChange =
    { Header: RecordHeader
      Identity: RaceIdentity
      Announcement: Sourced<AnnouncementTime option>
      After: Sourced<TimeOnly option>
      Before: Sourced<TimeOnly option>
      Raw: byte[] }

type RaceCourse =
    { DistanceMetres: Sourced<int option>
      Track: OfficialCode }

[<RequireQualifiedAccess>]
type CourseChangeReason =
    | StrongWind
    | Typhoon
    | Snow
    | Other
    | Unknown of string

type CourseChange =
    { Header: RecordHeader
      Identity: RaceIdentity
      Announcement: Sourced<AnnouncementTime option>
      After: RaceCourse
      Before: RaceCourse
      Reason: Sourced<CourseChangeReason>
      Raw: byte[] }

module internal ChangesParser =
    let parseWE data =
        Reader.parse
            "WE"
            42
            [ "1" ]
            (fun r header ->
                let date = r.Date "Identity.Date" 12
                let change = r.Ascii "Change" 34 1

                let state name p =
                    { Weather = r.Code CodeTable.Weather (name + ".Weather") p
                      Turf = r.Code CodeTable.TrackCondition (name + ".Turf") (p + 1)
                      Dirt = r.Code CodeTable.TrackCondition (name + ".Dirt") (p + 2) }

                { Header = header
                  Identity =
                    { Raw = r.Ascii "Identity" 12 14
                      Year = r.Int "Identity.Year" 12 4
                      MonthDay = r.Digits "Identity.MonthDay" 16 4
                      Date = date.Value
                      Racecourse = r.Code CodeTable.Racecourse "Identity.Racecourse" 20
                      Meeting = r.Int "Identity.Meeting" 22 2
                      Day = r.Int "Identity.Day" 24 2 }
                  Announcement = r.Announcement "Announcement" 26
                  Change =
                    { Raw = change
                      Value =
                        match change with
                        | "1" -> WeatherChangeKind.Initial
                        | "2" -> WeatherChangeKind.Weather
                        | "3" -> WeatherChangeKind.TrackCondition
                        | raw -> WeatherChangeKind.Unknown raw }
                  After = state "After" 35
                  Before = state "Before" 38
                  Raw = Array.copy data }
                : WeatherChange)
            data

    let parseAV data =
        Reader.parse
            "AV"
            78
            [ "1"; "2" ]
            (fun r header ->
                let reason = r.Ascii "Reason" 74 3

                { Header = header
                  Identity = r.Identity 12
                  Announcement = r.Announcement "Announcement" 28
                  HorseNumber = r.Int "HorseNumber" 36 2
                  HorseName = r.Text "HorseName" 38 36
                  Reason =
                    { Raw = reason
                      Value =
                        match reason with
                        | "000"
                        | "   " -> WithdrawalReason.Unspecified
                        | "001" -> WithdrawalReason.Illness
                        | "002" -> WithdrawalReason.Accident
                        | "003" -> WithdrawalReason.Other
                        | raw -> WithdrawalReason.Unknown raw }
                  Raw = Array.copy data }
                : Withdrawal)
            data

    let parseJC data =
        Reader.parse
            "JC"
            161
            [ "1" ]
            (fun r header ->
                let jockey name p =
                    { WeightKilograms = r.Number (name + ".WeightKilograms") p 3 10M
                      JockeyId = r.Digits (name + ".JockeyId") (p + 3) 5
                      Name = r.Text (name + ".Name") (p + 8) 34
                      Apprentice = r.Code CodeTable.Apprentice (name + ".Apprentice") (p + 42) }

                { Header = header
                  Identity = r.Identity 12
                  Announcement = r.Announcement "Announcement" 28
                  HorseNumber = r.Int "HorseNumber" 36 2
                  HorseName = r.Text "HorseName" 38 36
                  After = jockey "After" 74
                  Before = jockey "Before" 117
                  Raw = Array.copy data }
                : JockeyChange)
            data

    let parseTC data =
        Reader.parse
            "TC"
            45
            [ "1" ]
            (fun r header ->
                { Header = header
                  Identity = r.Identity 12
                  Announcement = r.Announcement "Announcement" 28
                  After = r.ClockTime "After" 36
                  Before = r.ClockTime "Before" 40
                  Raw = Array.copy data }
                : TimeChange)
            data

    let parseCC data =
        Reader.parse
            "CC"
            50
            [ "1" ]
            (fun r header ->
                let course name p =
                    { DistanceMetres = r.Int (name + ".DistanceMetres") p 4
                      Track = r.Code CodeTable.Track (name + ".Track") (p + 4) }

                let reason = r.Ascii "Reason" 48 1

                { Header = header
                  Identity = r.Identity 12
                  Announcement = r.Announcement "Announcement" 28
                  After = course "After" 36
                  Before = course "Before" 42
                  Reason =
                    { Raw = reason
                      Value =
                        match reason with
                        | "1" -> CourseChangeReason.StrongWind
                        | "2" -> CourseChangeReason.Typhoon
                        | "3" -> CourseChangeReason.Snow
                        | "4" -> CourseChangeReason.Other
                        | raw -> CourseChangeReason.Unknown raw }
                  Raw = Array.copy data }
                : CourseChange)
            data

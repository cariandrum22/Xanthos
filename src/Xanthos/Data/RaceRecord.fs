namespace Xanthos.Data

open Xanthos

[<RequireQualifiedAccess>]
type RaceRecordKind =
    | Course
    | GradeOne
    | Unknown of string

[<RequireQualifiedAccess>]
type RecordTimeKind =
    | Standard
    | Record
    | Reference
    | Remark
    | Unknown of string

type RecordHolder =
    { PedigreeId: string
      Name: string
      Symbol: OfficialCode
      Sex: OfficialCode
      TrainerId: string
      TrainerName: string
      WeightKilograms: Sourced<decimal option>
      JockeyId: string
      JockeyName: string }

type RaceRecord =
    { Header: RecordHeader
      Kind: Sourced<RaceRecordKind>
      Identity: RaceIdentity
      SpecialRaceNumber: Sourced<int option>
      Title: string
      Grade: OfficialCode
      RaceKind: OfficialCode
      DistanceMetres: Sourced<int option>
      Track: OfficialCode
      TimeKind: Sourced<RecordTimeKind>
      TimeSeconds: Sourced<decimal option>
      Weather: OfficialCode
      Turf: OfficialCode
      Dirt: OfficialCode
      Holders: RecordHolder array
      Raw: byte[] }

module internal RaceRecordParser =
    let parse data =
        Reader.parse
            "RC"
            501
            [ "0"; "1" ]
            (fun r header ->
                let kind = r.Ascii "Kind" 12 1
                let timeKind = r.Ascii "TimeKind" 102 1
                let raw = r.Ascii "TimeSeconds" 103 4

                let time =
                    if raw = "0000" || raw = "    " then
                        None
                    else
                        r.Digits "TimeSeconds" 103 4 |> ignore
                        let minutes, seconds, tenths = int raw[..0], int raw[1..2], int raw[3..]

                        if seconds > 59 then
                            r.Fail "TimeSeconds" 103 4 "Seconds are outside 00–59."

                        Some(decimal (minutes * 60 + seconds) + decimal tenths / 10M)

                { Header = header
                  Kind =
                    { Raw = kind
                      Value =
                        match kind with
                        | "1" -> RaceRecordKind.Course
                        | "2" -> RaceRecordKind.GradeOne
                        | raw -> RaceRecordKind.Unknown raw }
                  Identity = r.Identity 13
                  SpecialRaceNumber = r.Int "SpecialRaceNumber" 29 4
                  Title = r.Text "Title" 33 60
                  Grade = r.Code CodeTable.Grade "Grade" 93
                  RaceKind = r.Code CodeTable.RaceKind "RaceKind" 94
                  DistanceMetres = r.Int "DistanceMetres" 96 4
                  Track = r.Code CodeTable.Track "Track" 100
                  TimeKind =
                    { Raw = timeKind
                      Value =
                        match timeKind with
                        | "1" -> RecordTimeKind.Standard
                        | "2" -> RecordTimeKind.Record
                        | "3" -> RecordTimeKind.Reference
                        | "4" -> RecordTimeKind.Remark
                        | raw -> RecordTimeKind.Unknown raw }
                  TimeSeconds = { Raw = raw; Value = time }
                  Weather = r.Code CodeTable.Weather "Weather" 107
                  Turf = r.Code CodeTable.TrackCondition "Turf" 108
                  Dirt = r.Code CodeTable.TrackCondition "Dirt" 109
                  Holders =
                    Array.init 3 (fun i ->
                        let p = 110 + i * 130
                        let name = $"Holders[{i}]"

                        { PedigreeId = r.Identifier true (name + ".PedigreeId") p 10
                          Name = r.Text (name + ".Name") (p + 10) 36
                          Symbol = r.Code CodeTable.HorseSymbol (name + ".Symbol") (p + 46)
                          Sex = r.Code CodeTable.Sex (name + ".Sex") (p + 48)
                          TrainerId = r.Identifier true (name + ".TrainerId") (p + 49) 5
                          TrainerName = r.Text (name + ".TrainerName") (p + 54) 34
                          WeightKilograms = r.Number (name + ".WeightKilograms") (p + 88) 3 10M
                          JockeyId = r.Identifier true (name + ".JockeyId") (p + 91) 5
                          JockeyName = r.Text (name + ".JockeyName") (p + 96) 34 })
                  Raw = Array.copy data })
            data

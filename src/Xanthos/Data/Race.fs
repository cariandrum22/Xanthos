namespace Xanthos.Data

open System
open Xanthos

type CornerOrder =
    { Corner: Sourced<int option>
      Lap: Sourced<int option>
      Order: string }

type Race =
    { Header: RecordHeader
      Identity: RaceIdentity
      Name: RaceName
      Grade: OfficialCode
      PreviousGrade: OfficialCode
      Conditions: RaceConditions
      ConditionName: string
      Distance: Sourced<int option>
      PreviousDistance: Sourced<int option>
      Track: OfficialCode
      PreviousTrack: OfficialCode
      CourseCategory: string
      PreviousCourseCategory: string
      PrizesHundredYen: Sourced<decimal option> array
      PreviousPrizesHundredYen: Sourced<decimal option> array
      AddedPrizesHundredYen: Sourced<decimal option> array
      PreviousAddedPrizesHundredYen: Sourced<decimal option> array
      StartTime: Sourced<TimeOnly option>
      PreviousStartTime: Sourced<TimeOnly option>
      RegisteredCount: Sourced<int option>
      RunnerCount: Sourced<int option>
      FinisherCount: Sourced<int option>
      Weather: OfficialCode
      TurfCondition: OfficialCode
      DirtCondition: OfficialCode
      LapSeconds: Sourced<decimal option> array
      ObstacleMileSeconds: Sourced<decimal option>
      FirstThreeFurlongSeconds: Sourced<decimal option>
      FirstFourFurlongSeconds: Sourced<decimal option>
      LastThreeFurlongSeconds: Sourced<decimal option>
      LastFourFurlongSeconds: Sourced<decimal option>
      Corners: CornerOrder array
      RecordUpdateCategory: string
      Raw: byte[] }

module internal RaceParser =
    let private clock (reader: Reader) name position =
        let raw = reader.Ascii name position 4

        if raw = "0000" || raw = "    " then
            { Raw = raw; Value = None }
        else
            match
                TimeOnly.TryParseExact(
                    raw,
                    "HHmm",
                    Globalization.CultureInfo.InvariantCulture,
                    Globalization.DateTimeStyles.None
                )
            with
            | true, time -> { Raw = raw; Value = Some time }
            | _ -> reader.Fail name position 4 "Invalid hhmm time."

    let private mileSeconds (reader: Reader) =
        let value = reader.Number "ObstacleMileSeconds" 966 4 1M

        let duration =
            value.Value
            |> Option.map (fun number ->
                let minutes = Decimal.Truncate(number / 1000M)
                let seconds = (number - minutes * 1000M) / 10M

                if seconds >= 60M then
                    reader.Fail "ObstacleMileSeconds" 966 4 "Invalid mssS elapsed time."

                minutes * 60M + seconds)

        { Raw = value.Raw; Value = duration }

    let parse (data: byte[]) =
        Reader.parse
            "RA"
            1272
            [ "0"; "1"; "2"; "3"; "4"; "5"; "6"; "7"; "9"; "A"; "B" ]
            (fun reader header ->
                let numbers name position length count scale =
                    Array.init count (fun i -> reader.Number ($"{name}[{i}]") (position + length * i) length scale)

                { Header = header
                  Identity = reader.Identity 12
                  Name = reader.RaceName 28
                  Grade = reader.Code CodeTable.Grade "Grade" 615
                  PreviousGrade = reader.Code CodeTable.Grade "PreviousGrade" 616
                  Conditions = reader.Conditions 617
                  ConditionName = reader.Text "ConditionName" 638 60
                  Distance = reader.Int "Distance" 698 4
                  PreviousDistance = reader.Int "PreviousDistance" 702 4
                  Track = reader.Code CodeTable.Track "Track" 706
                  PreviousTrack = reader.Code CodeTable.Track "PreviousTrack" 708
                  CourseCategory = reader.Ascii "CourseCategory" 710 2
                  PreviousCourseCategory = reader.Ascii "PreviousCourseCategory" 712 2
                  PrizesHundredYen = numbers "PrizesHundredYen" 714 8 7 1M
                  PreviousPrizesHundredYen = numbers "PreviousPrizesHundredYen" 770 8 5 1M
                  AddedPrizesHundredYen = numbers "AddedPrizesHundredYen" 810 8 5 1M
                  PreviousAddedPrizesHundredYen = numbers "PreviousAddedPrizesHundredYen" 850 8 3 1M
                  StartTime = clock reader "StartTime" 874
                  PreviousStartTime = clock reader "PreviousStartTime" 878
                  RegisteredCount = reader.Int "RegisteredCount" 882 2
                  RunnerCount = reader.Int "RunnerCount" 884 2
                  FinisherCount = reader.Int "FinisherCount" 886 2
                  Weather = reader.Code CodeTable.Weather "Weather" 888
                  TurfCondition = reader.Code CodeTable.TrackCondition "TurfCondition" 889
                  DirtCondition = reader.Code CodeTable.TrackCondition "DirtCondition" 890
                  LapSeconds = numbers "LapSeconds" 891 3 25 10M
                  ObstacleMileSeconds = mileSeconds reader
                  FirstThreeFurlongSeconds = reader.Number "FirstThreeFurlongSeconds" 970 3 10M
                  FirstFourFurlongSeconds = reader.Number "FirstFourFurlongSeconds" 973 3 10M
                  LastThreeFurlongSeconds = reader.Number "LastThreeFurlongSeconds" 976 3 10M
                  LastFourFurlongSeconds = reader.Number "LastFourFurlongSeconds" 979 3 10M
                  Corners =
                    Array.init 4 (fun i ->
                        let offset = 981 + i * 72

                        { Corner = reader.Int ($"Corners[{i}].Corner") (offset + 1) 1
                          Lap = reader.Int ($"Corners[{i}].Lap") (offset + 2) 1
                          Order = reader.Text ($"Corners[{i}].Order") (offset + 3) 70 })
                  RecordUpdateCategory = reader.Ascii "RecordUpdateCategory" 1270 1
                  Raw = Array.copy data })
            data

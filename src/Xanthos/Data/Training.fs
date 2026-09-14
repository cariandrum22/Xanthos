namespace Xanthos.Data

open System
open Xanthos

[<RequireQualifiedAccess>]
type TrainingCentre =
    | Miho
    | Ritto
    | Unknown of string

[<RequireQualifiedAccess>]
type TrainingTime =
    | Missing
    | Unmeasurable
    | Seconds of decimal
    | AtLeastSeconds of decimal

[<RequireQualifiedAccess>]
type TrainingCourse =
    | A
    | B
    | C
    | D
    | E
    | Unknown of string

[<RequireQualifiedAccess>]
type TrainingDirection =
    | Right
    | Left
    | Unknown of string

type HillTraining =
    {
        Header: RecordHeader
        Centre: Sourced<TrainingCentre>
        Date: Sourced<DateOnly option>
        Time: Sourced<TimeOnly option>
        PedigreeId: string
        /// Remaining times from 800, 600 and 400 metres to the finish.
        Totals: Sourced<TrainingTime> array
        /// The four 200-metre sections, furthest from the finish first.
        Laps: Sourced<TrainingTime> array
        Raw: byte[]
    }

type WoodchipTraining =
    {
        Header: RecordHeader
        Centre: Sourced<TrainingCentre>
        Date: Sourced<DateOnly option>
        Time: Sourced<TimeOnly option>
        PedigreeId: string
        Course: Sourced<TrainingCourse>
        Direction: Sourced<TrainingDirection>
        /// Remaining times from 2000 through 400 metres, at 200-metre intervals.
        Totals: Sourced<TrainingTime> array
        /// The ten 200-metre sections, furthest from the finish first.
        Laps: Sourced<TrainingTime> array
        Raw: byte[]
    }

module internal TrainingParser =
    let private centre (r: Reader) =
        let raw = r.Ascii "Centre" 12 1

        { Raw = raw
          Value =
            match raw with
            | "0" -> TrainingCentre.Miho
            | "1" -> TrainingCentre.Ritto
            | raw -> TrainingCentre.Unknown raw }

    let private time capped (r: Reader) name p length =
        let number = r.Number name p length 10M

        { Raw = number.Raw
          Value =
            match number.Value with
            | None -> TrainingTime.Missing
            | Some 0M -> TrainingTime.Unmeasurable
            | Some n when capped && number.Raw = String('9', length) -> TrainingTime.AtLeastSeconds n
            | Some n -> TrainingTime.Seconds n }

    let parseHC data =
        Reader.parse
            "HC"
            60
            [ "0"; "1" ]
            (fun r header ->
                { Header = header
                  Centre = centre r
                  Date = r.Date "Date" 13
                  Time = r.ClockTime "Time" 21
                  PedigreeId = r.Digits "PedigreeId" 25 10
                  Totals = Array.init 3 (fun i -> time false r ($"Totals[{i}]") (35 + i * 7) 4)
                  Laps = Array.init 4 (fun i -> time false r ($"Laps[{i}]") (if i = 3 then 56 else 39 + i * 7) 3)
                  Raw = Array.copy data }
                : HillTraining)
            data

    let parseWC data =
        Reader.parse
            "WC"
            105
            [ "0"; "1" ]
            (fun r header ->
                let course = r.Ascii "Course" 35 1
                let direction = r.Ascii "Direction" 36 1

                { Header = header
                  Centre = centre r
                  Date = r.Date "Date" 13
                  Time = r.ClockTime "Time" 21
                  PedigreeId = r.Digits "PedigreeId" 25 10
                  Course =
                    { Raw = course
                      Value =
                        match course with
                        | "0" -> TrainingCourse.A
                        | "1" -> TrainingCourse.B
                        | "2" -> TrainingCourse.C
                        | "3" -> TrainingCourse.D
                        | "4" -> TrainingCourse.E
                        | raw -> TrainingCourse.Unknown raw }
                  Direction =
                    { Raw = direction
                      Value =
                        match direction with
                        | "0" -> TrainingDirection.Right
                        | "1" -> TrainingDirection.Left
                        | raw -> TrainingDirection.Unknown raw }
                  Totals = Array.init 9 (fun i -> time true r ($"Totals[{i}]") (38 + i * 7) 4)
                  Laps = Array.init 10 (fun i -> time true r ($"Laps[{i}]") (if i = 9 then 101 else 42 + i * 7) 3)
                  Raw = Array.copy data }
                : WoodchipTraining)
            data

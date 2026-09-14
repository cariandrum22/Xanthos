namespace Xanthos.Data

open System
open Xanthos

[<RequireQualifiedAccess>]
type BodyWeight =
    | Missing
    | Withdrawn
    | Unmeasurable
    | Kilograms of int

[<RequireQualifiedAccess>]
type WeightChange =
    | Missing
    | Unmeasurable
    | Kilograms of int

[<RequireQualifiedAccess>]
type TimeGap =
    | Unset
    | NoResult
    | Seconds of decimal

[<RequireQualifiedAccess>]
type SectionalTime =
    | NotRecorded
    | NoResult
    | Seconds of decimal

type Opponent = { PedigreeId: string; Name: string }

type Runner =
    { Header: RecordHeader
      Identity: RaceIdentity
      Bracket: Sourced<int option>
      HorseNumber: Sourced<int option>
      PedigreeId: string
      Name: string
      HorseSymbol: OfficialCode
      Sex: OfficialCode
      Breed: OfficialCode
      Coat: OfficialCode
      Age: Sourced<int option>
      Affiliation: OfficialCode
      TrainerCode: string
      TrainerName: string
      OwnerCode: string
      OwnerName: string
      SilksDescription: string
      AssignedWeight: Sourced<decimal option>
      PreviousAssignedWeight: Sourced<decimal option>
      BlinkersCategory: string
      JockeyCode: string
      PreviousJockeyCode: string
      JockeyName: string
      PreviousJockeyName: string
      Apprentice: OfficialCode
      PreviousApprentice: OfficialCode
      BodyWeight: Sourced<BodyWeight>
      WeightChangeSign: string
      WeightChange: Sourced<WeightChange>
      Abnormality: OfficialCode
      FinishPosition: Sourced<int option>
      ConfirmedPosition: Sourced<int option>
      DeadHeatCategory: string
      DeadHeatCount: Sourced<int option>
      FinishSeconds: Sourced<decimal option>
      Margin: OfficialCode
      AdditionalMargin: OfficialCode
      ThirdMargin: OfficialCode
      CornerPositions: Sourced<int option> array
      WinOdds: Sourced<decimal option>
      Popularity: Sourced<int option>
      PrizeHundredYen: Sourced<decimal option>
      AddedPrizeHundredYen: Sourced<decimal option>
      LastFourFurlongSeconds: Sourced<SectionalTime>
      LastThreeFurlongSeconds: Sourced<SectionalTime>
      Opponents: Opponent array
      TimeGap: Sourced<TimeGap>
      RecordUpdateCategory: string
      MiningCategory: string
      PredictedFinishSeconds: Sourced<decimal option>
      PredictionFasterErrorSeconds: Sourced<decimal option>
      PredictionSlowerErrorSeconds: Sourced<decimal option>
      PredictedPosition: Sourced<int option>
      RunningStyle: string
      Raw: byte[] }

module internal RunnerParser =
    let private sectional (reader: Reader) name position =
        let number = reader.Number name position 3 10M

        { Raw = number.Raw
          Value =
            match number.Raw, number.Value with
            | "999", _ -> SectionalTime.NoResult
            | "000", _
            | _, None -> SectionalTime.NotRecorded
            | _, Some value -> SectionalTime.Seconds value }

    let private elapsed (reader: Reader) name position length fraction =
        let number = reader.Number name position length 1M

        let converted =
            number.Value
            |> Option.map (fun n ->
                let minutes = Decimal.Truncate(n / (100M * fraction))
                let seconds = (n - minutes * 100M * fraction) / fraction

                if seconds >= 60M then
                    reader.Fail name position length "Invalid minute/second elapsed time."

                minutes * 60M + seconds)

        { Raw = number.Raw; Value = converted }

    let private weight (reader: Reader) =
        let number = reader.Int "BodyWeight" 325 3

        let weight =
            match number.Value with
            | None -> BodyWeight.Missing
            | Some 0 -> BodyWeight.Withdrawn
            | Some 999 -> BodyWeight.Unmeasurable
            | Some n when n >= 2 && n <= 998 -> BodyWeight.Kilograms n
            | _ -> reader.Fail "BodyWeight" 325 3 "Expected 002–998, 000, 999 or a blank value."

        { Raw = number.Raw; Value = weight }

    let private weightChange sign (reader: Reader) =
        let number = reader.Int "WeightChange" 329 3

        let change =
            match number.Value with
            | None -> WeightChange.Missing
            | Some 999 -> WeightChange.Unmeasurable
            | Some n -> WeightChange.Kilograms(if sign = "-" then -n else n)

        { Raw = number.Raw; Value = change }

    let private gap (reader: Reader) =
        let raw = reader.Ascii "TimeGap" 532 4

        let result =
            match raw with
            | "9999" -> TimeGap.NoResult
            | "0000"
            | "    " -> TimeGap.Unset
            | _ ->
                if
                    (raw[0] <> '+' && raw[0] <> '-')
                    || raw.Substring(1) |> Seq.exists (fun c -> c < '0' || c > '9')
                then
                    reader.Fail "TimeGap" 532 4 "Expected signed tenths of seconds or a defined special value."

                TimeGap.Seconds(Decimal.Parse(raw, Globalization.CultureInfo.InvariantCulture) / 10M)

        { Raw = raw; Value = result }

    let parse (data: byte[]) =
        Reader.parse
            "SE"
            555
            [ "0"; "1"; "2"; "3"; "4"; "5"; "6"; "7"; "9"; "A"; "B" ]
            (fun reader header ->
                let sign = reader.Ascii "WeightChangeSign" 328 1

                if sign <> "+" && sign <> "-" && sign <> " " then
                    reader.Fail "WeightChangeSign" 328 1 "Unknown weight-change sign."

                { Header = header
                  Identity = reader.Identity 12
                  Bracket = reader.Int "Bracket" 28 1
                  HorseNumber = reader.Int "HorseNumber" 29 2
                  PedigreeId = reader.Digits "PedigreeId" 31 10
                  Name = reader.Text "Name" 41 36
                  HorseSymbol = reader.Code CodeTable.HorseSymbol "HorseSymbol" 77
                  Sex = reader.Code CodeTable.Sex "Sex" 79
                  Breed = reader.Code CodeTable.Breed "Breed" 80
                  Coat = reader.Code CodeTable.Coat "Coat" 81
                  Age = reader.Int "Age" 83 2
                  Affiliation = reader.Code CodeTable.Affiliation "Affiliation" 85
                  TrainerCode = reader.Digits "TrainerCode" 86 5
                  TrainerName = reader.Text "TrainerName" 91 8
                  OwnerCode = reader.Digits "OwnerCode" 99 6
                  OwnerName = reader.Text "OwnerName" 105 64
                  SilksDescription = reader.Text "SilksDescription" 169 60
                  AssignedWeight = reader.Number "AssignedWeight" 289 3 10M
                  PreviousAssignedWeight = reader.Number "PreviousAssignedWeight" 292 3 10M
                  BlinkersCategory = reader.Ascii "BlinkersCategory" 295 1
                  JockeyCode = reader.Digits "JockeyCode" 297 5
                  PreviousJockeyCode = reader.Digits "PreviousJockeyCode" 302 5
                  JockeyName = reader.Text "JockeyName" 307 8
                  PreviousJockeyName = reader.Text "PreviousJockeyName" 315 8
                  Apprentice = reader.Code CodeTable.Apprentice "Apprentice" 323
                  PreviousApprentice = reader.Code CodeTable.Apprentice "PreviousApprentice" 324
                  BodyWeight = weight reader
                  WeightChangeSign = sign
                  WeightChange = weightChange sign reader
                  Abnormality = reader.Code CodeTable.Abnormality "Abnormality" 332
                  FinishPosition = reader.Int "FinishPosition" 333 2
                  ConfirmedPosition = reader.Int "ConfirmedPosition" 335 2
                  DeadHeatCategory = reader.Ascii "DeadHeatCategory" 337 1
                  DeadHeatCount = reader.Int "DeadHeatCount" 338 1
                  FinishSeconds = elapsed reader "FinishSeconds" 339 4 10M
                  Margin = reader.Code CodeTable.Margin "Margin" 343
                  AdditionalMargin = reader.Code CodeTable.Margin "AdditionalMargin" 346
                  ThirdMargin = reader.Code CodeTable.Margin "ThirdMargin" 349
                  CornerPositions = Array.init 4 (fun i -> reader.Int ($"CornerPositions[{i}]") (352 + i * 2) 2)
                  WinOdds = reader.Number "WinOdds" 360 4 10M
                  Popularity = reader.Int "Popularity" 364 2
                  PrizeHundredYen = reader.Number "PrizeHundredYen" 366 8 1M
                  AddedPrizeHundredYen = reader.Number "AddedPrizeHundredYen" 374 8 1M
                  LastFourFurlongSeconds = sectional reader "LastFourFurlongSeconds" 388
                  LastThreeFurlongSeconds = sectional reader "LastThreeFurlongSeconds" 391
                  Opponents =
                    Array.init 3 (fun i ->
                        { PedigreeId = reader.Identifier true ($"Opponents[{i}].PedigreeId") (394 + i * 46) 10
                          Name = reader.Text ($"Opponents[{i}].Name") (404 + i * 46) 36 })
                  TimeGap = gap reader
                  RecordUpdateCategory = reader.Ascii "RecordUpdateCategory" 536 1
                  MiningCategory = reader.Ascii "MiningCategory" 537 1
                  PredictedFinishSeconds = elapsed reader "PredictedFinishSeconds" 538 5 100M
                  PredictionFasterErrorSeconds = reader.Number "PredictionFasterErrorSeconds" 543 4 100M
                  PredictionSlowerErrorSeconds = reader.Number "PredictionSlowerErrorSeconds" 547 4 100M
                  PredictedPosition = reader.Int "PredictedPosition" 551 2
                  RunningStyle = reader.Ascii "RunningStyle" 553 1
                  Raw = Array.copy data })
            data

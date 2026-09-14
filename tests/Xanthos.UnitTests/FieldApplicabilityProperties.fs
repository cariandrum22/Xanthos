namespace Xanthos.UnitTests

open System
open System.IO
open System.Text.Json
open Microsoft.FSharp.Reflection
open Xunit
open Xunit.Abstractions
open Xanthos
open Xanthos.Data

/// The checked-in manifest selects semantics independently of the parser's result.
/// Positions come from the spreadsheet oracle, never production layout constants.
module internal FieldApplicability =
    type Example =
        | Value of category: string * raw: string * expected: obj
        | Invalid of category: string * raw: string

    let leafType (t: Type) (path: string) =
        let mutable current = t

        for part in path.Split('.') do
            let bracket = part.IndexOf('[')
            current <- current.GetProperty(if bracket < 0 then part else part[.. bracket - 1]).PropertyType

            if bracket >= 0 then
                current <- current.GetElementType()

        current

    let examples profile width seed =
        let spaces = String(' ', width)
        let zeros = String('0', width)
        let nines = String('9', width)

        let number n =
            string n |> fun s -> s.PadLeft(width, '0')

        let value category raw expected = Value(category, raw, box expected)
        let invalid = Invalid("invalid", String('X', width))

        let choices values =
            values
            |> List.map (fun (raw, expected: obj) ->
                let case, _ = FSharpValue.GetUnionFields(expected, expected.GetType())
                Value(case.Name, raw, expected))

        match profile with
        | "text" ->
            [ value "blank" spaces spaces
              value "one-byte" (String('A', width)) (String('A', width))
              if width >= 2 then
                  let raw = String('A', width - 2) + "馬"
                  value "two-byte-end" raw raw ]
        | "ascii" -> [ value "blank" spaces spaces; value "ascii" nines nines ]
        | "official-code" ->
            let unknown = String('?', width)
            [ value "unknown" unknown unknown; value "blank" spaces spaces ]
        | "sign" -> [ value "blank" " " " "; value "plus" "+" "+"; value "minus" "-" "-"; invalid ]
        | "digits"
        | "blank-digits" ->
            [ value "zero" zeros zeros
              value "maximum" nines nines
              if profile = "blank-digits" then
                  value "blank" spaces spaces
              else
                  Invalid("blank-invalid", spaces)
              invalid ]
        | "int"
        | "registration-count" ->
            let maximum =
                if profile = "registration-count" then
                    300
                else
                    min (pown 10M width - 1M) (decimal Int32.MaxValue) |> int

            [ value "zero" zeros (Some 0)
              value "maximum" (number maximum) (Some maximum)
              value "nonzero" (number (1 + seed % maximum)) (Some(1 + seed % maximum))
              value "blank" spaces (None: int option)
              invalid
              if profile = "registration-count" then
                  Invalid("overflow", "301")
              if width >= 10 then
                  Invalid("overflow", "2147483648".PadLeft(width, '0')) ]
        | "year" ->
            // A year in a race identity also participates in its eight-byte date.
            [ value "maximum" "9999" (Some 9999)
              value "minimum" "0001" (Some 1)
              Invalid("zero-invalid-date", zeros)
              Invalid("blank-invalid-date", spaces)
              invalid ]
        | "decimal-1"
        | "decimal-10"
        | "decimal-100" ->
            let scale = Decimal.Parse(profile[8..], Globalization.CultureInfo.InvariantCulture)
            let maximum = pown 10M width - 1M

            [ value "zero" zeros (Some 0M)
              value "maximum" nines (Some(maximum / scale))
              value "nonzero" (number 1) (Some(1M / scale))
              value "blank" spaces (None: decimal option)
              invalid ]
        | "elapsed-10"
        | "record-elapsed-10"
        | "elapsed-100" ->
            let digits = if profile = "elapsed-100" then 2 else 1
            let fraction = pown 10M digits
            let minutes = pown 10M (width - digits - 2) - 1M
            let maxRaw = minutes * 100M * fraction + 60M * fraction - 1M

            [ value "zero" zeros (if profile = "record-elapsed-10" then None else Some 0M)
              value "maximum" (number maxRaw) (Some(minutes * 60M + 60M - 1M / fraction))
              value "blank" spaces (None: decimal option)
              Invalid("invalid-seconds", nines)
              invalid ]
        | "score" ->
            [ value "zero" "0000" (Some 0M)
              value "maximum" "1000" (Some 100M)
              value "blank" spaces (None: decimal option)
              Invalid("overflow", "1001")
              invalid ]
        | "date"
        | "header-date" ->
            [ value "leap" "20240229" (Some(DateOnly(2024, 2, 29)))
              value "missing" "00000000" (None: DateOnly option)
              value "maximum" "99991231" (Some DateOnly.MaxValue)
              Invalid("invalid-date", "20230229")
              Invalid("blank-invalid", spaces) ]
        | "month-day" ->
            [ value "maximum" "1231" "1231"
              value "ordinary" "0101" "0101"
              Invalid("invalid-date", "0230")
              Invalid("blank-invalid", spaces) ]
        | "clock" ->
            [ value "maximum" "2359" (Some(TimeOnly(23, 59)))
              value "zero" zeros (None: TimeOnly option)
              value "blank" spaces (None: TimeOnly option)
              Invalid("invalid-hour", "2400")
              Invalid("invalid-minute", "1260") ]
        | "announcement" ->
            [ value
                  "leap"
                  "02292359"
                  (Some
                      { Month = 2
                        Day = 29
                        Hour = 23
                        Minute = 59 })
              value "zero" zeros (None: AnnouncementTime option)
              value "blank" spaces (None: AnnouncementTime option)
              Invalid("invalid-day", "02302359")
              Invalid("invalid-hour", "01012400")
              Invalid("invalid-minute", "01012360") ]
        | "bool" ->
            [ value "false" "0" (Some false)
              value "true" "1" (Some true)
              value "unknown" "9" (None: bool option)
              value "blank" " " (None: bool option) ]
        | "BodyWeight" ->
            choices
                [ "   ", box BodyWeight.Missing
                  "000", box BodyWeight.Withdrawn
                  "999", box BodyWeight.Unmeasurable
                  "002", box (BodyWeight.Kilograms 2)
                  "998", box (BodyWeight.Kilograms 998) ]
            @ [ Invalid("invalid-sentinel", "001") ]
        | "WeightChange"
        | "negative-WeightChange" ->
            let sign = if profile = "negative-WeightChange" then -1 else 1

            choices
                [ "   ", box WeightChange.Missing
                  "999", box WeightChange.Unmeasurable
                  "000", box (WeightChange.Kilograms 0)
                  "998", box (WeightChange.Kilograms(sign * 998)) ]
            @ [ invalid ]
        | "SectionalTime" ->
            choices
                [ "   ", box SectionalTime.NotRecorded
                  "000", box SectionalTime.NotRecorded
                  "999", box SectionalTime.NoResult
                  "998", box (SectionalTime.Seconds 99.8M) ]
            @ [ invalid ]
        | "TimeGap" ->
            choices
                [ "    ", box TimeGap.Unset
                  "0000", box TimeGap.Unset
                  "9999", box TimeGap.NoResult
                  "-999", box (TimeGap.Seconds -99.9M)
                  "+999", box (TimeGap.Seconds 99.9M) ]
            @ [ invalid ]
        | "training"
        | "capped-training" ->
            let maximum = (pown 10M width - 1M) / 10M

            choices
                [ spaces, box TrainingTime.Missing
                  zeros, box TrainingTime.Unmeasurable
                  number 1, box (TrainingTime.Seconds 0.1M)
                  nines,
                  box (
                      if profile = "training" then
                          TrainingTime.Seconds maximum
                      else
                          TrainingTime.AtLeastSeconds maximum
                  ) ]
            @ [ invalid ]
        | "SaleState" ->
            choices
                [ "0", box SaleState.NotSold
                  "1", box SaleState.CancelledBeforeSale
                  "3", box SaleState.CancelledAfterSale
                  "7", box SaleState.Available
                  "9", box (SaleState.Unknown "9") ]
        | "PlacePayoutRule" ->
            choices
                [ "0", box PlacePayoutRule.NotSold
                  "2", box PlacePayoutRule.TwoPlaces
                  "3", box PlacePayoutRule.ThreePlaces
                  "9", box (PlacePayoutRule.Unknown "9") ]
        | "Popularity" ->
            choices
                [ spaces, box Popularity.NotRegistered
                  String('-', width), box Popularity.CancelledBeforeSale
                  String('*', width), box Popularity.CancelledAfterSale
                  zeros, box (Popularity.Rank 0)
                  nines, box (Popularity.Rank(Int32.Parse nines)) ]
            @ [ invalid ]
        | "odds"
        | "capped-odds" ->
            let maximum = (pown 10M width - 1M) / 10M

            choices
                [ spaces, box OddsValue.NotRegistered
                  zeros, box OddsValue.NoVotes
                  String('-', width), box OddsValue.CancelledBeforeSale
                  String('*', width), box OddsValue.CancelledAfterSale
                  number 1, box (OddsValue.Quoted 0.1M)
                  nines,
                  box (
                      if profile = "odds" then
                          OddsValue.Quoted maximum
                      else
                          OddsValue.AtLeast maximum
                  ) ]
            @ [ invalid ]
        | "PersonSex" ->
            choices
                [ "1", box PersonSex.Male
                  "2", box PersonSex.Female
                  "9", box (PersonSex.Unknown "9") ]
        | "ImportKind"
        | "foal-ImportKind" ->
            choices
                [ "0", box ImportKind.Domestic
                  "1", box ImportKind.ImportedInUtero
                  "2", box ImportKind.ImportedAsDomestic
                  "3", box ImportKind.Imported
                  "9",
                  box (
                      if profile = "foal-ImportKind" then
                          ImportKind.Unknown "9"
                      else
                          ImportKind.Other
                  )
                  "8", box (ImportKind.Unknown "8") ]
        | "RaceRecordKind" ->
            choices
                [ "1", box RaceRecordKind.Course
                  "2", box RaceRecordKind.GradeOne
                  "9", box (RaceRecordKind.Unknown "9") ]
        | "RecordTimeKind" ->
            choices
                [ "1", box RecordTimeKind.Standard
                  "2", box RecordTimeKind.Record
                  "3", box RecordTimeKind.Reference
                  "4", box RecordTimeKind.Remark
                  "9", box (RecordTimeKind.Unknown "9") ]
        | "TrainingCentre" ->
            choices
                [ "0", box TrainingCentre.Miho
                  "1", box TrainingCentre.Ritto
                  "9", box (TrainingCentre.Unknown "9") ]
        | "TrainingCourse" ->
            choices
                [ "0", box TrainingCourse.A
                  "1", box TrainingCourse.B
                  "2", box TrainingCourse.C
                  "3", box TrainingCourse.D
                  "4", box TrainingCourse.E
                  "9", box (TrainingCourse.Unknown "9") ]
        | "TrainingDirection" ->
            choices
                [ "0", box TrainingDirection.Right
                  "1", box TrainingDirection.Left
                  "9", box (TrainingDirection.Unknown "9") ]
        | "EntryStatus" ->
            choices
                [ "1", box EntryStatus.Entered
                  "2", box EntryStatus.ExcludedAtDeadline
                  "4", box EntryStatus.Reentered
                  "5", box EntryStatus.ReentryExcluded
                  "6", box EntryStatus.WithdrawnWithoutNumber
                  "9", box EntryStatus.Withdrawn
                  "8", box (EntryStatus.Unknown "8") ]
        | "ExclusionStatus" ->
            choices
                [ "1", box ExclusionStatus.NotBalloted
                  "2", box ExclusionStatus.NotSelected
                  "9", box (ExclusionStatus.Unknown "9") ]
        | "WeatherChangeKind" ->
            choices
                [ "1", box WeatherChangeKind.Initial
                  "2", box WeatherChangeKind.Weather
                  "3", box WeatherChangeKind.TrackCondition
                  "9", box (WeatherChangeKind.Unknown "9") ]
        | "WithdrawalReason" ->
            choices
                [ "000", box WithdrawalReason.Unspecified
                  "   ", box WithdrawalReason.Unspecified
                  "001", box WithdrawalReason.Illness
                  "002", box WithdrawalReason.Accident
                  "003", box WithdrawalReason.Other
                  "999", box (WithdrawalReason.Unknown "999") ]
        | "CourseChangeReason" ->
            choices
                [ "1", box CourseChangeReason.StrongWind
                  "2", box CourseChangeReason.Typhoon
                  "3", box CourseChangeReason.Snow
                  "4", box CourseChangeReason.Other
                  "9", box (CourseChangeReason.Unknown "9") ]
        | other -> failwith $"Unknown independent field profile {other}"

type FieldApplicabilityProperties(output: ITestOutputHelper) =
    static member Cases = GeneratedRecordOracle.cases

    [<Theory; MemberData(nameof FieldApplicabilityProperties.Cases)>]
    member _.``Q06 every declared field profile reaches its required categories at record boundaries``
        (id: string, seed: int)
        =
        use manifest =
            JsonDocument.Parse(
                File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Contracts", "field-applicability.json"))
            )

        let rows =
            manifest.RootElement.GetProperty("fields").EnumerateArray()
            |> Seq.filter (fun r -> r.GetProperty("id").GetString() = id)
            |> Seq.toArray

        let mapping = GeneratedRecordOracle.mapping id

        Assert.Equal<Set<string * string>>(
            mapping |> Set.ofList,
            rows
            |> Seq.map (fun r -> r.GetProperty("key").GetString(), r.GetProperty("path").GetString())
            |> Set.ofSeq
        )

        let layout = RecordOracle.layout id
        let baseline = GeneratedRecordOracle.fixture id

        let modelType =
            typeof<Session>.Assembly.GetType("Xanthos.Records").GetMethod("parse" + id).ReturnType.GetGenericArguments()[0]

        for row in rows do
            let get (key: string) = row.GetProperty(key).GetString()
            let key, path, profile = get "key", get "path", get "profile"
            let field = RecordOracle.field layout key
            Assert.Equal(get "kind", (FieldApplicability.leafType modelType path).ToString())
            let counts = Collections.Generic.Dictionary<string, int>()

            let hit category =
                counts[category] <- (if counts.ContainsKey category then counts[category] else 0) + 1

            if profile = "structural" then
                Assert.True(
                    path = "Header.RecordId"
                    || path = "Header.DataCategory"
                    || get "kind" = typeof<RaceIdentity>.ToString(),
                    $"Unapproved structural exemption: {id}/{path}"
                )

                Assert.False(String.IsNullOrWhiteSpace(get "verifiedBy"))
            else
                let offsets = RecordOracle.positions layout field
                let occurrences = [ 0; offsets.Length - 1 ] |> List.distinct

                for occurrence in occurrences do
                    let actualPath = GeneratedRecordOracle.modelPath field path occurrence

                    let verifyError (data: byte[]) =
                        match GeneratedRecordOracle.individual id data with
                        | Ok _ -> failwith $"Accepted invalid {id}/{actualPath}/{profile}"
                        | Error error ->
                            Assert.Equal(id, error.RecordId)

                            let errorPath =
                                if profile = "year" || profile = "month-day" then
                                    if id = "WF" then "Date" else "Identity.Date"
                                elif profile = "header-date" then
                                    "CreatedDate"
                                else
                                    actualPath

                            Assert.True(
                                error.Field = errorPath
                                || error.Field = actualPath
                                || error.Field.StartsWith(errorPath + ".", StringComparison.Ordinal),
                                $"{id}/{actualPath}: unexpected error field {error.Field}"
                            )

                            let start =
                                if profile = "month-day" then
                                    offsets[occurrence] - 4
                                else
                                    offsets[occurrence]

                            let width =
                                if profile = "year" || profile = "month-day" then
                                    8
                                else
                                    field.Length

                            let position, length =
                                if profile = "announcement" && error.Field <> actualPath then
                                    let offset =
                                        match error.Field[(actualPath.Length + 1) ..] with
                                        | "Month" -> 0
                                        | "Day" -> 2
                                        | "Hour" -> 4
                                        | "Minute" -> 6
                                        | other -> failwith $"Unknown announcement error: {other}"

                                    offsets[occurrence] + offset + 1, 2
                                elif error.Field = actualPath then
                                    offsets[occurrence] + 1, field.Length
                                else
                                    start + 1, width

                            Assert.Equal(position, error.Position)
                            Assert.Equal(length, error.Length)
                            Assert.Equal<byte>(data[error.Position - 1 .. error.Position + error.Length - 2], error.Raw)

                    for example in FieldApplicability.examples profile field.Length seed do
                        let data = Array.copy baseline

                        let category, raw =
                            match example with
                            | FieldApplicability.Value(c, r, _)
                            | FieldApplicability.Invalid(c, r) -> c, r

                        RecordOracle.write layout key occurrence raw data

                        match example with
                        | FieldApplicability.Invalid _ -> verifyError data
                        | FieldApplicability.Value(_, _, expected) ->
                            let parsed =
                                GeneratedRecordOracle.individual id data
                                |> GeneratedRecordOracle.success $"{id}/{actualPath}/{category}"

                            let value = FieldCategories.fieldValue parsed actualPath

                            if profile = "header-date" then
                                Assert.Equal(raw, unbox<string> value)
                                Assert.Equal(expected, FieldCategories.fieldValue parsed "Header.CreatedDate")
                            elif value :? string then
                                Assert.Equal(expected, value)
                            elif value :? OfficialCode then
                                let code = unbox<OfficialCode> value
                                Assert.Equal(unbox<string> expected, Codes.raw code)

                                if category = "unknown" then
                                    Assert.False(Codes.isKnown code)
                            else
                                Assert.Equal(raw, value.GetType().GetProperty("Raw").GetValue(value) :?> string)
                                Assert.Equal(expected, value.GetType().GetProperty("Value").GetValue value)

                        hit category

                    if profile = "text" then
                        let data = Array.copy baseline
                        Array.fill data offsets[occurrence] field.Length (byte 'A')
                        data[offsets[occurrence] + field.Length - 1] <- 0x82uy
                        verifyError data
                        hit "invalid-cp932"

                    if offsets.Length > 1 then
                        hit (if occurrence = 0 then "first" else "last")

                Assert.NotEmpty(row.GetProperty("requiredCategories").EnumerateArray())

                for category in row.GetProperty("requiredCategories").EnumerateArray() |> Seq.map _.GetString() do
                    Assert.True(
                        counts.ContainsKey category && counts[category] > 0,
                        $"Unreached {id}/{path}/{profile}/{category}"
                    )

            output.WriteLine(
                JsonSerializer.Serialize
                    {| id = id
                       field = path
                       profile = profile
                       seed = seed
                       categories = counts |}
            )

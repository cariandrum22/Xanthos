namespace Xanthos.UnitTests

open System
open System.IO
open System.Text.Json
open Microsoft.FSharp.Reflection
open Xunit
open Xunit.Abstractions
open Xanthos
open Xanthos.Data

/// The manifest fixes applicable records and mandatory categories independently
/// of the examples executed below. These are field-type representatives; the
/// spreadsheet contract tests separately verify every mapped field position.
module internal FieldCategories =
    let rec leaves (t: Type) =
        seq {
            if t.IsArray then
                if t <> typeof<byte[]> then
                    yield! leaves (t.GetElementType())
            elif t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<Sourced<_>> then
                yield (t.GetGenericArguments()[0]).ToString()
            elif t = typeof<string> then
                yield "string"
            elif t = typeof<OfficialCode> then
                yield "OfficialCode"
            elif FSharpType.IsRecord t then
                for field in FSharpType.GetRecordFields t do
                    yield! leaves field.PropertyType
            else
                yield t.ToString()
        }

    let reader (raw: string) =
        Reader("ZZ", RecordOracle.encoding.GetBytes raw)

    let fieldValue (model: obj) (path: string) =
        let mutable current = model

        for part in path.Split('.') do
            let bracket = part.IndexOf('[')
            let name = if bracket < 0 then part else part[.. bracket - 1]
            current <- current.GetType().GetProperty(name).GetValue current

            if bracket >= 0 then
                current <- (current :?> Array).GetValue(Int32.Parse(part[bracket + 1 .. part.Length - 2]))

        current

    let samples id path (values: (string * obj) list) hit =
        let layout = RecordOracle.layout id
        let key = GeneratedRecordOracle.mapping id |> List.find (snd >> (=) path) |> fst
        let field = RecordOracle.field layout key
        let offsets = RecordOracle.positions layout field
        let occurrences = [ 0; offsets.Length - 1 ] |> List.distinct

        for occurrence in occurrences do
            for raw, expected in values do
                let data = GeneratedRecordOracle.fixture id
                RecordOracle.write layout key occurrence raw data

                let parsed =
                    GeneratedRecordOracle.individual id data
                    |> GeneratedRecordOracle.success $"{id}/{path}/{raw}"

                let value =
                    fieldValue parsed (GeneratedRecordOracle.modelPath field path occurrence)

                Assert.Equal(raw, value.GetType().GetProperty("Raw").GetValue(value) :?> string)
                let actual = value.GetType().GetProperty("Value").GetValue value
                Assert.Equal(expected, actual)
                let union, _ = FSharpValue.GetUnionFields(expected, expected.GetType())
                hit union.Name

            if offsets.Length > 1 then
                hit (if occurrence = 0 then "first" else "last")

    let verify kind seed hit =
        let assertValue category (expected: 'T) (actual: 'T) =
            Assert.Equal<'T>(expected, actual)
            hit category

        let invalid category operation =
            Assert.Throws<ReadFailure>(Action operation) |> ignore
            hit category

        let samples id path values = samples id path values hit

        match kind with
        | "string" ->
            for width in [ 2; 3; 8; 36; 60; 4000 ] do
                for category, raw in
                    [ "one-byte", String('A', width)
                      "two-byte-end", String('A', width - 2) + "馬"
                      "blank", String(' ', width) ] do
                    assertValue category raw ((reader raw).Text "Text" 1 width)

                let broken = Array.create width 65uy
                broken[width - 1] <- 0x82uy
                invalid "invalid-cp932" (fun () -> Reader("ZZ", broken).Text "Text" 1 width |> ignore)

            for width in [ 1; 5; 10; 12 ] do
                let raw = String('9', width)
                assertValue "identifier" raw ((reader raw).Identifier false "Id" 1 width)

                invalid "invalid-identifier" (fun () ->
                    (reader (String('A', width))).Identifier false "Id" 1 width |> ignore)
        | "Microsoft.FSharp.Core.FSharpOption`1[System.Int32]" ->
            for width in [ 1; 2; 3; 4; 5; 8; 10 ] do
                let maximum = min (pown 10M width - 1M) (decimal Int32.MaxValue) |> int

                for category, value in
                    [ "zero", 0
                      "maximum", maximum
                      "nonzero", Random(seed + width).Next(1, maximum) ] do
                    let raw = string value |> fun s -> s.PadLeft(width, '0')
                    assertValue category (Some value) ((reader raw).Int "Number" 1 width).Value

                assertValue "blank" None ((reader (String(' ', width))).Int "Number" 1 width).Value

            invalid "overflow" (fun () -> (reader "2147483648").Int "Number" 1 10 |> ignore)
        | "Microsoft.FSharp.Core.FSharpOption`1[System.Decimal]" ->
            for width in [ 1; 2; 3; 4; 5; 8; 11 ] do
                for scale in [ 1M; 10M; 100M ] do
                    for category, value in [ "zero", 0M; "maximum", pown 10M width - 1M; "nonzero", 1M ] do
                        let raw = value.ToString("0").PadLeft(width, '0')
                        assertValue category (Some(value / scale)) ((reader raw).Number "Number" 1 width scale).Value

                    assertValue "blank" None ((reader (String(' ', width))).Number "Number" 1 width scale).Value
                    invalid "invalid" (fun () -> (reader (String('X', width))).Number "Number" 1 width scale |> ignore)
        | "Microsoft.FSharp.Core.FSharpOption`1[System.DateOnly]" ->
            for raw, expected, category in
                [ "20240229", Some(DateOnly(2024, 2, 29)), "leap"
                  "00000000", None, "missing"
                  "99991231", Some(DateOnly.MaxValue), "maximum" ] do
                assertValue category expected ((reader raw).Date "Date" 1).Value

            for raw in [ "20230229"; "        "; "20261301" ] do
                invalid "invalid" (fun () -> (reader raw).Date "Date" 1 |> ignore)
        | "Microsoft.FSharp.Core.FSharpOption`1[System.TimeOnly]" ->
            assertValue "maximum" (Some(TimeOnly(23, 59))) ((reader "2359").ClockTime "Time" 1).Value

            for category, raw in [ "zero", "0000"; "blank", "    " ] do
                assertValue category None ((reader raw).ClockTime "Time" 1).Value

            for raw in [ "2400"; "1260" ] do
                invalid "invalid" (fun () -> (reader raw).ClockTime "Time" 1 |> ignore)
        | "Microsoft.FSharp.Core.FSharpOption`1[Xanthos.Data.AnnouncementTime]" ->
            assertValue
                "leap"
                (Some
                    { Month = 2
                      Day = 29
                      Hour = 23
                      Minute = 59 })
                ((reader "02292359").Announcement "Time" 1).Value

            for category, raw in [ "zero", "00000000"; "blank", "        " ] do
                assertValue category None ((reader raw).Announcement "Time" 1).Value

            for raw in [ "02302359"; "13012359"; "01012400"; "01012360" ] do
                invalid "invalid" (fun () -> (reader raw).Announcement "Time" 1 |> ignore)
        | "Microsoft.FSharp.Core.FSharpOption`1[System.Boolean]" ->
            for category, raw, expected in
                [ "false", "0", Some false
                  "true", "1", Some true
                  "unknown", "9", None
                  "blank", " ", None ] do
                assertValue category expected (BettingReader.flag (reader raw) "Flag" 1).Value
        | "OfficialCode" ->
            assertValue "known" "05" ((reader "05").Code CodeTable.Racecourse "Code" 1 |> Codes.raw)
            assertValue "unknown" "99" ((reader "99").Code CodeTable.Racecourse "Code" 1 |> Codes.raw)
            invalid "non-ascii" (fun () -> (reader "馬").Code CodeTable.Racecourse "Code" 1 |> ignore)
        | "Xanthos.Data.BodyWeight" ->
            let values =
                [ "   ", box BodyWeight.Missing
                  "000", box BodyWeight.Withdrawn
                  "999", box BodyWeight.Unmeasurable
                  "002", box (BodyWeight.Kilograms 2)
                  "998", box (BodyWeight.Kilograms 998) ]

            samples "SE" "BodyWeight" values
            samples "WH" "Horses[{i}].Weight" values
        | "Xanthos.Data.WeightChange" ->
            let values =
                [ "   ", box WeightChange.Missing
                  "999", box WeightChange.Unmeasurable
                  "000", box (WeightChange.Kilograms 0)
                  "998", box (WeightChange.Kilograms 998) ]

            samples "SE" "WeightChange" values
            // WH applies its separate '-' sign field; SE preserves the magnitude.
            let signed =
                values
                |> List.map (fun (raw, value) ->
                    raw,
                    match unbox<WeightChange> value with
                    | WeightChange.Kilograms n -> box (WeightChange.Kilograms -n)
                    | value -> box value)

            samples "WH" "Horses[{i}].Change" signed
        | "Xanthos.Data.SectionalTime" ->
            samples
                "SE"
                "LastThreeFurlongSeconds"
                [ "   ", box SectionalTime.NotRecorded
                  "000", box SectionalTime.NotRecorded
                  "999", box SectionalTime.NoResult
                  "998", box (SectionalTime.Seconds 99.8M) ]
        | "Xanthos.Data.TimeGap" ->
            samples
                "SE"
                "TimeGap"
                [ "    ", box TimeGap.Unset
                  "0000", box TimeGap.Unset
                  "9999", box TimeGap.NoResult
                  "-999", box (TimeGap.Seconds -99.9M)
                  "+999", box (TimeGap.Seconds 99.9M) ]
        | "Xanthos.Data.TrainingTime" ->
            samples
                "HC"
                "Laps[0]"
                [ "   ", box TrainingTime.Missing
                  "000", box TrainingTime.Unmeasurable
                  "999", box (TrainingTime.Seconds 99.9M) ]

            samples
                "WC"
                "Laps[0]"
                [ "   ", box TrainingTime.Missing
                  "000", box TrainingTime.Unmeasurable
                  "998", box (TrainingTime.Seconds 99.8M)
                  "999", box (TrainingTime.AtLeastSeconds 99.9M) ]
        | "Xanthos.Data.SaleState" ->
            for raw, expected in
                [ "0", SaleState.NotSold
                  "1", SaleState.CancelledBeforeSale
                  "3", SaleState.CancelledAfterSale
                  "7", SaleState.Available
                  "9", SaleState.Unknown "9" ] do
                let actual = BettingReader.sale (reader raw) "Sale" 1
                Assert.Equal(expected, actual.Value)
                let case, _ = FSharpValue.GetUnionFields(expected, typeof<SaleState>)
                hit case.Name
        | "Xanthos.Data.PlacePayoutRule" ->
            for raw, expected in
                [ "0", PlacePayoutRule.NotSold
                  "2", PlacePayoutRule.TwoPlaces
                  "3", PlacePayoutRule.ThreePlaces
                  "9", PlacePayoutRule.Unknown "9" ] do
                let actual = BettingReader.placeRule (reader raw) "Rule" 1
                Assert.Equal(expected, actual.Value)
                let case, _ = FSharpValue.GetUnionFields(expected, typeof<PlacePayoutRule>)
                hit case.Name
        | "Xanthos.Data.Popularity" ->
            for raw, expected in
                [ "   ", Popularity.NotRegistered
                  "---", Popularity.CancelledBeforeSale
                  "***", Popularity.CancelledAfterSale
                  "000", Popularity.Rank 0
                  "999", Popularity.Rank 999 ] do
                let actual = BettingReader.popularity (reader raw) "Popularity" 1 3
                Assert.Equal(expected, actual.Value)
                let case, _ = FSharpValue.GetUnionFields(expected, typeof<Popularity>)
                hit case.Name
        | "Xanthos.Data.OddsValue" ->
            samples
                "O1"
                "Win[{i}].Odds"
                [ "    ", box OddsValue.NotRegistered
                  "0000", box OddsValue.NoVotes
                  "----", box OddsValue.CancelledBeforeSale
                  "****", box OddsValue.CancelledAfterSale
                  "0001", box (OddsValue.Quoted 0.1M)
                  "9999", box (OddsValue.AtLeast 999.9M) ]
        | "Xanthos.Data.PersonSex" ->
            samples
                "KS"
                "Sex"
                [ "1", box PersonSex.Male
                  "2", box PersonSex.Female
                  "9", box (PersonSex.Unknown "9") ]
        | "Xanthos.Data.ImportKind" ->
            samples
                "HN"
                "ImportKind"
                [ "0", box ImportKind.Domestic
                  "1", box ImportKind.ImportedInUtero
                  "2", box ImportKind.ImportedAsDomestic
                  "3", box ImportKind.Imported
                  "9", box ImportKind.Other
                  "8", box (ImportKind.Unknown "8") ]
        | "Xanthos.Data.RaceRecordKind" ->
            samples
                "RC"
                "Kind"
                [ "1", box RaceRecordKind.Course
                  "2", box RaceRecordKind.GradeOne
                  "9", box (RaceRecordKind.Unknown "9") ]
        | "Xanthos.Data.RecordTimeKind" ->
            samples
                "RC"
                "TimeKind"
                [ "1", box RecordTimeKind.Standard
                  "2", box RecordTimeKind.Record
                  "3", box RecordTimeKind.Reference
                  "4", box RecordTimeKind.Remark
                  "9", box (RecordTimeKind.Unknown "9") ]
        | "Xanthos.Data.TrainingCentre" ->
            samples
                "HC"
                "Centre"
                [ "0", box TrainingCentre.Miho
                  "1", box TrainingCentre.Ritto
                  "9", box (TrainingCentre.Unknown "9") ]
        | "Xanthos.Data.TrainingCourse" ->
            samples
                "WC"
                "Course"
                [ "0", box TrainingCourse.A
                  "1", box TrainingCourse.B
                  "2", box TrainingCourse.C
                  "3", box TrainingCourse.D
                  "4", box TrainingCourse.E
                  "9", box (TrainingCourse.Unknown "9") ]
        | "Xanthos.Data.TrainingDirection" ->
            samples
                "WC"
                "Direction"
                [ "0", box TrainingDirection.Right
                  "1", box TrainingDirection.Left
                  "9", box (TrainingDirection.Unknown "9") ]
        | "Xanthos.Data.EntryStatus" ->
            samples
                "JG"
                "EntryStatus"
                [ "1", box EntryStatus.Entered
                  "2", box EntryStatus.ExcludedAtDeadline
                  "4", box EntryStatus.Reentered
                  "5", box EntryStatus.ReentryExcluded
                  "6", box EntryStatus.WithdrawnWithoutNumber
                  "9", box EntryStatus.Withdrawn
                  "8", box (EntryStatus.Unknown "8") ]
        | "Xanthos.Data.ExclusionStatus" ->
            samples
                "JG"
                "ExclusionStatus"
                [ "1", box ExclusionStatus.NotBalloted
                  "2", box ExclusionStatus.NotSelected
                  "9", box (ExclusionStatus.Unknown "9") ]
        | "Xanthos.Data.WeatherChangeKind" ->
            samples
                "WE"
                "Change"
                [ "1", box WeatherChangeKind.Initial
                  "2", box WeatherChangeKind.Weather
                  "3", box WeatherChangeKind.TrackCondition
                  "9", box (WeatherChangeKind.Unknown "9") ]
        | "Xanthos.Data.WithdrawalReason" ->
            samples
                "AV"
                "Reason"
                [ "000", box WithdrawalReason.Unspecified
                  "   ", box WithdrawalReason.Unspecified
                  "001", box WithdrawalReason.Illness
                  "002", box WithdrawalReason.Accident
                  "003", box WithdrawalReason.Other
                  "999", box (WithdrawalReason.Unknown "999") ]
        | "Xanthos.Data.CourseChangeReason" ->
            samples
                "CC"
                "Reason"
                [ "1", box CourseChangeReason.StrongWind
                  "2", box CourseChangeReason.Typhoon
                  "3", box CourseChangeReason.Snow
                  "4", box CourseChangeReason.Other
                  "9", box (CourseChangeReason.Unknown "9") ]
        | other -> failwith $"No category verifier for {other}"

type FieldCategoryProperties(output: ITestOutputHelper) =
    [<Theory; InlineData(104729); InlineData(130363); InlineData(155921)>]
    member _.``Q06 field type manifest requires nonzero boundary and sentinel categories`` seed =
        use manifest =
            JsonDocument.Parse(
                File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Contracts", "field-categories.json"))
            )

        let records = typeof<Session>.Assembly.GetType("Xanthos.Records")

        let actual =
            GeneratedRecordOracle.ids
            |> Array.collect (fun id ->
                records.GetMethod("parse" + id).ReturnType.GetGenericArguments()[0]
                |> FieldCategories.leaves
                |> Seq.distinct
                |> Seq.map (fun kind -> kind, id)
                |> Seq.toArray)
            |> Array.groupBy fst
            |> Array.map (fun (kind, rows) -> kind, rows |> Array.map snd |> Set.ofArray)
            |> Map.ofArray

        let declared =
            manifest.RootElement.EnumerateArray()
            |> Seq.map (fun row -> row.GetProperty("fieldType").GetString())
            |> Set.ofSeq

        Assert.Equal<Set<string>>(actual |> Map.keys |> Set.ofSeq, declared)

        for row in manifest.RootElement.EnumerateArray() do
            let kind = row.GetProperty("fieldType").GetString()

            let applicable =
                row.GetProperty("records").EnumerateArray()
                |> Seq.map _.GetString()
                |> Set.ofSeq

            Assert.Equal<Set<string>>(applicable, actual[kind])

            let metadata =
                Set.ofList
                    [ "Xanthos.Data.OddsLimitFormat"
                      "Xanthos.Data.IdentifierFormat"
                      "Xanthos.RecordKind" ]

            Assert.Equal(metadata.Contains kind, row.TryGetProperty("metadataOnly") |> fst)

            if not (metadata.Contains kind) then
                let counts = Collections.Generic.Dictionary<string, int>()

                let hit category =
                    counts[category] <- (if counts.ContainsKey category then counts[category] else 0) + 1

                FieldCategories.verify kind seed hit

                for category in row.GetProperty("requiredCategories").EnumerateArray() |> Seq.map _.GetString() do
                    Assert.True(counts.ContainsKey category && counts[category] > 0, $"Unreached {kind}/{category}")

                output.WriteLine(
                    JsonSerializer.Serialize(
                        {| fieldType = kind
                           seed = seed
                           categories = counts
                           records = Set.toArray applicable |}
                    )
                )

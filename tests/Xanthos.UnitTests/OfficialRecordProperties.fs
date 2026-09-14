namespace Xanthos.UnitTests

open System
open Microsoft.FSharp.Reflection
open Xunit
open Xunit.Abstractions
open Xanthos

module internal GeneratedRecordOracle =
    let ids =
        "TK RA SE HR H1 H6 O1 O2 O3 O4 O5 O6 UM KS CH BR BN HN SK CK RC HC HS HY YS BT CS DM TM WF JG WC WH WE AV JC TC CC"
            .Split(' ')

    let seeds = [| 104729; 130363; 155921 |]

    let cases =
        seq {
            for id in ids do
                for seed in seeds do
                    yield [| box id; box seed |]
        }

    let fixture id =
        match id with
        | "TK" -> SpecialRegistrationTests.fixture ()
        | "RA" -> RaceContractTests.fixture ()
        | "SE" -> RunnerContractTests.fixture ()
        | "HR" -> PayoffContractTests.fixture ()
        | "UM" -> HorseContractTests.fixture () |> snd
        | "RC" -> RaceRecordContractTests.fixture () |> snd
        | "H1"
        | "H6" -> VotesContractTests.fixture id |> snd
        | "O1"
        | "O2"
        | "O3"
        | "O4"
        | "O5"
        | "O6" -> OddsContractTests.fixture id |> snd
        | "WE"
        | "AV"
        | "JC"
        | "TC"
        | "CC"
        | "WF" -> ChangesContractTests.fixture id |> snd
        | "BR"
        | "BN" -> OwnershipContractTests.fixture id |> snd
        | "KS"
        | "CH" -> PeopleContractTests.fixture id |> snd
        | _ -> AdditionalRecordContractTests.fixture id |> snd

    let individual id data =
        let parser =
            typeof<Session>.Assembly.GetType("Xanthos.Records").GetMethod("parse" + id)

        Assert.NotNull parser
        let result = parser.Invoke(null, [| box data |])
        let case, fields = FSharpValue.GetUnionFields(result, result.GetType())

        if case.Name = "Ok" then
            Ok fields[0]
        else
            Error(unbox<RecordParseError> fields[0])

    let success context =
        function
        | Ok value -> value
        | Error error -> failwithf "%s: %A" context error

    let property name (model: obj) =
        model.GetType().GetProperty(name).GetValue(model)

    let mapping id =
        match id with
        | "TK" ->
            [ yield! RaceContractTests.mappings |> List.take 23
              "24", "Conditions.RaceKind"
              "25", "Conditions.RaceSymbol"
              "26", "Conditions.WeightRule"
              for i in 0..4 do
                  string (27 + i), $"Conditions.AgeConditions[{i}]"
              "32", "Distance"
              "33", "Track"
              "34", "CourseCategory"
              "35", "HandicapDate"
              "36", "RegisteredCount"
              "37.a", "Horses[{i}].Sequence"
              "37.b", "Horses[{i}].PedigreeId"
              "37.c", "Horses[{i}].Name"
              "37.d", "Horses[{i}].HorseSymbol"
              "37.e", "Horses[{i}].Sex"
              "37.f", "Horses[{i}].TrainerAffiliation"
              "37.g", "Horses[{i}].TrainerCode"
              "37.h", "Horses[{i}].TrainerName"
              "37.i", "Horses[{i}].AssignedWeight"
              "37.j", "Horses[{i}].ExchangeCategory" ]
        | "RA" -> RaceContractTests.mappings
        | "SE" -> RunnerContractTests.mappings
        | "HR" -> PayoffContractTests.mappings
        | "UM" -> HorseContractTests.mapping
        | "RC" -> RaceRecordContractTests.mapping
        | "H1"
        | "H6" -> VotesContractTests.mappings id
        | "O1"
        | "O2"
        | "O3"
        | "O4"
        | "O5"
        | "O6" -> OddsContractTests.mappings id
        | "WE"
        | "AV"
        | "JC"
        | "TC"
        | "CC"
        | "WF" -> ChangesContractTests.mappings id
        | "BR"
        | "BN" -> OwnershipContractTests.mapping id
        | "KS"
        | "CH" -> PeopleContractTests.mapping id
        | _ -> AdditionalRecordContractTests.mapping id

    let modelPath (field: RecordOracle.Field) (path: string) index =
        let row, col =
            if path.Contains("{j}") then
                index / field.Repeat, index % field.Repeat
            else
                index, 0

        path.Replace("{i}", string row).Replace("{j}", string col)

type OfficialRecordProperties(output: ITestOutputHelper) =
    static member Cases = GeneratedRecordOracle.cases
    static member Records = GeneratedRecordOracle.ids |> Seq.map (fun id -> [| box id |])

    [<Theory; MemberData(nameof OfficialRecordProperties.Cases)>]
    member _.``Q06 generated official records agree with individual parser dispatcher and owned Raw``
        (id: string, seed: int)
        =
        let baseline = GeneratedRecordOracle.fixture id
        let layout = RecordOracle.layout id
        let random = Random seed

        let count =
            if Environment.GetEnvironmentVariable "XANTHOS_TEST_PROFILE" = "Stress" then
                1000
            else
                100

        let mutable nonblank = 0
        let mutable leap = 0
        let mutable missing = 0

        let textFields =
            GeneratedRecordOracle.mapping id
            |> List.choose (fun (key, path) ->
                let field = RecordOracle.field layout key

                if
                    field.Length >= 2
                    && (field.Name.Contains("名") || field.Name.Contains("説明"))
                    && not (path.Contains("Code") || path.Contains("Id"))
                then
                    Some(field, path)
                else
                    None)

        for index in 0 .. count - 1 do
            let data = Array.copy baseline

            let expected =
                match index % 10 with
                | 0 ->
                    leap <- leap + 1
                    Some(DateOnly(2024, 2, 29))
                | 1 ->
                    missing <- missing + 1
                    None
                | _ -> Some(DateOnly(2000, 1, 1).AddDays(random.Next(0, 36525)))

            let raw =
                expected
                |> Option.map (fun value -> value.ToString("yyyyMMdd"))
                |> Option.defaultValue "00000000"

            RecordOracle.write layout "3" 0 raw data

            let changedText =
                if textFields.IsEmpty then
                    None
                else
                    let field, path = textFields[index % textFields.Length]
                    let offsets = RecordOracle.positions layout field
                    let occurrence = if index % 2 = 0 then 0 else offsets.Length - 1
                    let marker = (if index % 3 = 0 then "A" else "馬") + string (random.Next(10))

                    let marker =
                        if RecordOracle.encoding.GetByteCount marker <= field.Length then
                            marker
                        else
                            "馬"

                    let expectedText = RecordOracle.padded field.Length marker
                    RecordOracle.write layout field.Id occurrence expectedText data
                    Some(field, occurrence, GeneratedRecordOracle.modelPath field path occurrence)

            let context =
                $"id={id} seed={seed} index={index} field=CreatedDate position=4 length=8"

            let verify (input: byte[]) =
                // Recompute expectations from independent source bytes when shrinking.
                let expectedRaw = RecordOracle.text layout "3" 0 input

                let expectedDate =
                    if expectedRaw = "00000000" then
                        None
                    else
                        Some(DateOnly.ParseExact(expectedRaw, "yyyyMMdd", Globalization.CultureInfo.InvariantCulture))

                let model =
                    GeneratedRecordOracle.individual id input
                    |> GeneratedRecordOracle.success context

                let dispatched = Records.parse input |> GeneratedRecordOracle.success context
                let _, values = FSharpValue.GetUnionFields(dispatched, typeof<Records.Record>)
                Assert.Equal(model, values[0])
                let header = GeneratedRecordOracle.property "Header" model :?> RecordHeader
                Assert.Equal(expectedDate, header.CreatedDate)
                Assert.Equal(expectedRaw, header.CreatedDateRaw)

                changedText
                |> Option.iter (fun (field, occurrence, path) ->
                    Assert.Equal(RecordOracle.text layout field.Id occurrence input, RecordOracle.modelText model path))

                let owned = GeneratedRecordOracle.property "Raw" model :?> byte[]
                Assert.Equal<byte>(input, owned)

                Assert.True(
                    input[11 .. input.Length - 3] |> Array.exists ((<>) 32uy),
                    context + " has no nonblank body"
                )

                let first = input[0]

                try
                    input[0] <- 0uy
                    Assert.Equal(first, owned[0])
                finally
                    input[0] <- first

            try
                verify data
            with error ->
                let path = GeneratedFailure.preserve id seed index baseline data verify error
                failwithf "%s; failure payload=%s; %s" context path error.Message

            nonblank <- nonblank + 1

        Assert.Equal(count, nonblank)
        Assert.True(leap > 0 && missing > 0)

        output.WriteLine(
            $"id={id} seed={seed} examples={count} rejected=0 nonblank={nonblank} leap={leap} missing={missing}; replay by id/seed/index; no generated input filtering"
        )

    [<Theory; MemberData(nameof OfficialRecordProperties.Records)>]
    member _.``Q06 every official parser rejects exact malformed boundaries``(id: string) =
        let valid = GeneratedRecordOracle.fixture id

        let check field position length data =
            match GeneratedRecordOracle.individual id data with
            | Ok _ -> failwith $"{id}: accepted mutation {field}"
            | Error error ->
                Assert.Equal(id, error.RecordId)
                Assert.Equal(field, error.Field)
                Assert.Equal(position, error.Position)
                Assert.Equal(length, error.Length)

        for data in [ null; [||]; valid[.. valid.Length - 2]; Array.append valid [| 32uy |] ] do
            check "RecordLength" 1 valid.Length data

        let wrongId = Array.copy valid
        wrongId[0] <- byte 'Z'
        wrongId[1] <- byte 'Z'
        check "RecordId" 1 2 wrongId
        let crlf = Array.copy valid
        crlf[crlf.Length - 2] <- 32uy
        check "CRLF" (crlf.Length - 1) 2 crlf
        let date = Array.copy valid
        RecordOracle.write (RecordOracle.layout id) "3" 0 "20230229" date
        check "CreatedDate" 4 8 date
        RecordOracle.write (RecordOracle.layout id) "3" 0 "        " date
        check "CreatedDate" 4 8 date

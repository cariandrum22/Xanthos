namespace Xanthos.UnitTests

open System
open System.IO
open System.Text.Json
open Microsoft.FSharp.Reflection
open Xunit
open Xanthos

module FormatAndDataSpecProperties =
    let private success =
        function
        | Ok value -> value
        | Error error -> failwithf "%A" error

    let private legacy id =
        if id = "BR" || id = "UM" then
            let layout, data, _ = IdentifierFormatTests.fixture id
            layout, data
        else
            let current, baseline = AdditionalRecordContractTests.fixture id
            let old = AdditionalFormatTests.oldLayout id
            let bytes = RecordOracle.blank old

            for field, _ in AdditionalRecordContractTests.mapping id do
                let target = RecordOracle.field old field

                for index in 0 .. (RecordOracle.positions old target).Length - 1 do
                    let original =
                        RecordOracle.text current field index baseline |> RecordOracle.encoding.GetBytes

                    RecordOracle.write
                        old
                        field
                        index
                        (RecordOracle.encoding.GetString(original, 0, target.Length))
                        bytes

            old, bytes

    let LegacyCases =
        seq {
            for id in [ "BR"; "UM"; "HN"; "SK"; "HS"; "BT"; "CK" ] do
                for seed in GeneratedRecordOracle.seeds do
                    yield [| box id; box seed |]
        }

    [<Theory; MemberData(nameof LegacyCases)>]
    let ``Q06 legacy identifier layouts keep original bytes across generated delivery dates`` id seed =
        let layout, baseline = legacy id
        let random = Random(seed)

        let count =
            if Environment.GetEnvironmentVariable "XANTHOS_TEST_PROFILE" = "Stress" then
                1000
            else
                100

        let options =
            { Records.ParseOptions.Default with
                IdentifierFormat = Data.IdentifierFormat.Legacy }

        let methodInfo =
            typeof<Session>.Assembly.GetType("Xanthos.Records").GetMethod("parse" + id + "WithFormat")

        for index in 0 .. count - 1 do
            let bytes = Array.copy baseline

            let expected =
                DateOnly(2023, 8, 8).AddDays(random.Next(0, 1000)).ToString("yyyyMMdd")

            RecordOracle.write layout "3" 0 expected bytes
            let dispatched = Records.parseWith options bytes |> success
            let _, models = FSharpValue.GetUnionFields(dispatched, typeof<Records.Record>)

            let individual =
                methodInfo.Invoke(null, [| box Data.IdentifierFormat.Legacy; box bytes |])

            let case, result = FSharpValue.GetUnionFields(individual, individual.GetType())
            Assert.True((case.Name = "Ok"), $"id={id} seed={seed} index={index}: {individual}")
            Assert.Equal(models[0], result[0])
            let header = GeneratedRecordOracle.property "Header" models[0] :?> RecordHeader
            Assert.Equal(expected, header.CreatedDateRaw)
            Assert.Equal<byte>(bytes, GeneratedRecordOracle.property "Raw" models[0] :?> byte[])

    [<Theory; InlineData(104729); InlineData(130363); InlineData(155921)>]
    let ``Q06 dataspec options record sets time ranges and format cutoffs follow independent contracts`` seed =
        use json =
            JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Contracts", "dataspecs.json")))

        let definitions =
            json.RootElement.GetProperty("definitions").EnumerateArray() |> Seq.toArray

        let random = Random(seed)
        let noEnd = Set.ofList [ "TOKU"; "DIFF"; "DIFN"; "HOSE"; "HOSN"; "HOYU"; "COMM" ]
        let expanded = Set.ofList [ "DIFN"; "BLDN"; "SNPN"; "HOSN"; "TCVN"; "RCVN" ]
        let old = Set.ofList [ "DIFF"; "BLOD"; "SNAP"; "HOSE"; "TCOV"; "RCOV" ]

        for definition in definitions do
            let id = definition.GetProperty("id").GetString()
            let actual = DataSpecs.tryFind id |> Option.get

            let values (property: string) =
                definition.GetProperty(property).EnumerateArray()
                |> Seq.map _.GetString()
                |> Set.ofSeq

            let realtime = values "realtime"
            Assert.Equal<Set<string>>((if realtime.IsEmpty then values "normal" else realtime), actual.RecordIds)
            Assert.Equal<Set<string>>(values "setup", actual.SetupRecordIds)

            Assert.Equal(
                (if expanded.Contains id then
                     Some(DateOnly(2023, 8, 8))
                 else
                     None),
                actual.DeliveryStart
            )

            for offset in [ -1; 0; 1 ] do
                let delivery = DateTime(2023, 8, 8).AddDays(float offset)

                for option in [ 0; 1; 2; 3; 4; 5 ] do
                    for withEnd in [ false; true ] do
                        let permitted =
                            definition.GetProperty("options").EnumerateArray()
                            |> Seq.exists (fun n -> n.GetInt32() = option)

                        let request =
                            { Dataspec = id
                              FromTime = delivery
                              ToTime =
                                (if withEnd then
                                     Some(delivery.AddSeconds(float (random.Next(1, 30))))
                                 else
                                     None)
                              Option = option }

                        let expected = permitted && (not withEnd || not (noEnd.Contains id))
                        Assert.Equal(expected, DataSpecs.validateOpen request |> Result.isOk)

                let options =
                    DataSpecs.parseOptions id (DateOnly(2004, 8, 14).AddDays offset) |> Option.get

                Assert.Equal(
                    (if old.Contains id then
                         Data.IdentifierFormat.Legacy
                     else
                         Data.IdentifierFormat.Expanded),
                    options.IdentifierFormat
                )

                Assert.Equal(
                    (if offset < 0 then
                         Data.OddsLimitFormat.Before20040814
                     else
                         Data.OddsLimitFormat.Current),
                    options.OddsLimitFormat
                )

        for index in 0..99 do
            let chosen = definitions[random.Next definitions.Length]
            let id = chosen.GetProperty("id").GetString()
            let fromTime = DateTime(2026, 9, 13).AddMinutes(float index)

            let reversed =
                { Dataspec = id
                  FromTime = fromTime
                  ToTime = Some(fromTime.AddSeconds(-1.))
                  Option = 1 }

            Assert.True(DataSpecs.validateOpen reversed |> Result.isError)

namespace Xanthos.UnitTests

open System
open Xunit
open Xanthos
open Xanthos.Data

module RecordFieldBoundaryProperties =
    let private success =
        function
        | Ok value -> value
        | Error error -> failwithf "%A" error

    let private detail =
        function
        | ReadFailure error -> error
        | other -> raise other

    [<Theory; InlineData(104729); InlineData(130363); InlineData(155921)>]
    let ``Q06 numeric codecs preserve zero maximum blank scales and exact errors`` seed =
        let random = Random seed

        for width in [ 1; 2; 3; 4; 5; 8; 11 ] do
            for scale in [ 1M; 10M; 100M ] do
                let maximum = pown 10M width - 1M

                for value in [ 0M; maximum; decimal (random.Next(1, 10)) % (maximum + 1M) ] do
                    let raw = value.ToString("0").PadLeft(width, '0')
                    let bytes = RecordOracle.encoding.GetBytes raw
                    let actual = Reader("ZZ", bytes).Number "Value" 1 width scale
                    Assert.Equal(Some(value / scale), actual.Value)
                    Assert.Equal(raw, actual.Raw)

                Assert.Equal(None, (Reader("ZZ", Array.create width 32uy).Number "Value" 1 width scale).Value)
                let invalid = Array.create width (byte '0')
                invalid[width - 1] <- byte 'X'

                let error =
                    Assert.Throws<ReadFailure>(fun () -> Reader("ZZ", invalid).Number "Value" 1 width scale |> ignore)

                let failure = detail error
                Assert.Equal("ZZ", failure.RecordId)
                Assert.Equal("Value", failure.Field)
                Assert.Equal(1, failure.Position)
                Assert.Equal(width, failure.Length)
                Assert.Equal<byte>(invalid, failure.Raw)

    [<Theory; InlineData(2); InlineData(3); InlineData(8); InlineData(36); InlineData(60); InlineData(4000)>]
    let ``Q06 CP932 one and two byte characters occupy exact field boundaries`` width =
        for raw in [ String('A', width); String('A', width - 2) + "馬"; String(' ', width) ] do
            let bytes = RecordOracle.encoding.GetBytes raw
            Assert.Equal(width, bytes.Length)
            Assert.Equal(raw, Reader("ZZ", bytes).Text "Name" 1 width)

        let broken = Array.create width (byte 'A')
        broken[width - 1] <- 0x82uy

        let failure =
            Assert.Throws<ReadFailure>(fun () -> Reader("ZZ", broken).Text "Name" 1 width |> ignore)
            |> detail

        Assert.Equal("Name", failure.Field)
        Assert.Equal(width, failure.Length)
        Assert.Equal<byte>(broken, failure.Raw)

    [<Fact>]
    let ``Q06 date time announcement and unknown codes preserve their distinct contracts`` () =
        let reader (raw: string) =
            Reader("ZZ", RecordOracle.encoding.GetBytes raw)

        Assert.Equal(Some(DateOnly(2024, 2, 29)), ((reader "20240229").Date "Date" 1).Value)
        Assert.Equal(None, ((reader "00000000").Date "Date" 1).Value)

        for invalid in [ "20230229"; "20261301"; "        " ] do
            Assert.Throws<ReadFailure>(fun () -> (reader invalid).Date "Date" 1 |> ignore)
            |> ignore

        Assert.Equal(Some(TimeOnly(23, 59)), ((reader "2359").ClockTime "Time" 1).Value)

        for missing in [ "0000"; "    " ] do
            Assert.Equal(None, ((reader missing).ClockTime "Time" 1).Value)

        for invalid in [ "2400"; "1260" ] do
            Assert.Throws<ReadFailure>(fun () -> (reader invalid).ClockTime "Time" 1 |> ignore)
            |> ignore

        let announcement = (reader "02292359").Announcement "Announcement" 1
        Assert.Equal(29, announcement.Value.Value.Day)
        Assert.Equal(None, ((reader "        ").Announcement "Announcement" 1).Value)
        Assert.Equal("99", (reader "99").Code CodeTable.Racecourse "Course" 1 |> Codes.raw)

    [<Theory; InlineData(0); InlineData(17)>]
    let ``Q06 repeated horse weight first and last distinguish every sentinel`` index =
        let layout, baseline = AdditionalRecordContractTests.fixture "WH"

        for raw, expected in
            [ "000", BodyWeight.Withdrawn
              "002", BodyWeight.Kilograms 2
              "998", BodyWeight.Kilograms 998
              "999", BodyWeight.Unmeasurable
              "   ", BodyWeight.Missing ] do
            let bytes = Array.copy baseline
            RecordOracle.write layout "11.p39" index raw bytes
            let model = Records.parseWH bytes |> success
            Assert.Equal(expected, model.Horses[index].Weight.Value)
            Assert.Equal(raw, model.Horses[index].Weight.Raw)

        let bad = Array.copy baseline
        RecordOracle.write layout "11.p39" index "001" bad

        match Records.parseWH bad with
        | Ok _ -> failwith "Invalid weight accepted"
        | Error error ->
            Assert.Equal($"Horses[{index}].Weight", error.Field)
            Assert.Equal(74 + 45 * index, error.Position)
            Assert.Equal(3, error.Length)

namespace Xanthos.UnitTests

open System
open Xunit
open Xanthos
open Xanthos.Data

module RunnerContractTests =
    let private layout = RecordOracle.layout "SE"

    let private value =
        function
        | Ok value -> value
        | Error error -> failwithf "%A" error

    let private put id index raw data =
        RecordOracle.write layout id index raw data

    let private mappings =
        [ "1", "Header.RecordId"
          "2", "Header.DataCategory"
          "3", "Header.CreatedDateRaw"
          "4", "Identity.Year"
          "5", "Identity.MonthDay"
          "6", "Identity.Racecourse"
          "7", "Identity.Meeting"
          "8", "Identity.Day"
          "9", "Identity.RaceNumber"
          "10", "Bracket"
          "11", "HorseNumber"
          "12", "PedigreeId"
          "13", "Name"
          "14", "HorseSymbol"
          "15", "Sex"
          "16", "Breed"
          "17", "Coat"
          "18", "Age"
          "19", "Affiliation"
          "20", "TrainerCode"
          "21", "TrainerName"
          "22", "OwnerCode"
          "23", "OwnerName"
          "24", "SilksDescription"
          "26", "AssignedWeight"
          "27", "PreviousAssignedWeight"
          "28", "BlinkersCategory"
          "30", "JockeyCode"
          "31", "PreviousJockeyCode"
          "32", "JockeyName"
          "33", "PreviousJockeyName"
          "34", "Apprentice"
          "35", "PreviousApprentice"
          "36", "BodyWeight"
          "37", "WeightChangeSign"
          "38", "WeightChange"
          "39", "Abnormality"
          "40", "FinishPosition"
          "41", "ConfirmedPosition"
          "42", "DeadHeatCategory"
          "43", "DeadHeatCount"
          "44", "FinishSeconds"
          "45", "Margin"
          "46", "AdditionalMargin"
          "47", "ThirdMargin"
          "48", "CornerPositions[0]"
          "49", "CornerPositions[1]"
          "50", "CornerPositions[2]"
          "51", "CornerPositions[3]"
          "52", "WinOdds"
          "53", "Popularity"
          "54", "PrizeHundredYen"
          "55", "AddedPrizeHundredYen"
          "58", "LastFourFurlongSeconds"
          "59", "LastThreeFurlongSeconds"
          "60.a", "Opponents[{i}].PedigreeId"
          "60.b", "Opponents[{i}].Name"
          "61", "TimeGap"
          "62", "RecordUpdateCategory"
          "63", "MiningCategory"
          "64", "PredictedFinishSeconds"
          "65", "PredictionFasterErrorSeconds"
          "66", "PredictionSlowerErrorSeconds"
          "67", "PredictedPosition"
          "68", "RunningStyle" ]

    let private fixture () =
        let data = RecordOracle.blank layout

        for id, raw in
            [ "2", "7"
              "4", "2026"
              "5", "0912"
              "6", "05"
              "7", "04"
              "8", "02"
              "9", "11"
              "10", "3"
              "11", "05"
              "12", "2023123456"
              "14", "01"
              "15", "1"
              "16", "1"
              "17", "03"
              "18", "03"
              "19", "1"
              "20", "00123"
              "22", "123456"
              "26", "555"
              "27", "570"
              "28", "1"
              "30", "00456"
              "31", "00789"
              "34", "1"
              "35", "2"
              "36", "480"
              "37", "+"
              "38", "012"
              "39", "0"
              "40", "01"
              "41", "01"
              "42", "0"
              "43", "0"
              "44", "1572"
              "45", "1  "
              "46", " 12"
              "47", "A  "
              "48", "04"
              "49", "03"
              "50", "02"
              "51", "01"
              "52", "0123"
              "53", "02"
              "54", "12345678"
              "55", "00123456"
              "58", "456"
              "59", "334"
              "61", "-012"
              "62", "1"
              "63", "1"
              "64", "15723"
              "65", "0123"
              "66", "0456"
              "67", "01"
              "68", "2" ] do
            put id 0 raw data

        for id in [ "13"; "21"; "23"; "24"; "32"; "33" ] do
            put id 0 (RecordOracle.padded (RecordOracle.field layout id).Length "東京　") data

        for i in 0..2 do
            put "60.a" i (sprintf "2023%06d" (i + 1)) data
            put "60.b" i (RecordOracle.padded 36 ($"相手馬{i + 1}")) data

        for id in [ "25"; "29"; "56"; "57" ] do
            put id 0 (String('Z', (RecordOracle.field layout id).Length)) data

        data

    [<Fact; Trait("Category", "Contract")>]
    let ``Every nonreserved SE field and all three opponents follow the spreadsheet`` () =
        let data = fixture ()
        let record = Records.parseSE data |> value

        let dispatched =
            match Records.parse data |> value with
            | Records.Record.SE r -> r
            | other -> failwithf "%A" other

        Assert.Equal(record, dispatched)
        Assert.Equal(65, mappings.Length)
        Assert.Equal(layout.Fields.Length - 6, mappings.Length)

        for id, path in mappings do
            let f = RecordOracle.field layout id

            for i in 0 .. (RecordOracle.positions layout f |> List.length) - 1 do
                Assert.Equal(
                    RecordOracle.text layout id i data,
                    RecordOracle.modelText record (path.Replace("{i}", string i))
                )

        Assert.Equal(3, record.Opponents.Length)
        Assert.Equal<byte>(data, record.Raw)

    [<Fact; Trait("Category", "Contract")>]
    let ``SE scales and distinct special value types match official meanings`` () =
        let r = Records.parseSE (fixture ()) |> value
        Assert.Equal(Some 55.5M, r.AssignedWeight.Value)
        Assert.Equal(Some 57M, r.PreviousAssignedWeight.Value)
        Assert.Equal(BodyWeight.Kilograms 480, r.BodyWeight.Value)
        Assert.Equal(WeightChange.Kilograms 12, r.WeightChange.Value)
        Assert.Equal(Some 117.2M, r.FinishSeconds.Value)
        Assert.Equal(Some 12.3M, r.WinOdds.Value)
        Assert.Equal(Some 12345678M, r.PrizeHundredYen.Value)
        Assert.Equal(Some 123456M, r.AddedPrizeHundredYen.Value)
        Assert.Equal(SectionalTime.Seconds 45.6M, r.LastFourFurlongSeconds.Value)
        Assert.Equal(SectionalTime.Seconds 33.4M, r.LastThreeFurlongSeconds.Value)
        Assert.Equal(TimeGap.Seconds -1.2M, r.TimeGap.Value)
        Assert.Equal(Some 117.23M, r.PredictedFinishSeconds.Value)
        Assert.Equal(Some 1.23M, r.PredictionFasterErrorSeconds.Value)
        Assert.Equal(Some 4.56M, r.PredictionSlowerErrorSeconds.Value)

    [<Theory; Trait("Category", "Contract")>]
    [<InlineData("0")>]
    [<InlineData("1")>]
    [<InlineData("2")>]
    [<InlineData("3")>]
    [<InlineData("4")>]
    [<InlineData("5")>]
    [<InlineData("6")>]
    [<InlineData("7")>]
    [<InlineData("9")>]
    [<InlineData("A")>]
    [<InlineData("B")>]
    let ``SE retains every official category and initialized fields`` category =
        let data = RecordOracle.blank layout
        put "2" 0 category data
        let r = Records.parseSE data |> value
        Assert.Equal(category, r.Header.DataCategory)
        Assert.Equal(BodyWeight.Missing, r.BodyWeight.Value)
        Assert.Equal(TimeGap.Unset, r.TimeGap.Value)
        Assert.Equal<byte>(data, r.Raw)

    [<Fact; Trait("Category", "Contract")>]
    let ``SE measurement failures and no result are distinct from numeric values`` () =
        let data = fixture ()
        put "36" 0 "000" data
        Assert.Equal(BodyWeight.Withdrawn, (Records.parseSE data |> value).BodyWeight.Value)
        put "36" 0 "999" data
        put "38" 0 "999" data
        put "61" 0 "9999" data
        let r = Records.parseSE data |> value
        Assert.Equal(BodyWeight.Unmeasurable, r.BodyWeight.Value)
        Assert.Equal(WeightChange.Unmeasurable, r.WeightChange.Value)
        Assert.Equal(TimeGap.NoResult, r.TimeGap.Value)
        put "36" 0 "   " data
        put "38" 0 "   " data
        let blank = Records.parseSE data |> value
        Assert.Equal(BodyWeight.Missing, blank.BodyWeight.Value)
        Assert.Equal(WeightChange.Missing, blank.WeightChange.Value)

    [<Theory; Trait("Category", "Contract")>]
    [<InlineData("36", "001", "BodyWeight")>]
    [<InlineData("37", "X", "WeightChangeSign")>]
    [<InlineData("61", "x123", "TimeGap")>]
    [<InlineData("44", "1999", "FinishSeconds")>]
    [<InlineData("64", "19999", "PredictedFinishSeconds")>]
    [<InlineData("65", "x123", "PredictionFasterErrorSeconds")>]
    let ``SE invalid values report their exact source field`` id raw field =
        let data = fixture ()
        put id 0 raw data

        match Records.parseSE data with
        | Ok _ -> failwith "Invalid scalar accepted"
        | Error error ->
            Assert.Equal("SE", error.RecordId)
            Assert.Equal(field, error.Field)
            Assert.Equal((RecordOracle.positions layout (RecordOracle.field layout id))[0] + 1, error.Position)

    [<Fact; Trait("Category", "Contract")>]
    let ``SE unknown codes and unused opponent padding retain exact source`` () =
        let data = fixture ()
        put "17" 0 "??" data
        put "60.a" 2 (String(' ', 10)) data
        put "60.b" 2 (String(' ', 36)) data
        let r = Records.parseSE data |> value
        Assert.Equal("??", Codes.raw r.Coat)
        Assert.False(Codes.isKnown r.Coat)
        Assert.Equal(String(' ', 10), r.Opponents[2].PedigreeId)

    [<Fact; Trait("Category", "Contract")>]
    let ``SE validates null length identifier and CRLF`` () =
        let data = fixture ()

        for input in [ null; [||]; data[.. data.Length - 2]; Array.append data [| 32uy |] ] do
            Assert.True(Records.parseSE input |> Result.isError)

        put "1" 0 "RA" data
        Assert.True(Records.parseSE data |> Result.isError)
        put "1" 0 "SE" data
        data[data.Length - 1] <- 32uy
        Assert.True(Records.parseSE data |> Result.isError)

    [<Theory; Trait("Category", "Contract")>]
    [<InlineData("999")>]
    [<InlineData("000")>]
    [<InlineData("   ")>]
    [<InlineData("334")>]
    let ``Sectional times distinguish absent and no result from measured seconds`` raw =
        let data = fixture ()
        put "58" 0 raw data
        put "59" 0 raw data

        let expected =
            match raw with
            | "999" -> SectionalTime.NoResult
            | "000"
            | "   " -> SectionalTime.NotRecorded
            | _ -> SectionalTime.Seconds 33.4M

        let record = Records.parseSE data |> value
        Assert.Equal(raw, record.LastFourFurlongSeconds.Raw)
        Assert.Equal(raw, record.LastThreeFurlongSeconds.Raw)
        Assert.Equal(expected, record.LastFourFurlongSeconds.Value)
        Assert.Equal(expected, record.LastThreeFurlongSeconds.Value)

        match Records.parse data |> value with
        | Records.Record.SE dispatched -> Assert.Equal(record, dispatched)
        | _ -> Assert.Fail("Expected SE dispatch")

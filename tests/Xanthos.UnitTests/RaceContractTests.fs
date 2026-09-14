namespace Xanthos.UnitTests

open System
open Xunit
open Xanthos

module RaceContractTests =
    let private layout = RecordOracle.layout "RA"

    let private value =
        function
        | Ok value -> value
        | Error error -> failwithf "%A" error

    let private put id index raw data =
        RecordOracle.write layout id index raw data

    let internal mappings =
        [ "1", "Header.RecordId"
          "2", "Header.DataCategory"
          "3", "Header.CreatedDateRaw"
          "4", "Identity.Year"
          "5", "Identity.MonthDay"
          "6", "Identity.Racecourse"
          "7", "Identity.Meeting"
          "8", "Identity.Day"
          "9", "Identity.RaceNumber"
          "10", "Name.Weekday"
          "11", "Name.SpecialRaceNumber"
          "12", "Name.Title"
          "13", "Name.Subtitle"
          "14", "Name.Parentheses"
          "15", "Name.EnglishTitle"
          "16", "Name.EnglishSubtitle"
          "17", "Name.EnglishParentheses"
          "18", "Name.Abbreviation10"
          "19", "Name.Abbreviation6"
          "20", "Name.Abbreviation3"
          "21", "Name.Category"
          "22", "Name.Edition"
          "23", "Grade"
          "24", "PreviousGrade"
          "25", "Conditions.RaceKind"
          "26", "Conditions.RaceSymbol"
          "27", "Conditions.WeightRule"
          "28", "Conditions.AgeConditions[0]"
          "29", "Conditions.AgeConditions[1]"
          "30", "Conditions.AgeConditions[2]"
          "31", "Conditions.AgeConditions[3]"
          "32", "Conditions.AgeConditions[4]"
          "33", "ConditionName"
          "34", "Distance"
          "35", "PreviousDistance"
          "36", "Track"
          "37", "PreviousTrack"
          "38", "CourseCategory"
          "39", "PreviousCourseCategory"
          "40", "PrizesHundredYen[{i}]"
          "41", "PreviousPrizesHundredYen[{i}]"
          "42", "AddedPrizesHundredYen[{i}]"
          "43", "PreviousAddedPrizesHundredYen[{i}]"
          "44", "StartTime"
          "45", "PreviousStartTime"
          "46", "RegisteredCount"
          "47", "RunnerCount"
          "48", "FinisherCount"
          "49", "Weather"
          "50", "TurfCondition"
          "51", "DirtCondition"
          "52", "LapSeconds[{i}]"
          "53", "ObstacleMileSeconds"
          "54", "FirstThreeFurlongSeconds"
          "55", "FirstFourFurlongSeconds"
          "56", "LastThreeFurlongSeconds"
          "57", "LastFourFurlongSeconds"
          "58.a", "Corners[{i}].Corner"
          "58.b", "Corners[{i}].Lap"
          "58.c", "Corners[{i}].Order"
          "59", "RecordUpdateCategory" ]

    let internal fixture () =
        let data = RecordOracle.blank layout

        for id, raw in
            [ "2", "7"
              "4", "2026"
              "5", "0912"
              "6", "05"
              "7", "04"
              "8", "02"
              "9", "11"
              "10", "1"
              "11", "0123"
              "21", "2"
              "22", "042"
              "23", "A"
              "24", "B"
              "25", "13"
              "26", "A03"
              "27", "2"
              "28", "001"
              "29", "002"
              "30", "005"
              "31", "010"
              "32", "999"
              "34", "2400"
              "35", "2300"
              "36", "11"
              "37", "23"
              "38", "A "
              "39", "B "
              "44", "1530"
              "45", "1520"
              "46", "18"
              "47", "17"
              "48", "16"
              "49", "2"
              "50", "3"
              "51", "4"
              "53", "1572"
              "54", "345"
              "55", "456"
              "56", "334"
              "57", "445"
              "59", "1" ] do
            put id 0 raw data

        for id in [ "12"; "13"; "14"; "15"; "16"; "17"; "18"; "19"; "20"; "33" ] do
            let f = RecordOracle.field layout id
            put id 0 (RecordOracle.padded f.Length (if int id < 15 || id = "33" then "東京　" else "R" + id)) data

        for id in [ "40"; "41"; "42"; "43"; "52" ] do
            let f = RecordOracle.field layout id

            for i in 0 .. f.Repeat - 1 do
                put id i ((int id * 10 + i).ToString().PadLeft(f.Length, '0')) data

        for i in 0..3 do
            put "58.a" i (string (i + 1)) data
            put "58.b" i (string (i % 2 + 1)) data
            put "58.c" i (RecordOracle.padded 70 ($"{i + 1},2,3(4,5)")) data

        data

    [<Fact; Trait("Category", "Contract")>]
    let ``Every RA leaf and repeated element matches the independent spreadsheet`` () =
        let data = fixture ()
        let record = Records.parseRA data |> value

        let parsed =
            match Records.parse data |> value with
            | Records.Record.RA r -> r
            | other -> failwithf "%A" other

        Assert.Equal(record, parsed)
        Assert.Equal(61, mappings.Length)
        Assert.Equal(layout.Fields.Length - 2, mappings.Length)

        for id, path in mappings do
            let f = RecordOracle.field layout id
            let count = RecordOracle.positions layout f |> List.length

            for i in 0 .. count - 1 do
                Assert.Equal(
                    RecordOracle.text layout id i data,
                    RecordOracle.modelText record (path.Replace("{i}", string i))
                )

        Assert.Equal(4, record.Corners.Length)
        Assert.Equal<byte>(data, record.Raw)
        Assert.True(RecordBytes.crlf "RA" 1271 record.Raw |> Result.isOk)

    [<Fact; Trait("Category", "Contract")>]
    let ``RA meanings distinguish hundred yen decimal seconds and minute second time`` () =
        let r = Records.parseRA (fixture ()) |> value
        Assert.Equal(Some 2400, r.Distance.Value)
        Assert.Equal(Some(TimeOnly(15, 30)), r.StartTime.Value)
        Assert.Equal(Some(TimeOnly(15, 20)), r.PreviousStartTime.Value)
        Assert.Equal(Some 117.2M, r.ObstacleMileSeconds.Value)
        Assert.Equal(Some 34.5M, r.FirstThreeFurlongSeconds.Value)
        Assert.Equal(Some 45.6M, r.FirstFourFurlongSeconds.Value)
        Assert.Equal(Some 33.4M, r.LastThreeFurlongSeconds.Value)
        Assert.Equal(Some 44.5M, r.LastFourFurlongSeconds.Value)

        for i, n in Array.indexed r.PrizesHundredYen do
            Assert.Equal(Some(decimal (400 + i)), n.Value)

        for i, n in Array.indexed r.LapSeconds do
            Assert.Equal(Some(decimal (520 + i) / 10M), n.Value)

        Assert.Equal("A", Codes.raw r.Grade)
        Assert.Equal("B", Codes.raw r.PreviousGrade)

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
    let ``RA preserves every official category including cancellation and deletion`` category =
        let data = RecordOracle.blank layout
        put "2" 0 category data
        let r = Records.parseRA data |> value
        Assert.Equal(category, r.Header.DataCategory)
        Assert.Equal(None, r.Identity.Date)
        Assert.Equal(None, r.StartTime.Value)
        Assert.Equal<byte>(data, r.Raw)

    [<Theory; Trait("Category", "Contract")>]
    [<InlineData("44", 0, "2460", "StartTime")>]
    [<InlineData("53", 0, "1999", "ObstacleMileSeconds")>]
    [<InlineData("40", 3, "00000x00", "PrizesHundredYen[3]")>]
    [<InlineData("52", 12, "x12", "LapSeconds[12]")>]
    let ``RA rejects malformed scalar at its actual byte position`` id occurrence raw field =
        let data = fixture ()
        put id occurrence raw data

        match Records.parseRA data with
        | Ok _ -> failwith "Invalid input accepted"
        | Error error ->
            Assert.Equal("RA", error.RecordId)
            Assert.Equal(field, error.Field)
            Assert.Equal((RecordOracle.positions layout (RecordOracle.field layout id))[occurrence] + 1, error.Position)

    [<Fact; Trait("Category", "Contract")>]
    let ``RA rejects null wrong length wrong ID and invalid delimiter`` () =
        let data = fixture ()

        for input in [ null; [||]; data[.. data.Length - 2]; Array.append data [| 32uy |] ] do
            Assert.True(Records.parseRA input |> Result.isError)

        put "1" 0 "TK" data
        Assert.True(Records.parseRA data |> Result.isError)
        put "1" 0 "RA" data
        data[data.Length - 2] <- 32uy
        Assert.True(Records.parseRA data |> Result.isError)

    [<Fact; Trait("Category", "Contract")>]
    let ``RA preserves unknown grade instead of dropping its source`` () =
        let data = fixture ()
        put "23" 0 "?" data
        let r = Records.parseRA data |> value
        Assert.False(Codes.isKnown r.Grade)
        Assert.Equal("?", Codes.raw r.Grade)

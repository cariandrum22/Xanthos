namespace Xanthos.UnitTests

open System
open Xunit
open Xanthos
open Xanthos.Data

module SpecialRegistrationTests =
    let private layout = RecordOracle.layout "TK"

    let private value =
        function
        | Ok value -> value
        | Error error -> failwithf "%A" error

    let private put id index raw bytes =
        RecordOracle.write layout id index raw bytes

    let private raw id index bytes = RecordOracle.text layout id index bytes

    let internal fixture () =
        let data = RecordOracle.blank layout

        for id, text in
            [ "4", "2026"
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
              "24", "13"
              "25", "A03"
              "26", "2"
              "27", "001"
              "28", "002"
              "29", "005"
              "30", "010"
              "31", "999"
              "32", "2400"
              "33", "11"
              "34", "A "
              "35", "20260907"
              "36", "300" ] do
            put id 0 text data

        for id in [ "12"; "13"; "14"; "15"; "16"; "17"; "18"; "19"; "20" ] do
            let field = RecordOracle.field layout id
            put id 0 (RecordOracle.padded field.Length (if int id < 15 then "東京　" else "T" + id)) data

        for i in 0..299 do
            put "37.a" i (sprintf "%03d" (i + 1)) data
            put "37.b" i (sprintf "2023%06d" (i + 1)) data
            put "37.c" i (RecordOracle.padded 36 ($"登録馬{i + 1}")) data
            put "37.d" i (if i % 2 = 0 then "01" else "02") data
            put "37.e" i (if i % 2 = 0 then "1" else "2") data
            put "37.f" i "1" data
            put "37.g" i (sprintf "%05d" (i + 1)) data
            put "37.h" i (RecordOracle.padded 8 ($"師{i % 100}")) data
            put "37.i" i (sprintf "%03d" (500 + i % 100)) data
            put "37.j" i "1" data

        data

    let private fields (r: SpecialRegistration) =
        [ "1", r.Header.RecordId
          "2", r.Header.DataCategory
          "3", r.Header.CreatedDateRaw
          "4", r.Identity.Year.Raw
          "5", r.Identity.MonthDay
          "6", Codes.raw r.Identity.Racecourse
          "7", r.Identity.Meeting.Raw
          "8", r.Identity.Day.Raw
          "9", r.Identity.RaceNumber.Raw
          "10", Codes.raw r.Name.Weekday
          "11", r.Name.SpecialRaceNumber.Raw
          "12", r.Name.Title
          "13", r.Name.Subtitle
          "14", r.Name.Parentheses
          "15", r.Name.EnglishTitle
          "16", r.Name.EnglishSubtitle
          "17", r.Name.EnglishParentheses
          "18", r.Name.Abbreviation10
          "19", r.Name.Abbreviation6
          "20", r.Name.Abbreviation3
          "21", r.Name.Category
          "22", r.Name.Edition.Raw
          "23", Codes.raw r.Grade
          "24", Codes.raw r.Conditions.RaceKind
          "25", Codes.raw r.Conditions.RaceSymbol
          "26", Codes.raw r.Conditions.WeightRule
          for i in 0..4 do
              string (27 + i), Codes.raw r.Conditions.AgeConditions[i]
          "32", r.Distance.Raw
          "33", Codes.raw r.Track
          "34", r.CourseCategory
          "35", r.HandicapDate.Raw
          "36", r.RegisteredCount.Raw ]

    [<Fact; Trait("Category", "Contract")>]
    let ``All top level TK fields follow spreadsheet offsets through both public functions`` () =
        let data = fixture ()
        let record = Records.parseTK data |> value

        let dispatched =
            match Records.parse data |> value with
            | Records.Record.TK record -> record
            | other -> failwithf "%A" other

        Assert.Equal(record, dispatched)
        Assert.Equal(36, (fields record).Length)

        for id, actual in fields record do
            Assert.Equal(raw id 0 data, actual)

        Assert.Equal(Some 2400, record.Distance.Value)
        Assert.Equal(Some 300, record.RegisteredCount.Value)
        Assert.Equal(Some(DateOnly(2026, 9, 7)), record.HandicapDate.Value)
        Assert.Equal(Some(DateOnly(2026, 9, 12)), record.Identity.Date)
        Assert.True(Codes.isKnown record.Grade)
        Assert.Equal<byte>(data, record.Raw)

    [<Fact; Trait("Category", "Contract")>]
    let ``Every one of 300 horses retains every field and fixed point weight`` () =
        let data = fixture ()
        let record = Records.parseTK data |> value
        Assert.Equal(300, record.Horses.Length)

        for i, horse in Array.indexed record.Horses do
            let actual =
                [ "37.a", horse.Sequence.Raw
                  "37.b", horse.PedigreeId
                  "37.c", horse.Name
                  "37.d", Codes.raw horse.HorseSymbol
                  "37.e", Codes.raw horse.Sex
                  "37.f", Codes.raw horse.TrainerAffiliation
                  "37.g", horse.TrainerCode
                  "37.h", horse.TrainerName
                  "37.i", horse.AssignedWeight.Raw
                  "37.j", horse.ExchangeCategory ]

            for id, text in actual do
                Assert.Equal(raw id i data, text)

            Assert.Equal(Some(decimal (500 + i % 100) / 10M), horse.AssignedWeight.Value)
            Assert.Equal(Some(i + 1), horse.Sequence.Value)

        Assert.NotEqual(record.Horses[0], record.Horses[149])
        Assert.NotEqual(record.Horses[149], record.Horses[299])

    [<Theory; Trait("Category", "Contract")>]
    [<InlineData("0")>]
    [<InlineData("1")>]
    [<InlineData("2")>]
    let ``Official update and deletion categories retain initialized slots`` category =
        let data = RecordOracle.blank layout
        put "2" 0 category data
        let record = Records.parseTK data |> value
        Assert.Equal(category, record.Header.DataCategory)
        Assert.Equal(None, record.HandicapDate.Value)
        Assert.Equal(Some 0, record.RegisteredCount.Value)
        Assert.Equal(" ", Codes.raw record.Grade)
        Assert.Equal(300, record.Horses.Length)
        Assert.Equal<byte>(data, record.Raw)

    [<Fact; Trait("Category", "Contract")>]
    let ``Unknown table codes retain origin and original characters`` () =
        let data = fixture ()
        put "23" 0 "?" data
        put "37.d" 149 "??" data
        let record = Records.parseTK data |> value
        Assert.False(Codes.isKnown record.Grade)
        Assert.Equal("?", Codes.raw record.Grade)
        Assert.Equal("??", Codes.raw record.Horses[149].HorseSymbol)

    [<Fact; Trait("Category", "Contract")>]
    let ``SDK blank padding in unused horse slots is retained without accepting blank active IDs`` () =
        let data = fixture ()
        put "36" 0 "017" data
        let slots = RecordOracle.positions layout (RecordOracle.field layout "37")

        for i in 17..299 do
            Array.Fill(data, 32uy, slots[i], 70)

        let record = Records.parseTK data |> value
        Assert.Equal(300, record.Horses.Length)
        Assert.Equal(String(' ', 10), record.Horses[17].PedigreeId)
        Assert.Equal(String(' ', 5), record.Horses[299].TrainerCode)
        Assert.Equal(None, record.Horses[17].Sequence.Value)
        Assert.Equal(None, record.Horses[299].AssignedWeight.Value)
        Assert.Equal<byte>(data, record.Raw)
        put "36" 0 "018" data
        Assert.True(Records.parseTK data |> Result.isError)

    [<Theory; Trait("Category", "Contract")>]
    [<InlineData("35", 0, "20260230", "HandicapDate")>]
    [<InlineData("32", 0, "2x00", "Distance")>]
    [<InlineData("36", 0, "301", "RegisteredCount")>]
    [<InlineData("37.i", 149, "5x0", "Horses[149].AssignedWeight")>]
    [<InlineData("37.b", 299, "abcdefghij", "Horses[299].PedigreeId")>]
    let ``Invalid scalar reports actual field and spreadsheet byte offset`` id occurrence text expectedField =
        let data = fixture ()
        put id occurrence text data

        match Records.parseTK data with
        | Ok _ -> failwith "Invalid scalar accepted"
        | Error error ->
            Assert.Equal("TK", error.RecordId)
            Assert.Equal(expectedField, error.Field)
            Assert.Equal((RecordOracle.positions layout (RecordOracle.field layout id))[occurrence] + 1, error.Position)

    [<Fact; Trait("Category", "Contract")>]
    let ``Null short wrong ID and missing CRLF cannot pass the TK parser`` () =
        let data = fixture ()

        for input in [ null; [||]; data[.. data.Length - 2]; Array.append data [| 32uy |] ] do
            Assert.True(Records.parseTK input |> Result.isError)

        put "1" 0 "RA" data
        Assert.True(Records.parseTK data |> Result.isError)
        put "1" 0 "TK" data
        data[data.Length - 1] <- 32uy
        Assert.True(Records.parseTK data |> Result.isError)

    [<Fact; Trait("Category", "Contract")>]
    let ``Unrecognized record retains bytes without being mistaken for TK`` () =
        let data = [| 90uy; 90uy; 255uy; 0uy |]

        match Records.parse data |> value with
        | Records.Record.Unknown(id, original) ->
            Assert.Equal("ZZ", id)
            Assert.Equal<byte>(data, original)
            data[2] <- 1uy
            Assert.Equal(255uy, original[2])
        | _ -> failwith "Wrong dispatch"

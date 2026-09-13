namespace Xanthos.UnitTests

open System
open Xunit
open Xanthos
open Xanthos.Data

module PeopleContractTests =
    let private value =
        function
        | Ok v -> v
        | Error e -> failwithf "%A" e

    let private error =
        function
        | Error e -> e
        | Ok _ -> failwith "Expected a parsing error"

    let private parse id data =
        if id = "KS" then
            Records.parseKS data |> Result.map box
        else
            Records.parseCH data |> Result.map box

    let private mapping id =
        [ "1", "Header.RecordId"
          "2", "Header.DataCategory"
          "3", "Header.CreatedDateRaw"
          "4", (if id = "KS" then "JockeyId" else "TrainerId")
          "5", "Deregistered"
          "6", "LicensedDate"
          "7", "DeregisteredDate"
          "8", "BirthDate"
          "9", "Name"
          if id = "KS" then
              "11", "KanaName"
              "12", "Abbreviation"
              "13", "EuropeanName"
              "14", "Sex"
              "15", "Qualification"
              "16", "Apprentice"
              "17", "Affiliation"
              "18", "InvitationRegion"
              "19", "TrainerId"
              "20", "TrainerAbbreviation"
              "21.a", "FirstRides[{i}].Identity"
              "21.b", "FirstRides[{i}].RunnerCount"
              "21.c", "FirstRides[{i}].PedigreeId"
              "21.d", "FirstRides[{i}].HorseName"
              "21.e", "FirstRides[{i}].FinishPosition"
              "21.f", "FirstRides[{i}].Abnormality"
              "22.a", "FirstWins[{i}].Identity"
              "22.b", "FirstWins[{i}].RunnerCount"
              "22.c", "FirstWins[{i}].PedigreeId"
              "22.d", "FirstWins[{i}].HorseName"
          else
              "10", "KanaName"
              "11", "Abbreviation"
              "12", "EuropeanName"
              "13", "Sex"
              "14", "Affiliation"
              "15", "InvitationRegion"
          let win = if id = "KS" then "23." else "16."

          for letter, path in
              [ "a", "Identity"
                "b", "Title"
                "c", "Abbreviation10"
                "d", "Abbreviation6"
                "e", "Abbreviation3"
                "f", "Grade"
                "g", "RunnerCount"
                "h", "PedigreeId"
                "i", "HorseName" ] do
              win + letter, "RecentGradedWins[{i}]." + path

          let performance = if id = "KS" then "24." else "17."

          for letter, path in
              [ "a", "Year"
                "b", "FlatBasePrizeHundredYen"
                "c", "JumpBasePrizeHundredYen"
                "d", "FlatAddedPrizeHundredYen"
                "e", "JumpAddedPrizeHundredYen"
                "f", "Overall.Flat[{j}]"
                "g", "Overall.Jump[{j}]"
                "h", "Courses.Sapporo.Flat[{j}]"
                "i", "Courses.Sapporo.Jump[{j}]"
                "j", "Courses.Hakodate.Flat[{j}]"
                "k", "Courses.Hakodate.Jump[{j}]"
                "l", "Courses.Fukushima.Flat[{j}]"
                "m", "Courses.Fukushima.Jump[{j}]"
                "n", "Courses.Niigata.Flat[{j}]"
                "o", "Courses.Niigata.Jump[{j}]"
                "p", "Courses.Tokyo.Flat[{j}]"
                "q", "Courses.Tokyo.Jump[{j}]"
                "r", "Courses.Nakayama.Flat[{j}]"
                "s", "Courses.Nakayama.Jump[{j}]"
                "t", "Courses.Chukyo.Flat[{j}]"
                "u", "Courses.Chukyo.Jump[{j}]"
                "v", "Courses.Kyoto.Flat[{j}]"
                "w", "Courses.Kyoto.Jump[{j}]"
                "x", "Courses.Hanshin.Flat[{j}]"
                "y", "Courses.Hanshin.Jump[{j}]"
                "z", "Courses.Kokura.Flat[{j}]"
                "aa", "Courses.Kokura.Jump[{j}]"
                "ab", "TurfUpTo1600[{j}]"
                "ac", "Turf1601To2200[{j}]"
                "ad", "TurfOver2200[{j}]"
                "ae", "DirtUpTo1600[{j}]"
                "af", "Dirt1601To2200[{j}]"
                "ag", "DirtOver2200[{j}]" ] do
              performance + letter, "Performances[{i}]." + path ]

    let private fixture id =
        let layout = RecordOracle.layout id
        let data = RecordOracle.blank layout

        for field, path in mapping id do
            let f = RecordOracle.field layout field

            for index in 0 .. (RecordOracle.positions layout f).Length - 1 do
                let raw =
                    if field = "1" then
                        id
                    elif field = "2" then
                        "2"
                    elif field = "3" || field = "6" then
                        "20260912"
                    elif field = "7" then
                        "00000000"
                    elif field = "8" then
                        "20000229"
                    elif path.EndsWith("Identity") then
                        sprintf "202609%02d05010203" (index + 10)
                    elif path.EndsWith("Grade") then
                        [| "A"; "B"; "C" |][index]
                    elif f.Initial = "Ｓ" then
                        ("名" + string index).PadRight(f.Length - 1, ' ')
                    elif path = "KanaName" then
                        "ｷｼｭ".PadRight(f.Length, ' ')
                    elif path = "EuropeanName" then
                        "Person".PadRight(f.Length, ' ')
                    elif path.EndsWith("Year") then
                        string (2026 - index)
                    elif path.Contains("Prize") then
                        string (9876543210L + int64 index)
                    else
                        string (1 + index) |> fun s -> s.PadLeft(f.Length, '0')

                RecordOracle.write layout field index raw data

        layout, data

    [<Theory; Trait("Category", "Contract")>]
    [<InlineData("KS", 71)>]
    [<InlineData("CH", 57)>]
    let ``People masters cover every source field with all courses years places and race histories`` id count =
        let layout, data = fixture id
        let model = parse id data |> value
        let maps = mapping id
        Assert.Equal(count, maps.Length)

        let leaves =
            layout.Fields
            |> Array.filter (fun f ->
                not (f.Name.Contains("予備") || f.Name.Contains("レコード区切"))
                && not (layout.Fields |> Array.exists (fun child -> child.Parent = Some f.Id)))

        Assert.Equal<string>(leaves |> Array.map _.Id |> Array.sort, maps |> List.map fst |> List.sort)

        for field, path in maps do
            for index in 0 .. (RecordOracle.positions layout (RecordOracle.field layout field)).Length - 1 do
                let row, col =
                    if path.Contains("{j}") then
                        index / 6, index % 6
                    else
                        index, 0

                let target = path.Replace("{i}", string row).Replace("{j}", string col)
                Assert.Equal(RecordOracle.text layout field index data, RecordOracle.modelText model target)

        let result = Records.parse data |> value

        let _, fields =
            Microsoft.FSharp.Reflection.FSharpValue.GetUnionFields(result, typeof<Records.Record>)

        Assert.Equal(model, fields[0])
        let raw = model.GetType().GetProperty("Raw").GetValue(model) :?> byte[]
        Assert.Equal<byte>(data, raw)
        data[0] <- 0uy
        Assert.NotEqual(data[0], raw[0])

    [<Theory; Trait("Category", "Contract")>]
    [<InlineData("KS")>]
    [<InlineData("CH")>]
    let ``People registration update deletion initial fields and unknown sex are preserved`` id =
        let layout = RecordOracle.layout id

        for category in "012" do
            let data = RecordOracle.blank layout
            data[2] <- byte category
            parse id data |> value |> ignore

        let _, data = fixture id
        let f = if id = "KS" then "14" else "13"
        RecordOracle.write layout f 0 "X" data

        let sex, performances =
            if id = "KS" then
                let r = Records.parseKS data |> value in r.Sex, r.Performances
            else
                let r = Records.parseCH data |> value in r.Sex, r.Performances

        Assert.Equal(PersonSex.Unknown "X", sex.Value)
        Assert.Equal(Some 9876543212M, performances[2].JumpAddedPrizeHundredYen.Value)
        Assert.Equal(Some 18, performances[2].Courses.Kokura.Jump[5].Value)

    [<Theory; Trait("Category", "Contract")>]
    [<InlineData("KS")>]
    [<InlineData("CH")>]
    let ``People parsers reject malformed late fields race dates encoding and record framing`` id =
        let layout, original = fixture id

        for data in [ null; original[.. original.Length - 2] ] do
            Assert.Equal("RecordLength", (parse id data |> error).Field)

        for field, bad in [ "1", "ZZ"; "2", "9"; "8", "20210229" ] do
            let data = Array.copy original
            RecordOracle.write layout field 0 bad data
            Assert.True(parse id data |> Result.isError)

        let data = Array.copy original
        RecordOracle.write layout (if id = "KS" then "24.ag" else "17.ag") 17 "12345X" data
        Assert.Equal("Performances[2].DirtOver2200[5]", (parse id data |> error).Field)
        let data = Array.copy original
        RecordOracle.write layout (if id = "KS" then "23.a" else "16.a") 2 "2026023005010203" data
        Assert.Equal("Identity.Date", (parse id data |> error).Field)
        let data = Array.copy original
        let f = RecordOracle.field layout "9"
        data[f.Position + f.Length - 2] <- 0x82uy
        Assert.Equal("Name", (parse id data |> error).Field)
        let data = Array.copy original
        data[data.Length - 1] <- 0uy
        Assert.True(parse id data |> Result.isError)

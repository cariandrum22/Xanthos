namespace Xanthos.UnitTests

open System
open Xunit
open Xanthos
open Xanthos.Data

module ChangesContractTests =
    let private value =
        function
        | Ok v -> v
        | Error e -> failwithf "%A" e

    let private error =
        function
        | Error e -> e
        | Ok _ -> failwith "Expected a parsing error"

    let internal mappings id =
        [ "1", "Header.RecordId"
          "2", "Header.DataCategory"
          "3", "Header.CreatedDateRaw"
          "4", (if id = "WF" then "Year" else "Identity.Year")
          "5", (if id = "WF" then "MonthDay" else "Identity.MonthDay")
          if id = "WF" then
              "7.a", "Races[{i}].Racecourse"
              "7.b", "Races[{i}].Meeting"
              "7.c", "Races[{i}].Day"
              "7.d", "Races[{i}].RaceNumber"
              "9", "TicketsSold"
              "10.a", "RemainingTickets[{i}]"
              "11", "Refunded"
              "12", "Unformed"
              "13", "NoWinners"
              "14", "InitialCarryoverYen"
              "15", "NextCarryoverYen"
              "16.a", "Payouts[{i}].Combination"
              "16.b", "Payouts[{i}].AmountYen"
              "16.c", "Payouts[{i}].WinningTickets"
          else
              "6", "Identity.Racecourse"
              "7", "Identity.Meeting"
              "8", "Identity.Day"

              if id = "WE" then
                  "9", "Announcement"
                  "10", "Change"
                  "11", "After.Weather"
                  "12", "After.Turf"
                  "13", "After.Dirt"
                  "14", "Before.Weather"
                  "15", "Before.Turf"
                  "16", "Before.Dirt"
              else
                  "9", "Identity.RaceNumber"
                  "10", "Announcement"

                  match id with
                  | "AV" ->
                      "11", "HorseNumber"
                      "12", "HorseName"
                      "13", "Reason"
                  | "JC" ->
                      "11", "HorseNumber"
                      "12", "HorseName"
                      "13", "After.WeightKilograms"
                      "14", "After.JockeyId"
                      "15", "After.Name"
                      "16", "After.Apprentice"
                      "17", "Before.WeightKilograms"
                      "18", "Before.JockeyId"
                      "19", "Before.Name"
                      "20", "Before.Apprentice"
                  | "TC" ->
                      "11", "After"
                      "12", "Before"
                  | "CC" ->
                      "11", "After.DistanceMetres"
                      "12", "After.Track"
                      "13", "Before.DistanceMetres"
                      "14", "Before.Track"
                      "15", "Reason"
                  | _ -> failwith id ]

    let private parse id data =
        match id with
        | "WE" -> Records.parseWE data |> Result.map box
        | "AV" -> Records.parseAV data |> Result.map box
        | "JC" -> Records.parseJC data |> Result.map box
        | "TC" -> Records.parseTC data |> Result.map box
        | "CC" -> Records.parseCC data |> Result.map box
        | "WF" -> Records.parseWF data |> Result.map box
        | _ -> failwith id

    let internal fixture id =
        let layout = RecordOracle.layout id
        let data = RecordOracle.blank layout

        for field, path in mappings id do
            let f = RecordOracle.field layout field

            for index in 0 .. (RecordOracle.positions layout f).Length - 1 do
                let raw =
                    if field = "1" then
                        id
                    elif field = "2" then
                        "1"
                    elif field = "3" then
                        "20260912"
                    elif field = "4" then
                        "2026"
                    elif field = "5" then
                        "0912"
                    elif path = "Announcement" then
                        "09121345"
                    elif id = "TC" && field = "11" then
                        "1530"
                    elif id = "TC" && field = "12" then
                        "1500"
                    elif path = "HorseName" || path.EndsWith(".Name") then
                        (if path.StartsWith("Before") then "前" else "後") + String('　', f.Length / 2 - 1)
                    elif id = "WF" && f.Length >= 10 && not (path.EndsWith("Combination")) then
                        (9876543210L + int64 index).ToString().PadLeft(f.Length, '0')
                    else
                        (1 + index % 8).ToString().PadLeft(f.Length, '0')

                RecordOracle.write layout field index raw data

        layout, data

    [<Theory; Trait("Category", "Contract")>]
    [<InlineData("WE", 16)>]
    [<InlineData("AV", 13)>]
    [<InlineData("JC", 20)>]
    [<InlineData("TC", 12)>]
    [<InlineData("CC", 15)>]
    [<InlineData("WF", 19)>]
    let ``All nonreserved leaf fields and repeated occurrences match the independent spreadsheet`` id count =
        let layout, data = fixture id
        let model = parse id data |> value
        let fields = mappings id
        Assert.Equal(count, fields.Length)

        let leaves =
            layout.Fields
            |> Array.filter (fun f ->
                not (f.Name.Contains("予備") || f.Name.Contains("レコード区切"))
                && not (layout.Fields |> Array.exists (fun child -> child.Parent = Some f.Id)))

        Assert.Equal<string>(leaves |> Array.map _.Id |> Array.sort, fields |> List.map fst |> List.sort)

        for field, path in fields do
            for index in 0 .. (RecordOracle.positions layout (RecordOracle.field layout field)).Length - 1 do
                Assert.Equal(
                    RecordOracle.text layout field index data,
                    RecordOracle.modelText model (path.Replace("{i}", string index))
                )

        let dispatched = Records.parse data |> value

        let _, values =
            Microsoft.FSharp.Reflection.FSharpValue.GetUnionFields(dispatched, typeof<Records.Record>)

        Assert.Equal(model, values[0])
        let raw = model.GetType().GetProperty("Raw").GetValue(model) :?> byte[]
        Assert.Equal<byte>(data, raw)
        data[0] <- 0uy
        Assert.NotEqual(data[0], raw[0])

    [<Theory; Trait("Category", "Contract")>]
    [<InlineData("WE", "1")>]
    [<InlineData("AV", "12")>]
    [<InlineData("JC", "1")>]
    [<InlineData("TC", "1")>]
    [<InlineData("CC", "1")>]
    [<InlineData("WF", "012379")>]
    let ``Only the published categories are accepted including initial and deletion bodies`` id (categories: string) =
        let layout = RecordOracle.layout id

        for category in categories do
            let data = RecordOracle.blank layout
            data[2] <- byte category
            parse id data |> value |> ignore

        for category in "0123456789AB" do
            if not (categories.Contains(category)) then
                let data = RecordOracle.blank layout
                data[2] <- byte category
                Assert.Equal("DataCategory", (parse id data |> error).Field)

    [<Theory; Trait("Category", "Contract")>]
    [<InlineData("WE")>]
    [<InlineData("AV")>]
    [<InlineData("JC")>]
    [<InlineData("TC")>]
    [<InlineData("CC")>]
    [<InlineData("WF")>]
    let ``Malformed length identity date encoding and CRLF return located errors`` id =
        let layout, original = fixture id

        for data in [ null; original[.. original.Length - 2] ] do
            Assert.True(parse id data |> Result.isError)

        for field, replacement in [ "1", "ZZ"; "5", "0230" ] do
            let data = Array.copy original
            RecordOracle.write layout field 0 replacement data
            let error = parse id data |> error
            Assert.Equal(id, error.RecordId)

        let data = Array.copy original
        data[data.Length - 1] <- 0uy
        Assert.True(parse id data |> Result.isError)

        if id = "AV" || id = "JC" then
            let data = Array.copy original
            let field = RecordOracle.field layout "12"
            data[field.Position + field.Length - 3] <- 32uy
            data[field.Position + field.Length - 2] <- 0x82uy
            Assert.Equal("HorseName", (parse id data |> error).Field)

    [<Fact; Trait("Category", "Contract")>]
    let ``Change semantics preserve both sides unknown reasons and tenths of kilograms`` () =
        let layout, data = fixture "JC"
        RecordOracle.write layout "13" 0 "555" data
        RecordOracle.write layout "17" 0 "570" data
        RecordOracle.write layout "14" 0 "00000" data
        let jockey = Records.parseJC data |> value
        Assert.Equal(Some 55.5M, jockey.After.WeightKilograms.Value)
        Assert.Equal(Some 57M, jockey.Before.WeightKilograms.Value)
        Assert.Equal("00000", jockey.After.JockeyId)
        Assert.NotEqual<string>(jockey.After.Name, jockey.Before.Name)
        let _, times = fixture "TC"
        let time = Records.parseTC times |> value
        Assert.Equal(Some(TimeOnly(15, 30)), time.After.Value)
        Assert.Equal(Some(TimeOnly(15, 0)), time.Before.Value)
        let layout, data = fixture "AV"
        RecordOracle.write layout "13" 0 "987" data
        Assert.Equal(WithdrawalReason.Unknown "987", (Records.parseAV data |> value).Reason.Value)
        let layout, data = fixture "CC"
        RecordOracle.write layout "15" 0 "X" data
        Assert.Equal(CourseChangeReason.Unknown "X", (Records.parseCC data |> value).Reason.Value)

    [<Fact; Trait("Category", "Contract")>]
    let ``WIN5 preserves large yen amounts all 243 payouts and absent versus zero tickets`` () =
        let layout, data = fixture "WF"
        RecordOracle.write layout "14" 0 "999999999999999" data
        RecordOracle.write layout "10.a" 0 "           " data
        RecordOracle.write layout "10.a" 1 "00000000000" data
        let record = Records.parseWF data |> value
        Assert.Equal(5, record.Races.Length)
        Assert.Equal(243, record.Payouts.Length)
        Assert.Equal(Some 999999999999999M, record.InitialCarryoverYen.Value)
        Assert.Equal(None, record.RemainingTickets[0].Value)
        Assert.Equal(Some 0M, record.RemainingTickets[1].Value)
        Assert.Equal(Some 9876543452M, record.Payouts[242].WinningTickets.Value)
        RecordOracle.write layout "16.c" 242 "00000000X0" data
        Assert.Equal("Payouts[242].WinningTickets", (Records.parseWF data |> error).Field)

    [<Fact; Trait("Category", "Contract")>]
    let ``Yearless announcements and clock times reject impossible values`` () =
        let layout, data = fixture "WE"
        RecordOracle.write layout "9" 0 "02292359" data

        Assert.Equal(
            Some
                { Month = 2
                  Day = 29
                  Hour = 23
                  Minute = 59 },
            (Records.parseWE data |> value).Announcement.Value
        )

        for bad in [ "13010000"; "02300000"; "01012400"; "01010060" ] do
            RecordOracle.write layout "9" 0 bad data
            Assert.True(Records.parseWE data |> Result.isError)

        let layout, data = fixture "TC"
        RecordOracle.write layout "11" 0 "2460" data
        Assert.Equal("After", (Records.parseTC data |> error).Field)

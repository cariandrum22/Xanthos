namespace Xanthos.UnitTests

open System
open Xunit
open Xanthos

module PayoffContractTests =
    let private layout = RecordOracle.layout "HR"

    let private value =
        function
        | Ok value -> value
        | Error error -> failwithf "%A" error

    let private put id index raw data =
        RecordOracle.write layout id index raw data

    let private bets =
        [ "Win"
          "Place"
          "BracketQuinella"
          "Quinella"
          "Wide"
          "Exacta"
          "Trio"
          "Trifecta" ]

    let private payoutGroups =
        [ "42", "Win"
          "43", "Place"
          "44", "BracketQuinella"
          "45", "Quinella"
          "46", "Wide"
          "48", "Exacta"
          "49", "Trio"
          "50", "Trifecta" ]

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
          "10", "RegisteredCount"
          "11", "RunnerCount"
          for first, property in [ 12, "Unformed"; 21, "SpecialPayout"; 30, "Refunded" ] do
              for i, bet in List.indexed bets do
                  string (first + i + (if i >= 5 then 1 else 0)), property + "." + bet
          "39", "RefundedHorses[{i}]"
          "40", "RefundedBrackets[{i}]"
          "41", "RefundedSameBrackets[{i}]"
          for id, bet in payoutGroups do
              id + ".a", "Payouts." + bet + "[{i}].Combination"
              id + ".b", "Payouts." + bet + "[{i}].AmountYen"
              id + ".c", "Payouts." + bet + "[{i}].Popularity" ]

    let internal fixture () =
        let data = RecordOracle.blank layout

        for id, raw in
            [ "2", "2"
              "4", "2026"
              "5", "0912"
              "6", "05"
              "7", "04"
              "8", "02"
              "9", "11"
              "10", "18"
              "11", "17" ] do
            put id 0 raw data

        for first in [ 12; 21; 30 ] do
            for i in 0..7 do
                put (string (first + i + (if i >= 5 then 1 else 0))) 0 (string (i % 2)) data

        for id in [ "39"; "40"; "41" ] do
            for i in 0 .. (RecordOracle.field layout id).Repeat - 1 do
                put id i (string (i % 2)) data

        for id, _ in payoutGroups do
            let parent = RecordOracle.field layout id
            let combo = RecordOracle.field layout (id + ".a")
            let popularity = RecordOracle.field layout (id + ".c")

            for i in 0 .. parent.Repeat - 1 do
                let combination =
                    if id = "44" then
                        string (i + 1) + string (i + 2)
                    elif combo.Length = 2 then
                        sprintf "%02d" (i + 1)
                    elif combo.Length = 4 then
                        sprintf "%02d%02d" (i + 1) (i + 2)
                    else
                        sprintf "%02d%02d%02d" (i + 1) (i + 2) (i + 3)

                put (id + ".a") i combination data
                put (id + ".b") i ((int id * 1000 + i * 100).ToString().PadLeft(9, '0')) data
                put (id + ".c") i ((i + 1).ToString().PadLeft(popularity.Length, '0')) data

        for id in [ "17"; "26"; "35" ] do
            put id 0 "Z" data

        let reserved = RecordOracle.field layout "47"

        for position in RecordOracle.positions layout reserved do
            Array.Fill(data, 90uy, position, reserved.Length)

        data

    [<Fact; Trait("Category", "Contract")>]
    let ``All eight HR betting products preserve their full payoff and flag fields`` () =
        let data = fixture ()
        let record = Records.parseHR data |> value

        let dispatched =
            match Records.parse data |> value with
            | Records.Record.HR r -> r
            | other ->
                failwithf
                    "PayoffContractTests: All eight HR betting products preserve their full payoff and flag fields: %A"
                    other

        Assert.Equal(record, dispatched)
        Assert.Equal(62, mappings.Length)

        for id, path in mappings do
            let field = RecordOracle.field layout id

            for i in 0 .. (RecordOracle.positions layout field |> List.length) - 1 do
                Assert.Equal(
                    RecordOracle.text layout id i data,
                    RecordOracle.modelText record (path.Replace("{i}", string i))
                )

        Assert.Equal(28, record.RefundedHorses.Length)
        Assert.Equal(8, record.RefundedBrackets.Length)
        Assert.Equal(8, record.RefundedSameBrackets.Length)
        Assert.Equal<byte>(data, record.Raw)

    [<Fact; Trait("Category", "Contract")>]
    let ``HR array capacities amounts and popularity widths follow their betting products`` () =
        let r = Records.parseHR (fixture ()) |> value

        let groups =
            [ 42, r.Payouts.Win
              43, r.Payouts.Place
              44, r.Payouts.BracketQuinella
              45, r.Payouts.Quinella
              46, r.Payouts.Wide
              48, r.Payouts.Exacta
              49, r.Payouts.Trio
              50, r.Payouts.Trifecta ]

        Assert.Equal<int>([| 3; 5; 3; 3; 7; 6; 3; 6 |], groups |> List.map (fun (_, p) -> p.Length) |> List.toArray)

        for id, payouts in groups do
            for i, payout in Array.indexed payouts do
                Assert.Equal(Some(decimal (id * 1000 + i * 100)), payout.AmountYen.Value)
                Assert.Equal(Some(i + 1), payout.Popularity.Value)

        Assert.Equal(4, r.Payouts.Trifecta[0].Popularity.Raw.Length)
        Assert.Equal(Some false, r.Unformed.Win.Value)
        Assert.Equal(Some true, r.Unformed.Place.Value)

    [<Theory; Trait("Category", "Contract")>]
    [<InlineData("0")>]
    [<InlineData("1")>]
    [<InlineData("2")>]
    [<InlineData("9")>]
    let ``HR categories retain all initialized payout slots`` category =
        let data = RecordOracle.blank layout
        put "2" 0 category data
        let r = Records.parseHR data |> value
        Assert.Equal(category, r.Header.DataCategory)
        Assert.Equal<byte>(data, r.Raw)

    [<Fact; Trait("Category", "Contract")>]
    let ``Blank HR slots and unknown flags are retained without becoming false`` () =
        let data = fixture ()
        put "12" 0 "?" data
        let group = RecordOracle.field layout "50"
        Array.Fill(data, 32uy, (RecordOracle.positions layout group)[5], group.Length)
        let r = Records.parseHR data |> value
        Assert.Equal("?", r.Unformed.Win.Raw)
        Assert.Equal(None, r.Unformed.Win.Value)
        Assert.Equal(String(' ', 6), r.Payouts.Trifecta[5].Combination)
        Assert.Equal(None, r.Payouts.Trifecta[5].AmountYen.Value)

    [<Theory; Trait("Category", "Contract")>]
    [<InlineData("42.b", 0, "0000x0000", "Payouts.Win[0].AmountYen")>]
    [<InlineData("46.c", 3, "x01", "Payouts.Wide[3].Popularity")>]
    [<InlineData("50.a", 5, "ABCDEF", "Payouts.Trifecta[5].Combination")>]
    let ``HR malformed payout identifies record field and byte position`` id occurrence raw field =
        let data = fixture ()
        put id occurrence raw data

        match Records.parseHR data with
        | Ok _ -> failwith "Invalid payout accepted"
        | Error error ->
            Assert.Equal("HR", error.RecordId)
            Assert.Equal(field, error.Field)
            Assert.Equal((RecordOracle.positions layout (RecordOracle.field layout id))[occurrence] + 1, error.Position)

    [<Fact; Trait("Category", "Contract")>]
    let ``HR rejects wrong length identity and delimiter`` () =
        let data = fixture ()

        for input in [ null; [||]; data[.. data.Length - 2]; Array.append data [| 32uy |] ] do
            Assert.True(Records.parseHR input |> Result.isError)

        put "1" 0 "RA" data
        Assert.True(Records.parseHR data |> Result.isError)
        put "1" 0 "HR" data
        data[data.Length - 1] <- 32uy
        Assert.True(Records.parseHR data |> Result.isError)

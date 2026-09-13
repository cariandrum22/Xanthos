namespace Xanthos.UnitTests

open System
open Xunit
open Xanthos
open Xanthos.Data

module VotesContractTests =
    let private value =
        function
        | Ok value -> value
        | Error error -> failwithf "%A" error

    let private bets =
        [ "Win"; "Place"; "BracketQuinella"; "Quinella"; "Wide"; "Exacta"; "Trio" ]

    let internal mappings id =
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
          if id = "H1" then
              for i, bet in List.indexed bets do
                  string (12 + i), "Sales." + bet

              "19", "PlacePayoutRule"
              "20", "RefundedHorses[{i}]"
              "21", "RefundedBrackets[{i}]"
              "22", "RefundedSameBrackets[{i}]"

              for i, bet in List.indexed bets do
                  let group = string (23 + i)
                  group + ".a", "Entries." + bet + "[{i}].Combination"
                  group + ".b", "Entries." + bet + "[{i}].UnitsHundredYen"
                  group + ".c", "Entries." + bet + "[{i}].Popularity"
                  string (30 + i), "TotalUnitsHundredYen." + bet
                  string (37 + i), "RefundedUnitsHundredYen." + bet
          else
              "12", "Sale"
              "13", "RefundedHorses[{i}]"
              "14.a", "Entries[{i}].Combination"
              "14.b", "Entries[{i}].UnitsHundredYen"
              "14.c", "Entries[{i}].Popularity"
              "15", "TotalUnitsHundredYen"
              "16", "RefundedUnitsHundredYen" ]

    let private combinations =
        function
        | "Win"
        | "Place" ->
            [| for n in 1..28 do
                   sprintf "%02d" n |]
        | "BracketQuinella" ->
            [| for a in 1..8 do
                   for b in a..8 do
                       sprintf "%d%d" a b |]
        | "Quinella"
        | "Wide" ->
            [| for a in 1..17 do
                   for b in a + 1 .. 18 do
                       sprintf "%02d%02d" a b |]
        | "Exacta" ->
            [| for a in 1..18 do
                   for b in 1..18 do
                       if a <> b then
                           sprintf "%02d%02d" a b |]
        | "Trio" ->
            [| for a in 1..16 do
                   for b in a + 1 .. 17 do
                       for c in b + 1 .. 18 do
                           sprintf "%02d%02d%02d" a b c |]
        | "Trifecta" ->
            [| for a in 1..18 do
                   for b in 1..18 do
                       for c in 1..18 do
                           if a <> b && b <> c && a <> c then
                               sprintf "%02d%02d%02d" a b c |]
        | other -> failwith other

    let internal fixture id =
        let layout = RecordOracle.layout id
        let data = RecordOracle.blank layout

        let put field index raw =
            RecordOracle.write layout field index raw data

        for field, raw in
            [ "2", "5"
              "4", "2026"
              "5", "0912"
              "6", "05"
              "7", "04"
              "8", "02"
              "9", "11"
              "10", "18"
              "11", "17" ] do
            put field 0 raw

        let groups =
            if id = "H1" then
                List.indexed bets |> List.map (fun (i, bet) -> string (23 + i), bet)
            else
                [ "14", "Trifecta" ]

        if id = "H1" then
            for i in 0..6 do
                put (string (12 + i)) 0 "7"
                put (string (30 + i)) 0 (sprintf "%011d" (90000000000L + int64 i))
                put (string (37 + i)) 0 (sprintf "%011d" (80000000000L + int64 i))

            put "19" 0 "3"
        else
            put "12" 0 "7"
            put "15" 0 "99999999999"
            put "16" 0 "88888888888"

        for group, bet in groups do
            let combinations = combinations bet
            Assert.Equal((RecordOracle.field layout group).Repeat, combinations.Length)
            let rankWidth = (RecordOracle.field layout (group + ".c")).Length

            for i, combination in Array.indexed combinations do
                put (group + ".a") i combination
                put (group + ".b") i (sprintf "%011d" (98765400000L + int64 i))
                put (group + ".c") i ((i + 1).ToString().PadLeft(rankWidth, '0'))

        let flags = if id = "H1" then [ "20"; "21"; "22" ] else [ "13" ]

        for field in flags do
            for i in 0 .. (RecordOracle.field layout field).Repeat - 1 do
                put field i (string (i % 2))

        layout, data

    let private parse id data : obj =
        if id = "H1" then
            Records.parseH1 data |> value |> box
        else
            Records.parseH6 data |> value |> box

    [<Theory; Trait("Category", "Contract")>]
    [<InlineData("H1", 57)>]
    [<InlineData("H6", 18)>]
    let ``Every vote leaf and repeated combination matches the independent oracle`` id expectedMappings =
        let layout, data = fixture id
        let r = parse id data

        let dispatched =
            match Records.parse data |> value with
            | Records.Record.H1 r -> box r
            | Records.Record.H6 r -> box r
            | other -> failwithf "%A" other

        Assert.Equal(r, dispatched)
        Assert.Equal(expectedMappings, (mappings id).Length)

        for field, path in mappings id do
            let specification = RecordOracle.field layout field

            for i in 0 .. (RecordOracle.positions layout specification |> List.length) - 1 do
                Assert.Equal(
                    RecordOracle.text layout field i data,
                    RecordOracle.modelText r (path.Replace("{i}", string i))
                )

        let raw = r.GetType().GetProperty("Raw").GetValue(r) :?> byte[]
        Assert.Equal<byte>(data, raw)

    [<Fact; Trait("Category", "Contract")>]
    let ``H1 seven capacities and totals retain values above Int32`` () =
        let _, data = fixture "H1"
        let r = Records.parseH1 data |> value

        let groups =
            [ r.Entries.Win
              r.Entries.Place
              r.Entries.BracketQuinella
              r.Entries.Quinella
              r.Entries.Wide
              r.Entries.Exacta
              r.Entries.Trio ]

        Assert.Equal<int>([| 28; 28; 36; 153; 153; 306; 816 |], groups |> List.map Array.length |> List.toArray)

        for group in groups do
            for i, entry in Array.indexed group do
                Assert.Equal(Some(98765400000M + decimal i), entry.UnitsHundredYen.Value)
                Assert.Equal(Popularity.Rank(i + 1), entry.Popularity.Value)

        Assert.Equal(Some 90000000006M, r.TotalUnitsHundredYen.Trio.Value)
        Assert.Equal(Some 80000000006M, r.RefundedUnitsHundredYen.Trio.Value)
        Assert.Equal(PlacePayoutRule.ThreePlaces, r.PlacePayoutRule.Value)

    [<Fact; Trait("Category", "Contract")>]
    let ``H6 preserves every one of 4896 entries and the exact 102890 byte boundary`` () =
        let _, data = fixture "H6"
        let r = Records.parseH6 data |> value
        Assert.Equal(4896, r.Entries.Length)
        Assert.Equal(102890, r.Raw.Length)
        Assert.Equal(18, r.RefundedHorses.Length)

        for i, entry in Array.indexed r.Entries do
            Assert.Equal(Some(98765400000M + decimal i), entry.UnitsHundredYen.Value)
            Assert.Equal(Popularity.Rank(i + 1), entry.Popularity.Value)

        Assert.NotEqual<string>(r.Entries[0].Combination, r.Entries[2448].Combination)
        Assert.NotEqual<string>(r.Entries[2448].Combination, r.Entries[4895].Combination)
        Assert.Equal(Some 99999999999M, r.TotalUnitsHundredYen.Value)
        Assert.Equal(Some 88888888888M, r.RefundedUnitsHundredYen.Value)

    [<Theory; Trait("Category", "Contract")>]
    [<InlineData("H1")>]
    [<InlineData("H6")>]
    let ``Votes retain every official category and blank registration slots`` id =
        let layout = RecordOracle.layout id

        for category in [ "0"; "2"; "4"; "5"; "9" ] do
            let data = RecordOracle.blank layout
            RecordOracle.write layout "2" 0 category data
            let r = parse id data
            Assert.Equal(category, RecordOracle.modelText r "Header.DataCategory")

            let path =
                if id = "H1" then
                    "Entries.Win[0].UnitsHundredYen"
                else
                    "Entries[0].UnitsHundredYen"

            Assert.Equal(String(' ', 11), RecordOracle.modelText r path)

    [<Theory; Trait("Category", "Contract")>]
    [<InlineData("H1")>]
    [<InlineData("H6")>]
    let ``Votes distinguish cancellation markers and sale states without numeric coercion`` id =
        let layout, data = fixture id
        let group = if id = "H1" then "23" else "14"
        let width = (RecordOracle.field layout (group + ".c")).Length

        for flag, marker, expectedSale, expectedPopularity in
            [ "1", '-', SaleState.CancelledBeforeSale, Popularity.CancelledBeforeSale
              "3", '*', SaleState.CancelledAfterSale, Popularity.CancelledAfterSale ] do
            RecordOracle.write layout "12" 0 flag data
            RecordOracle.write layout (group + ".c") 0 (String(marker, width)) data

            let sale, popularity =
                if id = "H1" then
                    let r = Records.parseH1 data |> value in r.Sales.Win.Value, r.Entries.Win[0].Popularity.Value
                else
                    let r = Records.parseH6 data |> value in r.Sale.Value, r.Entries[0].Popularity.Value

            Assert.Equal(expectedSale, sale)
            Assert.Equal(expectedPopularity, popularity)

        RecordOracle.write layout "12" 0 "?" data

        if id = "H1" then
            Assert.Equal(SaleState.Unknown "?", (Records.parseH1 data |> value).Sales.Win.Value)
        else
            Assert.Equal(SaleState.Unknown "?", (Records.parseH6 data |> value).Sale.Value)

    [<Theory; Trait("Category", "Contract")>]
    [<InlineData("H1", "29.b", 815, "00000x00000", "Entries.Trio[815].UnitsHundredYen")>]
    [<InlineData("H6", "14.c", 4895, "12x4", "Entries[4895].Popularity")>]
    let ``Malformed final vote entry reports its actual offset`` id field index raw name =
        let layout, data = fixture id
        RecordOracle.write layout field index raw data

        let error =
            if id = "H1" then
                match Records.parseH1 data with
                | Error e -> e
                | _ -> failwith "Accepted malformed entry"
            else
                match Records.parseH6 data with
                | Error e -> e
                | _ -> failwith "Accepted malformed entry"

        Assert.Equal(id, error.RecordId)
        Assert.Equal(name, error.Field)
        Assert.Equal((RecordOracle.positions layout (RecordOracle.field layout field))[index] + 1, error.Position)

    [<Theory; Trait("Category", "Contract")>]
    [<InlineData("H1")>]
    [<InlineData("H6")>]
    let ``Votes reject null incorrect length ID and CRLF`` id =
        let layout, data = fixture id

        let invalid bytes =
            if id = "H1" then
                Records.parseH1 bytes |> Result.isError
            else
                Records.parseH6 bytes |> Result.isError

        for input in [ null; [||]; data[.. data.Length - 2]; Array.append data [| 32uy |] ] do
            Assert.True(invalid input)

        RecordOracle.write layout "1" 0 "RA" data
        Assert.True(invalid data)
        RecordOracle.write layout "1" 0 id data
        data[data.Length - 1] <- 32uy
        Assert.True(invalid data)

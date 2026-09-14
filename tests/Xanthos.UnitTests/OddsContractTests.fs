namespace Xanthos.UnitTests

open System
open Xunit
open Xanthos
open Xanthos.Data

module OddsContractTests =
    let private value =
        function
        | Ok value -> value
        | Error error -> failwithf "%A" error

    let private mappings id =
        [ "1", "Header.RecordId"
          "2", "Header.DataCategory"
          "3", "Header.CreatedDateRaw"
          "4", "Identity.Year"
          "5", "Identity.MonthDay"
          "6", "Identity.Racecourse"
          "7", "Identity.Meeting"
          "8", "Identity.Day"
          "9", "Identity.RaceNumber"
          "10", "Announcement"
          "11", "RegisteredCount"
          "12", "RunnerCount"
          if id = "O1" then
              "13", "WinSale"
              "14", "PlaceSale"
              "15", "BracketSale"
              "16", "PlacePayoutRule"
              "17.a", "Win[{i}].Combination"
              "17.b", "Win[{i}].Odds"
              "17.c", "Win[{i}].Popularity"
              "18.a", "Place[{i}].Combination"
              "18.b", "Place[{i}].Minimum"
              "18.c", "Place[{i}].Maximum"
              "18.d", "Place[{i}].Popularity"
              "19.a", "BracketQuinella[{i}].Combination"
              "19.b", "BracketQuinella[{i}].Odds"
              "19.c", "BracketQuinella[{i}].Popularity"
              "20", "WinTotalUnitsHundredYen"
              "21", "PlaceTotalUnitsHundredYen"
              "22", "BracketTotalUnitsHundredYen"
          else
              "13", "Sale"
              "14.a", "Entries[{i}].Combination"

              if id = "O3" then
                  "14.b", "Entries[{i}].Minimum"
                  "14.c", "Entries[{i}].Maximum"
                  "14.d", "Entries[{i}].Popularity"
              else
                  "14.b", "Entries[{i}].Odds"
                  "14.c", "Entries[{i}].Popularity"

              "15", "TotalUnitsHundredYen" ]

    let private combinations id group =
        if id = "O1" then
            if group = "19" then
                [| for a in 1..8 do
                       for b in a..8 do
                           sprintf "%d%d" a b |]
            else
                [| for n in 1..28 do
                       sprintf "%02d" n |]
        elif id = "O2" || id = "O3" then
            [| for a in 1..17 do
                   for b in a + 1 .. 18 do
                       sprintf "%02d%02d" a b |]
        elif id = "O4" then
            [| for a in 1..18 do
                   for b in 1..18 do
                       if a <> b then
                           sprintf "%02d%02d" a b |]
        elif id = "O5" then
            [| for a in 1..16 do
                   for b in a + 1 .. 17 do
                       for c in b + 1 .. 18 do
                           sprintf "%02d%02d%02d" a b c |]
        else
            [| for a in 1..18 do
                   for b in 1..18 do
                       for c in 1..18 do
                           if a <> b && a <> c && b <> c then
                               sprintf "%02d%02d%02d" a b c |]

    let private fixture id =
        let layout = RecordOracle.layout id
        let data = RecordOracle.blank layout

        let put field index raw =
            RecordOracle.write layout field index raw data

        for field, raw in
            [ "2", "1"
              "4", "2026"
              "5", "0912"
              "6", "05"
              "7", "04"
              "8", "02"
              "9", "11"
              "10", "09121345"
              "11", "18"
              "12", "17"
              "13", "7" ] do
            put field 0 raw

        if id = "O1" then
            put "14" 0 "7"
            put "15" 0 "7"
            put "16" 0 "3"

            for field in [ "20"; "21"; "22" ] do
                put field 0 "98765432100"
        else
            put "15" 0 "98765432100"

        for group in (if id = "O1" then [ "17"; "18"; "19" ] else [ "14" ]) do
            let combos = combinations id group
            Assert.Equal((RecordOracle.field layout group).Repeat, combos.Length)
            let isRange = (id = "O1" && group = "18") || id = "O3"

            for i, combo in Array.indexed combos do
                put (group + ".a") i combo

                put
                    (group + ".b")
                    i
                    ((100 + i).ToString().PadLeft((RecordOracle.field layout (group + ".b")).Length, '0'))

                if isRange then
                    put
                        (group + ".c")
                        i
                        ((200 + i).ToString().PadLeft((RecordOracle.field layout (group + ".c")).Length, '0'))

                let popularity = group + (if isRange then ".d" else ".c")
                put popularity i ((i + 1).ToString().PadLeft((RecordOracle.field layout popularity).Length, '0'))

        layout, data

    let private parse id format data : obj =
        match id with
        | "O1" -> Records.parseO1WithFormat format data |> value |> box
        | "O2" -> Records.parseO2WithFormat format data |> value |> box
        | "O3" -> Records.parseO3WithFormat format data |> value |> box
        | "O4" -> Records.parseO4WithFormat format data |> value |> box
        | "O5" -> Records.parseO5WithFormat format data |> value |> box
        | _ -> Records.parseO6WithFormat format data |> value |> box

    let private firstOdds id data =
        match id with
        | "O1" -> (Records.parseO1 data |> value).Win[0].Odds.Value
        | "O2" -> (Records.parseO2 data |> value).Entries[0].Odds.Value
        | "O3" -> (Records.parseO3 data |> value).Entries[0].Minimum.Value
        | "O4" -> (Records.parseO4 data |> value).Entries[0].Odds.Value
        | "O5" -> (Records.parseO5 data |> value).Entries[0].Odds.Value
        | _ -> (Records.parseO6 data |> value).Entries[0].Odds.Value

    [<Theory; Trait("Category", "Contract")>]
    [<InlineData("O1", 29)>]
    [<InlineData("O2", 17)>]
    [<InlineData("O3", 18)>]
    [<InlineData("O4", 17)>]
    [<InlineData("O5", 17)>]
    [<InlineData("O6", 17)>]
    let ``Every odds leaf and repeated slot matches the independent layout`` id expectedMappings =
        let layout, data = fixture id
        let record = parse id OddsLimitFormat.Current data

        let dispatched =
            match Records.parse data |> value with
            | Records.Record.O1 r -> box r
            | Records.Record.O2 r -> box r
            | Records.Record.O3 r -> box r
            | Records.Record.O4 r -> box r
            | Records.Record.O5 r -> box r
            | Records.Record.O6 r -> box r
            | other -> failwithf "%A" other

        Assert.Equal(record, dispatched)
        Assert.Equal(expectedMappings, (mappings id).Length)

        for field, path in mappings id do
            for i in
                0 .. (RecordOracle.positions layout (RecordOracle.field layout field) |> List.length)
                     - 1 do
                Assert.Equal(
                    RecordOracle.text layout field i data,
                    RecordOracle.modelText record (path.Replace("{i}", string i))
                )

        Assert.Equal<byte>(data, record.GetType().GetProperty("Raw").GetValue(record) :?> byte[])

    [<Fact; Trait("Category", "Contract")>]
    let ``O2 quinella and O3 wide have different meanings and scalar versus range quotes`` () =
        let _, o2 = fixture "O2"
        let _, o3 = fixture "O3"
        let quinella = Records.parseO2 o2 |> value
        let wide = Records.parseO3 o3 |> value
        Assert.Equal(RecordKind.QuinellaOdds, quinella.Kind)
        Assert.Equal(OddsValue.Quoted 10M, quinella.Entries[0].Odds.Value)
        Assert.Equal(OddsValue.Quoted 10M, wide.Entries[0].Minimum.Value)
        Assert.Equal(OddsValue.Quoted 20M, wide.Entries[0].Maximum.Value)
        Assert.True(Records.parseO2 o3 |> Result.isError)
        Assert.True(Records.parseO3 o2 |> Result.isError)

    [<Theory; Trait("Category", "Contract")>]
    [<InlineData("O1")>]
    [<InlineData("O2")>]
    [<InlineData("O3")>]
    [<InlineData("O4")>]
    [<InlineData("O5")>]
    [<InlineData("O6")>]
    let ``Odds preserve all categories and yearless announcement components`` id =
        let layout, data = fixture id

        for category in [ "0"; "1"; "2"; "3"; "4"; "5"; "9" ] do
            RecordOracle.write layout "2" 0 category data
            let record = parse id OddsLimitFormat.Current data
            Assert.Equal(category, RecordOracle.modelText record "Header.DataCategory")

        let announcement =
            (parse id OddsLimitFormat.Current data)
                .GetType()
                .GetProperty("Announcement")
                .GetValue(parse id OddsLimitFormat.Current data)
            :?> Sourced<AnnouncementTime option>

        Assert.Equal(
            Some
                { Month = 9
                  Day = 12
                  Hour = 13
                  Minute = 45 },
            announcement.Value
        )

        let blank = RecordOracle.blank layout
        let record = parse id OddsLimitFormat.Current blank

        let missing =
            record.GetType().GetProperty("Announcement").GetValue(record) :?> Sourced<AnnouncementTime option>

        Assert.Equal(None, missing.Value)

    [<Theory; Trait("Category", "Contract")>]
    [<InlineData("O1")>]
    [<InlineData("O2")>]
    [<InlineData("O3")>]
    [<InlineData("O4")>]
    [<InlineData("O5")>]
    [<InlineData("O6")>]
    let ``Odds distinguish no votes nonregistration and both cancellation markers`` id =
        let layout, data = fixture id
        let field = if id = "O1" then "17.b" else "14.b"
        let width = (RecordOracle.field layout field).Length

        for marker, expected in
            [ '0', OddsValue.NoVotes
              ' ', OddsValue.NotRegistered
              '-', OddsValue.CancelledBeforeSale
              '*', OddsValue.CancelledAfterSale ] do
            RecordOracle.write layout field 0 (String(marker, width)) data
            Assert.Equal(expected, firstOdds id data)

    [<Fact; Trait("Category", "Contract")>]
    let ``O1 historical cap selection is explicit and keeps the original quote`` () =
        let layout, data = fixture "O1"
        RecordOracle.write layout "18.b" 0 "0999" data
        RecordOracle.write layout "19.b" 0 "09999" data
        let current = Records.parseO1 data |> value
        let old = Records.parseO1WithFormat OddsLimitFormat.Before20040814 data |> value
        Assert.Equal(OddsValue.Quoted 99.9M, current.Place[0].Minimum.Value)
        Assert.Equal(OddsValue.AtLeast 99.9M, old.Place[0].Minimum.Value)
        Assert.Equal(OddsValue.Quoted 999.9M, current.BracketQuinella[0].Odds.Value)
        Assert.Equal(OddsValue.AtLeast 999.9M, old.BracketQuinella[0].Odds.Value)
        Assert.Equal(current.Place[0].Minimum.Raw, old.Place[0].Minimum.Raw)
        RecordOracle.write layout "18.b" 0 "1000" data
        Assert.True(Records.parseO1WithFormat OddsLimitFormat.Before20040814 data |> Result.isError)

    [<Theory; Trait("Category", "Contract")>]
    [<InlineData("O2")>]
    [<InlineData("O4")>]
    [<InlineData("O5")>]
    let ``Combination odds preserve the old limit marker with explicit format context`` id =
        let layout, data = fixture id
        RecordOracle.write layout "14.b" 0 "099999" data
        let current = parse id OddsLimitFormat.Current data :?> SingleOdds
        let old = parse id OddsLimitFormat.Before20040814 data :?> SingleOdds
        Assert.Equal(OddsValue.Quoted 9999.9M, current.Entries[0].Odds.Value)
        Assert.Equal(OddsValue.AtLeast 9999.9M, old.Entries[0].Odds.Value)

    [<Fact; Trait("Category", "Contract")>]
    let ``O1 and O3 documented upper limits remain lower bound values`` () =
        let l1, o1 = fixture "O1"
        RecordOracle.write l1 "17.b" 0 "9999" o1
        Assert.Equal(OddsValue.AtLeast 999.9M, firstOdds "O1" o1)
        let l3, o3 = fixture "O3"
        RecordOracle.write l3 "14.b" 0 "99999" o3
        Assert.Equal(OddsValue.AtLeast 9999.9M, firstOdds "O3" o3)

    [<Theory; Trait("Category", "Contract")>]
    [<InlineData("O1")>]
    [<InlineData("O2")>]
    [<InlineData("O3")>]
    [<InlineData("O4")>]
    [<InlineData("O5")>]
    [<InlineData("O6")>]
    let ``Odds validate length ID delimiter numeric data and partial date bounds`` id =
        let layout, data = fixture id
        let invalid input = Records.parse input |> Result.isError

        for input in [ null; [||]; data[.. data.Length - 2]; Array.append data [| 32uy |] ] do
            Assert.True(invalid input)

        let field = if id = "O1" then "17.b" else "14.b"
        RecordOracle.write layout field 0 (String('x', (RecordOracle.field layout field).Length)) data
        Assert.True(invalid data)
        let _, dateData = fixture id
        RecordOracle.write layout "10" 0 "02301345" dateData
        Assert.True(invalid dateData)
        RecordOracle.write layout "10" 0 "09122400" dateData
        Assert.True(invalid dateData)
        RecordOracle.write layout "10" 0 "09121345" dateData
        dateData[dateData.Length - 1] <- 32uy
        Assert.True(invalid dateData)

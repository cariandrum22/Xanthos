namespace Xanthos.UnitTests

open System
open Xunit
open Xanthos
open Xanthos.Data

module AdditionalFormatTests =
    open AdditionalRecordContractTests

    // Previous absolute positions independently transcribed from the 2023-08-08 layout change.
    let internal oldLayout id =
        let current = RecordOracle.layout id
        let size = Map.ofList [ "HN", 245; "SK", 178; "HS", 196; "BT", 6887; "CK", 6864 ]

        let change (f: RecordOracle.Field) =
            let p, n =
                match id, f.Id with
                | "HN", "4" -> 12, 8
                | "HN", "18" -> 228, 8
                | "HN", "19" -> 236, 8
                | "HN", "20" -> 244, 2
                | "HN", _ when f.Position >= 22 -> f.Position - 2, f.Length
                | "SK", "11" -> 39, 6
                | "SK", "12" -> 45, 20
                | "SK", "13" -> 65, 8
                | "SK", "14" -> 177, 2
                | "HS", "5" -> 22, 8
                | "HS", "6" -> 30, 8
                | "HS", _ when f.Position >= 42 -> f.Position - 4, f.Length
                | "BT", "4" -> 12, 8
                | "BT", _ when f.Position >= 22 -> f.Position - 2, f.Length
                | "CK", "98_producer" -> 6597, 6
                | "CK", "99" -> 6603, 70
                | "CK", "100" -> 6673, 70
                | "CK", "101" -> 6743, 60
                | "CK", "102" -> 6863, 2
                | _ -> f.Position, f.Length

            { f with Position = p; Length = n }

        { current with
            Length = size[id]
            Fields = Array.map change current.Fields }

    let private parse id format data =
        match id with
        | "HN" -> Records.parseHNWithFormat format data |> Result.map box
        | "SK" -> Records.parseSKWithFormat format data |> Result.map box
        | "HS" -> Records.parseHSWithFormat format data |> Result.map box
        | "BT" -> Records.parseBTWithFormat format data |> Result.map box
        | "CK" -> Records.parseCKWithFormat format data |> Result.map box
        | _ -> failwith id

    [<Theory; Trait("Category", "Contract")>]
    [<InlineData("HN")>]
    [<InlineData("SK")>]
    [<InlineData("HS")>]
    [<InlineData("BT")>]
    [<InlineData("CK")>]
    let ``Old identifiers preserve every source field with explicit format and original error positions`` id =
        let current, original = fixture id
        let old = oldLayout id
        let data = RecordOracle.blank old

        for field, _ in mapping id do
            let f = RecordOracle.field old field

            for i in 0 .. (RecordOracle.positions old f).Length - 1 do
                let bytes =
                    RecordOracle.text current field i original |> RecordOracle.encoding.GetBytes

                RecordOracle.write old field i (RecordOracle.encoding.GetString(bytes, 0, f.Length)) data

        let parsed = parse id IdentifierFormat.Legacy data |> value

        for field, path in mapping id do
            let f = RecordOracle.field old field

            for index in 0 .. (RecordOracle.positions old f).Length - 1 do
                let row, col =
                    if path.Contains("{j}") then
                        index / f.Repeat, index % f.Repeat
                    else
                        index, 0

                let path = path.Replace("{i}", string row).Replace("{j}", string col)
                Assert.Equal(RecordOracle.text old field index data, RecordOracle.modelText parsed path)

        let options =
            { Records.ParseOptions.Default with
                IdentifierFormat = IdentifierFormat.Legacy }

        let union = Records.parseWith options data |> value

        let _, fields =
            Microsoft.FSharp.Reflection.FSharpValue.GetUnionFields(union, typeof<Records.Record>)

        Assert.Equal(parsed, fields[0])
        Assert.True(Records.parse data |> Result.isError)
        Assert.True(parse id IdentifierFormat.Legacy original |> Result.isError)

        let f =
            RecordOracle.field
                old
                (if id = "CK" then "98_producer"
                 elif id = "SK" then "13"
                 elif id = "HS" then "6"
                 else "4")

        let offset = RecordOracle.positions old f |> List.last
        data[offset + f.Length - 1] <- byte 'X'
        let failure = parse id IdentifierFormat.Legacy data |> error
        Assert.Equal(offset + 1, failure.Position)
        Assert.Equal(f.Length, failure.Length)

    [<Theory; Trait("Category", "Contract")>]
    [<InlineData("HN")>]
    [<InlineData("SK")>]
    [<InlineData("HS")>]
    [<InlineData("HY")>]
    [<InlineData("CK")>]
    [<InlineData("HC")>]
    [<InlineData("WC")>]
    [<InlineData("WH")>]
    [<InlineData("JG")>]
    [<InlineData("YS")>]
    [<InlineData("BT")>]
    [<InlineData("CS")>]
    [<InlineData("DM")>]
    [<InlineData("TM")>]
    let ``Individual parsers equal canonical dispatch and reject a different record identifier and null`` id =
        let _, data = fixture id
        let moduleType = typeof<Records.Record>.DeclaringType
        let method = moduleType.GetMethod("parse" + id)
        Assert.NotNull method
        let result = method.Invoke(null, [| box data |])

        let case, fields =
            Microsoft.FSharp.Reflection.FSharpValue.GetUnionFields(result, result.GetType())

        Assert.Equal("Ok", case.Name)
        Assert.Equal(model data, fields[0])

        for bad in [ null; Array.copy data ] do
            if not (isNull bad) then
                bad[0] <- byte 'Z'
                bad[1] <- byte 'Z'

            let result = method.Invoke(null, [| box bad |])

            let case, _ =
                Microsoft.FSharp.Reflection.FSharpValue.GetUnionFields(result, result.GetType())

            Assert.Equal("Error", case.Name)

    [<Fact; Trait("Category", "Contract")>]
    let ``Training zero caps direction and source scale are explicit`` () =
        let h, hd = fixture "HC"
        let w, wd = fixture "WC"

        for layout, data, field in [ h, hd, "8"; w, wd, "11" ] do
            RecordOracle.write layout field 0 "9999" data

        Assert.Equal(TrainingTime.Seconds 999.9M, (Records.parseHC hd |> value).Totals[0].Value)
        Assert.Equal(TrainingTime.AtLeastSeconds 999.9M, (Records.parseWC wd |> value).Totals[0].Value)
        RecordOracle.write w "29" 0 "999" wd
        Assert.Equal(TrainingTime.AtLeastSeconds 99.9M, (Records.parseWC wd |> value).Laps[9].Value)
        RecordOracle.write w "29" 0 "000" wd
        RecordOracle.write w "9" 0 "0" wd
        let r = Records.parseWC wd |> value
        Assert.Equal(TrainingTime.Unmeasurable, r.Laps[9].Value)
        Assert.Equal(TrainingDirection.Right, r.Direction.Value)
        RecordOracle.write w "29" 0 "123" wd
        Assert.Equal(TrainingTime.Seconds 12.3M, (Records.parseWC wd |> value).Laps[9].Value)
        RecordOracle.write w "6" 0 "2460" wd
        Assert.Equal("Time", (Records.parseWC wd |> error).Field)

    [<Fact; Trait("Category", "Contract")>]
    let ``Mining converts minutes and hundredths and enforces score boundaries`` () =
        let d, dd = fixture "DM"
        let t, td = fixture "TM"
        let dm = Records.parseDM dd |> value
        Assert.Equal(Some 94.73M, dm.Horses[17].PredictedSeconds.Value)
        RecordOracle.write d "11.c" 17 "1234" dd
        RecordOracle.write d "11.d" 17 "0567" dd
        let dm = Records.parseDM dd |> value
        Assert.Equal(Some 12.34M, dm.Horses[17].FasterErrorSeconds.Value)
        Assert.Equal(Some 5.67M, dm.Horses[17].SlowerErrorSeconds.Value)
        RecordOracle.write d "11.b" 17 "16000" dd
        Assert.Equal("Horses[17].PredictedSeconds", (Records.parseDM dd |> error).Field)

        for raw, expected in [ "0000", 0M; "0765", 76.5M; "1000", 100M ] do
            RecordOracle.write t "11.b" 17 raw td
            Assert.Equal(Some expected, (Records.parseTM td |> value).Horses[17].Score.Value)

        RecordOracle.write t "11.b" 17 "1001" td
        Assert.Equal("Horses[17].Score", (Records.parseTM td |> error).Field)

    [<Fact; Trait("Category", "Contract")>]
    let ``Horse weight distinguishes withdrawals measurement failures and signed changes`` () =
        let layout, data = fixture "WH"

        for raw, expected in
            [ "000", BodyWeight.Withdrawn
              "999", BodyWeight.Unmeasurable
              "   ", BodyWeight.Missing
              "998", BodyWeight.Kilograms 998
              "002", BodyWeight.Kilograms 2 ] do
            RecordOracle.write layout "11.p39" 17 raw data
            Assert.Equal(expected, (Records.parseWH data |> value).Horses[17].Weight.Value)

        RecordOracle.write layout "11.p43" 17 "012" data
        Assert.Equal(WeightChange.Kilograms -12, (Records.parseWH data |> value).Horses[17].Change.Value)
        RecordOracle.write layout "11.p43" 17 "999" data
        Assert.Equal(WeightChange.Unmeasurable, (Records.parseWH data |> value).Horses[17].Change.Value)
        RecordOracle.write layout "11.p39" 17 "001" data
        Assert.Equal("Horses[17].Weight", (Records.parseWH data |> error).Field)

    [<Fact; Trait("Category", "Contract")>]
    let ``Unknown local classifications retain exact codes`` () =
        let h, hd = fixture "HN"
        RecordOracle.write h "15" 0 "X" hd
        Assert.Equal(ImportKind.Unknown "X", (Records.parseHN hd |> value).ImportKind.Value)
        let j, jd = fixture "JG"
        RecordOracle.write j "13" 0 "X" jd
        RecordOracle.write j "14" 0 "Y" jd
        let r = Records.parseJG jd |> value
        Assert.Equal(EntryStatus.Unknown "X", r.EntryStatus.Value)
        Assert.Equal(ExclusionStatus.Unknown "Y", r.ExclusionStatus.Value)

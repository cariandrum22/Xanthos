namespace Xanthos.UnitTests

open System
open Xunit
open Xanthos

module OwnershipContractTests =
    let private value =
        function
        | Ok v -> v
        | Error e -> failwithf "%A" e

    let private error =
        function
        | Error e -> e
        | Ok _ -> failwith "Expected a parsing error"

    let private parse id data =
        if id = "BR" then
            Records.parseBR data |> Result.map box
        else
            Records.parseBN data |> Result.map box

    let internal mapping id =
        [ "1", "Header.RecordId"
          "2", "Header.DataCategory"
          "3", "Header.CreatedDateRaw"
          "4", (if id = "BR" then "BreederId" else "OwnerId")
          "5", "Name"
          "6", "NameWithoutLegalForm"
          "7", "KanaName"
          "8", "EuropeanName"
          "9", (if id = "BR" then "Address" else "RacingColours")
          "10.a", "Performances[{i}].Year"
          "10.b", "Performances[{i}].BasePrizeHundredYen"
          "10.c", "Performances[{i}].AddedPrizeHundredYen"
          "10.d", "Performances[{i}].FinishCounts[{j}]" ]

    let internal fixture id =
        let layout = RecordOracle.layout id
        let data = RecordOracle.blank layout

        for field, path in mapping id do
            let f = RecordOracle.field layout field

            for index in 0 .. (RecordOracle.positions layout f).Length - 1 do
                let raw =
                    match field with
                    | "1" -> id
                    | "2" -> "2"
                    | "3" -> "20260912"
                    | "5"
                    | "6"
                    | "8" -> ("牧場" + field).PadRight(f.Length - 2, ' ')
                    | "7" -> "ﾎﾞｸｼﾞｮｳ".PadRight(f.Length, ' ')
                    | "9" -> "赤" + String('　', f.Length / 2 - 1)
                    | "10.a" -> string (2026 - index)
                    | "10.b"
                    | "10.c" -> string (9000000000L + int64 index)
                    | _ -> string (index + 1) |> fun s -> s.PadLeft(f.Length, '0')

                RecordOracle.write layout field index raw data

        layout, data

    [<Theory; Trait("Category", "Contract")>]
    [<InlineData("BR")>]
    [<InlineData("BN")>]
    let ``Ownership models match every source leaf and nested year finish combination`` id =
        let layout, data = fixture id
        let model = parse id data |> value
        let maps = mapping id
        Assert.Equal(13, maps.Length)
        let leaves = layout.Fields |> Array.filter (fun f -> f.Id <> "10" && f.Id <> "11")
        Assert.Equal<string>(leaves |> Array.map _.Id |> Array.sort, maps |> List.map fst |> List.sort)

        for field, path in maps do
            for index in 0 .. (RecordOracle.positions layout (RecordOracle.field layout field)).Length - 1 do
                let row, col = if field = "10.d" then index / 6, index % 6 else index, 0
                let actualPath = path.Replace("{i}", string row).Replace("{j}", string col)
                Assert.Equal(RecordOracle.text layout field index data, RecordOracle.modelText model actualPath)

        let record = Records.parse data |> value

        let _, fields =
            Microsoft.FSharp.Reflection.FSharpValue.GetUnionFields(record, typeof<Records.Record>)

        Assert.Equal(model, fields[0])
        let raw = model.GetType().GetProperty("Raw").GetValue(model) :?> byte[]
        Assert.Equal<byte>(data, raw)
        data[0] <- 0uy
        Assert.NotEqual(data[0], raw[0])

    [<Theory; Trait("Category", "Contract")>]
    [<InlineData("BR")>]
    [<InlineData("BN")>]
    let ``Ownership creation update and deletion preserve absent and zero amounts`` id =
        let layout = RecordOracle.layout id

        for category in "012" do
            let data = RecordOracle.blank layout
            data[2] <- byte category
            parse id data |> value |> ignore

        let layout, data = fixture id
        RecordOracle.write layout "10.b" 0 "          " data
        RecordOracle.write layout "10.b" 1 "0000000000" data

        let performances =
            if id = "BR" then
                (Records.parseBR data |> value).Performances
            else
                (Records.parseBN data |> value).Performances

        Assert.Equal(None, performances[0].BasePrizeHundredYen.Value)
        Assert.Equal(Some 0M, performances[1].BasePrizeHundredYen.Value)
        Assert.Equal(Some 9000000001M, performances[1].AddedPrizeHundredYen.Value)

    [<Theory; Trait("Category", "Contract")>]
    [<InlineData("BR")>]
    [<InlineData("BN")>]
    let ``Ownership parsers reject wrong lengths identifiers dates encodings and last nested number`` id =
        let layout, original = fixture id

        for data in [ null; original[.. original.Length - 2]; Array.append original [| 0uy |] ] do
            Assert.Equal("RecordLength", (parse id data |> error).Field)

        for field, bad in [ "1", "ZZ"; "2", "9"; "3", "20260230" ] do
            let data = Array.copy original
            RecordOracle.write layout field 0 bad data
            Assert.Equal(id, (parse id data |> error).RecordId)

        let data = Array.copy original
        RecordOracle.write layout "10.d" 11 "12345X" data
        Assert.Equal("Performances[1].FinishCounts[5]", (parse id data |> error).Field)
        let data = Array.copy original
        let f = RecordOracle.field layout "8"
        data[f.Position + f.Length - 2] <- 0x82uy
        Assert.Equal("EuropeanName", (parse id data |> error).Field)
        let data = Array.copy original
        data[data.Length - 2] <- 0uy
        Assert.True(parse id data |> Result.isError)

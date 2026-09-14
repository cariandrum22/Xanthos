namespace Xanthos.UnitTests

open System
open Xunit
open Xanthos
open Xanthos.Data

module IdentifierFormatTests =
    let private value =
        function
        | Ok v -> v
        | Error e -> failwithf "%A" e

    let private error =
        function
        | Error e -> e
        | Ok _ -> failwith "Expected parsing error"

    // Independent old-layout expectations from the XLSX change log, 2023-08-08.
    // Do not use the runtime's width/offset adjustments to construct this oracle.
    let private layout id =
        let current = RecordOracle.layout id

        let change (f: RecordOracle.Field) =
            if id = "BR" then
                let positions =
                    Map.ofList
                        [ "4", (12, 6)
                          "5", (18, 70)
                          "6", (88, 70)
                          "7", (158, 70)
                          "8", (228, 168)
                          "9", (396, 20)
                          "10", (416, 60)
                          "11", (536, 2) ]

                match Map.tryFind f.Id positions with
                | Some(p, n) -> { f with Position = p; Length = n }
                | None -> f
            else
                match f.Id with
                | "18" -> { f with Length = 44 }
                | "18.a" -> { f with Length = 8 }
                | "18.b" -> { f with Position = 9 }
                | "23" -> { f with Position = 855; Length = 6 }
                | "24" -> { f with Position = 861; Length = 70 }
                | _ when f.Parent.IsNone && f.Position >= 963 -> { f with Position = f.Position - 32 }
                | _ when f.Parent.IsNone && f.Position >= 849 -> { f with Position = f.Position - 28 }
                | _ -> f

        { current with
            Length = (if id = "BR" then 537 else 1577)
            Fields = Array.map change current.Fields }

    let internal fixture id =
        let old = layout id
        let data = RecordOracle.blank old

        let mapping =
            if id = "BR" then
                OwnershipContractTests.mapping id
            else
                HorseContractTests.mapping

        for field, path in mapping do
            let f = RecordOracle.field old field

            for i in 0 .. (RecordOracle.positions old f).Length - 1 do
                let raw =
                    if field = "1" then
                        id
                    elif field = "2" then
                        "2"
                    elif field = "3" then
                        "20260912" // Old DIFF format is still old when delivered after 2023.
                    elif id = "UM" && List.contains field [ "6"; "7"; "8" ] then
                        "00000000"
                    elif f.Initial.Contains("Ｓ") then
                        ("旧" + string i).PadRight(f.Length - 1, ' ')
                    elif f.Initial = "sp" && f.Length > 15 then
                        "Old".PadRight(f.Length, ' ')
                    else
                        string ((i + 1) % 10) |> fun s -> s.PadLeft(f.Length, '0')

                RecordOracle.write old field i raw data

        old, data, mapping

    [<Theory; Trait("Category", "Contract")>]
    [<InlineData("BR")>]
    [<InlineData("UM")>]
    let ``Explicit legacy format projects every field without padding identifiers or losing source bytes`` id =
        let old, data, mapping = fixture id

        let model =
            if id = "BR" then
                Records.parseBRWithFormat IdentifierFormat.Legacy data |> value |> box
            else
                Records.parseUMWithFormat IdentifierFormat.Legacy data |> value |> box

        for field, path in mapping do
            for i in 0 .. (RecordOracle.positions old (RecordOracle.field old field)).Length - 1 do
                let row, col = if id = "BR" && field = "10.d" then i / 6, i % 6 else i, 0
                let target = path.Replace("{i}", string row).Replace("{j}", string col)
                Assert.Equal(RecordOracle.text old field i data, RecordOracle.modelText model target)

        let raw = model.GetType().GetProperty("Raw").GetValue(model) :?> byte[]
        Assert.Equal<byte>(data, raw)
        Assert.Equal(box IdentifierFormat.Legacy, model.GetType().GetProperty("Format").GetValue(model))

        if id = "BR" then
            Assert.Equal(6, (Records.parseBRWithFormat IdentifierFormat.Legacy data |> value).BreederId.Length)
            Assert.True(Records.parseBR data |> Result.isError)
        else
            let record = Records.parseUMWithFormat IdentifierFormat.Legacy data |> value
            Assert.Equal(8, record.Ancestors[13].BreedingId.Length)
            Assert.Equal(6, record.BreederId.Length)
            Assert.True(Records.parseUM data |> Result.isError)

        Assert.True(Records.parse data |> Result.isError)

        let options =
            { Records.ParseOptions.Default with
                IdentifierFormat = IdentifierFormat.Legacy }

        let dispatched = Records.parseWith options data |> value

        let _, fields =
            Microsoft.FSharp.Reflection.FSharpValue.GetUnionFields(dispatched, typeof<Records.Record>)

        Assert.Equal(model, fields[0])

    [<Theory; Trait("Category", "Contract")>]
    [<InlineData("BR")>]
    [<InlineData("UM")>]
    let ``Legacy errors refer to original byte coordinates and current bytes require expanded selection`` id =
        let old, data, _ = fixture id

        let parse =
            if id = "BR" then
                Records.parseBRWithFormat IdentifierFormat.Legacy >> Result.map box
            else
                Records.parseUMWithFormat IdentifierFormat.Legacy >> Result.map box

        let field, i, bad =
            if id = "BR" then
                "10.d", 11, "12345X"
            else
                "18.a", 13, "1234567X"

        RecordOracle.write old field i bad data
        let e = parse data |> error
        Assert.Equal((RecordOracle.positions old (RecordOracle.field old field))[i] + 1, e.Position)
        Assert.Equal((RecordOracle.field old field).Length, e.Length)
        Assert.Equal<byte>(RecordOracle.encoding.GetBytes bad, e.Raw)
        let current = RecordOracle.blank (RecordOracle.layout id)
        Assert.Equal("RecordLength", (parse current |> error).Field)

namespace Xanthos.UnitTests

open System
open Xunit
open Xanthos

module HorseContractTests =
    let private value =
        function
        | Ok v -> v
        | Error e -> failwithf "%A" e

    let private error =
        function
        | Error e -> e
        | Ok _ -> failwith "Expected a parsing error"

    let internal mapping =
        [ "1", "Header.RecordId"
          "2", "Header.DataCategory"
          "3", "Header.CreatedDateRaw"
          "4", "PedigreeId"
          "5", "Deregistered"
          "6", "RegisteredDate"
          "7", "DeregisteredDate"
          "8", "BirthDate"
          "9", "Name"
          "10", "KanaName"
          "11", "EuropeanName"
          "12", "AtJraFacility"
          "14", "Symbol"
          "15", "Sex"
          "16", "Breed"
          "17", "Coat"
          "18.a", "Ancestors[{i}].BreedingId"
          "18.b", "Ancestors[{i}].Name"
          "19", "Affiliation"
          "20", "TrainerId"
          "21", "TrainerAbbreviation"
          "22", "InvitationRegion"
          "23", "BreederId"
          "24", "BreederName"
          "25", "Birthplace"
          "26", "OwnerId"
          "27", "OwnerName"
          "28", "FlatBasePrizeHundredYen"
          "29", "JumpBasePrizeHundredYen"
          "30", "FlatAddedPrizeHundredYen"
          "31", "JumpAddedPrizeHundredYen"
          "32", "FlatEarnedPrizeHundredYen"
          "33", "JumpEarnedPrizeHundredYen"
          "34", "FinishCounts.Overall[{i}]"
          "35", "FinishCounts.Central[{i}]"
          "36", "FinishCounts.TurfStraight[{i}]"
          "37", "FinishCounts.TurfRight[{i}]"
          "38", "FinishCounts.TurfLeft[{i}]"
          "39", "FinishCounts.DirtStraight[{i}]"
          "40", "FinishCounts.DirtRight[{i}]"
          "41", "FinishCounts.DirtLeft[{i}]"
          "42", "FinishCounts.Jump[{i}]"
          "43", "FinishCounts.TurfFirm[{i}]"
          "44", "FinishCounts.TurfGood[{i}]"
          "45", "FinishCounts.TurfYielding[{i}]"
          "46", "FinishCounts.TurfSoft[{i}]"
          "47", "FinishCounts.DirtFirm[{i}]"
          "48", "FinishCounts.DirtGood[{i}]"
          "49", "FinishCounts.DirtYielding[{i}]"
          "50", "FinishCounts.DirtSoft[{i}]"
          "51", "FinishCounts.JumpFirm[{i}]"
          "52", "FinishCounts.JumpGood[{i}]"
          "53", "FinishCounts.JumpYielding[{i}]"
          "54", "FinishCounts.JumpSoft[{i}]"
          "55", "FinishCounts.TurfUpTo1600[{i}]"
          "56", "FinishCounts.Turf1601To2200[{i}]"
          "57", "FinishCounts.TurfOver2200[{i}]"
          "58", "FinishCounts.DirtUpTo1600[{i}]"
          "59", "FinishCounts.Dirt1601To2200[{i}]"
          "60", "FinishCounts.DirtOver2200[{i}]"
          "61", "RunningStyleCounts[{i}]"
          "62", "RecordedRaceCount" ]

    let private fixture () =
        let layout = RecordOracle.layout "UM"
        let data = RecordOracle.blank layout

        for field, path in mapping do
            let f = RecordOracle.field layout field

            for i in 0 .. (RecordOracle.positions layout f).Length - 1 do
                let raw =
                    match field with
                    | "1" -> "UM"
                    | "2" -> "4"
                    | "3"
                    | "6" -> "20260912"
                    | "7" -> "00000000"
                    | "8" -> "20200229"
                    | "9"
                    | "18.b"
                    | "21"
                    | "22"
                    | "24"
                    | "25"
                    | "27" -> ("馬" + string i).PadRight(f.Length - 1, ' ')
                    | "10" -> "ｳﾏ".PadRight(f.Length, ' ')
                    | "11" -> "Horse".PadRight(f.Length, ' ')
                    | "5"
                    | "12" -> "1"
                    | _ ->
                        let n = if field.Contains('.') then 10 + i else int field + i
                        (string (n % (if f.Length = 1 then 10 else 1000))).PadLeft(f.Length, '0')

                RecordOracle.write layout field i raw data

        layout, data

    [<Fact; Trait("Category", "Contract")>]
    let ``Horse projection covers every nonreserved field fourteen ancestors and all finish counts`` () =
        let layout, data = fixture ()
        let record = Records.parseUM data |> value
        Assert.Equal(62, mapping.Length)

        let leaves =
            layout.Fields
            |> Array.filter (fun f -> f.Id <> "13" && f.Id <> "18" && f.Id <> "63")

        Assert.Equal<string>(leaves |> Array.map _.Id |> Array.sort, mapping |> List.map fst |> List.sort)

        for field, path in mapping do
            for i in 0 .. (RecordOracle.positions layout (RecordOracle.field layout field)).Length - 1 do
                Assert.Equal(
                    RecordOracle.text layout field i data,
                    RecordOracle.modelText record (path.Replace("{i}", string i))
                )

        match Records.parse data |> value with
        | Records.Record.UM dispatched -> Assert.Equal(record, dispatched)
        | other -> failwithf "Wrong dispatch %A" other

        Assert.Equal<byte>(data, record.Raw)
        data[0] <- 0uy
        Assert.Equal(byte 'U', record.Raw[0])
        Assert.Equal(Some(DateOnly(2020, 2, 29)), record.BirthDate.Value)
        Assert.Equal(None, record.DeregisteredDate.Value)
        Assert.Equal(14, record.Ancestors.Length)
        Assert.Equal(4, record.RunningStyleCounts.Length)

    [<Theory; Trait("Category", "Contract")>]
    [<InlineData("0")>]
    [<InlineData("1")>]
    [<InlineData("2")>]
    [<InlineData("3")>]
    [<InlineData("4")>]
    [<InlineData("9")>]
    let ``Horse categories preserve initial values and unknown flags`` category =
        let layout = RecordOracle.layout "UM"
        let data = RecordOracle.blank layout
        RecordOracle.write layout "2" 0 category data
        let record = Records.parseUM data |> value
        Assert.Equal(Some false, record.Deregistered.Value)
        Assert.Equal(None, record.AtJraFacility.Value)
        Assert.Equal(Some 0M, record.FlatBasePrizeHundredYen.Value)
        RecordOracle.write layout "12" 0 "X" data
        Assert.Equal(None, (Records.parseUM data |> value).AtJraFacility.Value)
        Assert.Equal("X", (Records.parseUM data |> value).AtJraFacility.Raw)

    [<Fact; Trait("Category", "Contract")>]
    let ``Horse parser locates malformed body fields and rejects a wrong layout`` () =
        let layout, original = fixture ()

        for data in [ null; original[.. original.Length - 2]; Array.append original [| 0uy |] ] do
            Assert.Equal("RecordLength", (Records.parseUM data |> error).Field)

        for field, occurrence, bad, path in
            [ "1", 0, "ZZ", "RecordId"
              "2", 0, "7", "DataCategory"
              "8", 0, "20210229", "BirthDate"
              "18.a", 13, "123456789X", "Ancestors[13].BreedingId"
              "60", 5, "99X", "FinishCounts.DirtOver2200[5]"
              "62", 0, "-01", "RecordedRaceCount" ] do
            let data = Array.copy original
            RecordOracle.write layout field occurrence bad data
            Assert.Equal(path, (Records.parseUM data |> error).Field)

        let data = Array.copy original
        let f = RecordOracle.field layout "9"
        data[f.Position + f.Length - 2] <- 0x82uy
        Assert.Equal("Name", (Records.parseUM data |> error).Field)
        let data = Array.copy original
        data[data.Length - 1] <- 0uy
        Assert.True(Records.parseUM data |> Result.isError)

namespace Xanthos.UnitTests

open System
open Xunit
open Xanthos
open Xanthos.Data

module RaceRecordContractTests =
    let private value =
        function
        | Ok v -> v
        | Error e -> failwithf "%A" e

    let private error =
        function
        | Error e -> e
        | Ok _ -> failwith "Expected a parsing error"

    let private mapping =
        [ "1", "Header.RecordId"
          "2", "Header.DataCategory"
          "3", "Header.CreatedDateRaw"
          "4", "Kind"
          "5", "Identity.Year"
          "6", "Identity.MonthDay"
          "7", "Identity.Racecourse"
          "8", "Identity.Meeting"
          "9", "Identity.Day"
          "10", "Identity.RaceNumber"
          "11", "SpecialRaceNumber"
          "12", "Title"
          "13", "Grade"
          "14", "RaceKind"
          "15", "DistanceMetres"
          "16", "Track"
          "17", "TimeKind"
          "18", "TimeSeconds"
          "19", "Weather"
          "20", "Turf"
          "21", "Dirt"
          "22.a", "Holders[{i}].PedigreeId"
          "22.b", "Holders[{i}].Name"
          "22.c", "Holders[{i}].Symbol"
          "22.d", "Holders[{i}].Sex"
          "22.e", "Holders[{i}].TrainerId"
          "22.f", "Holders[{i}].TrainerName"
          "22.g", "Holders[{i}].WeightKilograms"
          "22.h", "Holders[{i}].JockeyId"
          "22.i", "Holders[{i}].JockeyName" ]

    let private fixture () =
        let layout = RecordOracle.layout "RC"
        let data = RecordOracle.blank layout

        for field, path in mapping do
            let f = RecordOracle.field layout field

            for i in 0 .. (RecordOracle.positions layout f).Length - 1 do
                let raw =
                    match field with
                    | "1" -> "RC"
                    | "2" -> "1"
                    | "3" -> "20260912"
                    | "5" -> "2026"
                    | "6" -> "0912"
                    | "12"
                    | "22.b"
                    | "22.f"
                    | "22.i" -> ("馬" + string i).PadRight(f.Length - 1, ' ')
                    | "13" -> "A"
                    | "18" -> "1572"
                    | "22.g" -> string (550 + i)
                    | _ -> (string (i + 1)).PadLeft(f.Length, '0')

                RecordOracle.write layout field i raw data

        layout, data

    [<Fact; Trait("Category", "Contract")>]
    let ``Record master projects the shifted race identity and all three complete holders`` () =
        let layout, data = fixture ()
        let record = Records.parseRC data |> value
        Assert.Equal(30, mapping.Length)
        let leaves = layout.Fields |> Array.filter (fun f -> f.Id <> "22" && f.Id <> "23")
        Assert.Equal<string>(leaves |> Array.map _.Id |> Array.sort, mapping |> List.map fst |> List.sort)

        for field, path in mapping do
            for i in 0 .. (RecordOracle.positions layout (RecordOracle.field layout field)).Length - 1 do
                Assert.Equal(
                    RecordOracle.text layout field i data,
                    RecordOracle.modelText record (path.Replace("{i}", string i))
                )

        match Records.parse data |> value with
        | Records.Record.RC actual -> Assert.Equal(record, actual)
        | _ -> failwith "Wrong record dispatch"

        Assert.Equal(Some 117.2M, record.TimeSeconds.Value)
        Assert.Equal(Some 55.2M, record.Holders[2].WeightKilograms.Value)
        Assert.Equal(RaceRecordKind.Course, record.Kind.Value)
        Assert.Equal(RecordTimeKind.Standard, record.TimeKind.Value)
        Assert.Equal<byte>(data, record.Raw)
        data[0] <- 0uy
        Assert.Equal(byte 'R', record.Raw[0])

    [<Theory; Trait("Category", "Contract")>]
    [<InlineData("0")>]
    [<InlineData("1")>]
    let ``Record master initial and deletion bodies retain unset time and unknown distinctions`` category =
        let layout = RecordOracle.layout "RC"
        let data = RecordOracle.blank layout
        RecordOracle.write layout "2" 0 category data
        let record = Records.parseRC data |> value
        Assert.Equal(None, record.TimeSeconds.Value)
        Assert.Equal(RaceRecordKind.Unknown "0", record.Kind.Value)
        RecordOracle.write layout "4" 0 "X" data
        RecordOracle.write layout "17" 0 "Y" data
        let record = Records.parseRC data |> value
        Assert.Equal(RaceRecordKind.Unknown "X", record.Kind.Value)
        Assert.Equal(RecordTimeKind.Unknown "Y", record.TimeKind.Value)

    [<Fact; Trait("Category", "Contract")>]
    let ``Record master rejects invalid times dates numbers text and framing with field errors`` () =
        let layout, original = fixture ()

        for data in [ null; original[.. original.Length - 2] ] do
            Assert.Equal("RecordLength", (Records.parseRC data |> error).Field)

        for field, i, bad, path in
            [ "1", 0, "ZZ", "RecordId"
              "2", 0, "9", "DataCategory"
              "6", 0, "0230", "Identity.Date"
              "18", 0, "1999", "TimeSeconds"
              "22.g", 2, "-55", "Holders[2].WeightKilograms" ] do
            let data = Array.copy original
            RecordOracle.write layout field i bad data
            Assert.Equal(path, (Records.parseRC data |> error).Field)

        let data = Array.copy original
        let f = RecordOracle.field layout "12"
        data[f.Position + f.Length - 2] <- 0x82uy
        Assert.Equal("Title", (Records.parseRC data |> error).Field)
        let data = Array.copy original
        data[data.Length - 1] <- 0uy
        Assert.True(Records.parseRC data |> Result.isError)

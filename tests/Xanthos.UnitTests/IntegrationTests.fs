module Xanthos.UnitTests.IntegrationTests

open System
open Xunit
open Xanthos.Core
open Xanthos.Core.Text
open Xanthos.Core.Records
open Xanthos.Core.Records.RecordParser
open Xanthos.Core.Records.CodeTables
open Xanthos.Core.Records.TK
open Xanthos.Core.Records.RA
open Xanthos.Core.Records.SE
open Xanthos.Core.Records.HR
open Xanthos.Core.Records.O1
open Xanthos.Core.Records.H1
open Xanthos.Core.Records.WF
open Xanthos.Core.Records.UM

// ============================================================================
// Integration Tests - Multiple Record Parsing
// ============================================================================

[<Fact>]
let ``Parse multiple records of same type successfully`` () =
    let raceKey1 = "2024050512345678"
    let raceKey2 = "2024050612345679"

    // Create two O1 records
    let data1 = Array.create 100 32uy
    Array.Copy(encodeShiftJis "O1", 0, data1, 0, 2)
    Array.Copy(encodeShiftJis raceKey1, 0, data1, 2, 16)
    Array.Copy(encodeShiftJis "03", 0, data1, 18, 2)
    Array.Copy(encodeShiftJis "0025", 0, data1, 20, 4)

    let data2 = Array.create 100 32uy
    Array.Copy(encodeShiftJis "O1", 0, data2, 0, 2)
    Array.Copy(encodeShiftJis raceKey2, 0, data2, 2, 16)
    Array.Copy(encodeShiftJis "05", 0, data2, 18, 2)
    Array.Copy(encodeShiftJis "0038", 0, data2, 20, 4)

    match (O1.parse data1, O1.parse data2) with
    | (Ok record1, Ok record2) ->
        Assert.Equal(raceKey1, record1.RaceKey)
        Assert.Equal(raceKey2, record2.RaceKey)
        Assert.Equal(Some 3, record1.HorseNumber)
        Assert.Equal(Some 5, record2.HorseNumber)
    | _ -> failwith "Both records should parse successfully"

[<Fact>]
let ``Parse different record types with same race key`` () =
    let raceKey = "2024050512345678"

    // Create TK record
    let tkData = Array.create 346 32uy
    Array.Copy(encodeShiftJis "TK", 0, tkData, 0, 2)
    Array.Copy(encodeShiftJis raceKey, 0, tkData, 2, 16)
    Array.Copy(encodeShiftJis "2020105678", 0, tkData, 18, 10) // HorseId required
    Array.Copy(encodeShiftJis "TestHorse", 0, tkData, 28, 9) // HorseName required

    // Create RA record
    let raData = Array.create 366 32uy
    Array.Copy(encodeShiftJis "RA", 0, raData, 0, 2)
    Array.Copy(encodeShiftJis raceKey, 0, raData, 2, 16)
    Array.Copy(encodeShiftJis "TestRace", 0, raData, 18, 8) // RaceName required
    Array.Copy(encodeShiftJis "05", 0, raData, 68, 2) // Racecourse code: Tokyo

    // Create SE record
    let seData = Array.create 1446 32uy
    Array.Copy(encodeShiftJis "SE", 0, seData, 0, 2)
    Array.Copy(encodeShiftJis raceKey, 0, seData, 2, 16)
    Array.Copy(encodeShiftJis "2020105678", 0, seData, 18, 10) // HorseId required
    Array.Copy(encodeShiftJis "TestHorse", 0, seData, 28, 9) // HorseName required
    Array.Copy(encodeShiftJis "03", 0, seData, 65, 2) // Horse number

    match (TK.parse tkData, RA.parse raData, SE.parse seData) with
    | (Ok tk, Ok ra, Ok se) ->
        // All records should have the same race key
        Assert.Equal(raceKey, tk.RaceKey)
        Assert.Equal(raceKey, ra.RaceKey)
        Assert.Equal(raceKey, se.RaceKey)

        // RA should have racecourse code
        Assert.Equal(Some RacecourseCode.Tokyo, ra.RacecourseCode)

        // SE should have horse number
        Assert.Equal(Some 3, se.HorseNumber)
    | _ -> failwith "All records should parse successfully"

[<Fact>]
let ``Parse mixed valid and invalid records`` () =
    let raceKey = "2024050512345678"

    // Valid O1 record
    let validData = Array.create 100 32uy
    Array.Copy(encodeShiftJis "O1", 0, validData, 0, 2)
    Array.Copy(encodeShiftJis raceKey, 0, validData, 2, 16)
    Array.Copy(encodeShiftJis "03", 0, validData, 18, 2)
    Array.Copy(encodeShiftJis "0025", 0, validData, 20, 4)

    // Invalid O1 record (too short)
    let invalidData = Array.create 10 32uy
    Array.Copy(encodeShiftJis "O1", 0, invalidData, 0, 2)

    let validResult = O1.parse validData
    let invalidResult = O1.parse invalidData

    Assert.True(Result.isOk validResult)
    Assert.True(Result.isError invalidResult)

[<Fact>]
let ``Parse complete race scenario with all related records`` () =
    let raceKey = "2024050512345678"

    // TK: Race registration
    let tkData = Array.create 346 32uy
    Array.Copy(encodeShiftJis "TK", 0, tkData, 0, 2)
    Array.Copy(encodeShiftJis raceKey, 0, tkData, 2, 16)
    Array.Copy(encodeShiftJis "2020105678", 0, tkData, 18, 10) // HorseId required
    Array.Copy(encodeShiftJis "TestHorse", 0, tkData, 28, 9) // HorseName required

    // RA: Race details
    let raData = Array.create 366 32uy
    Array.Copy(encodeShiftJis "RA", 0, raData, 0, 2)
    Array.Copy(encodeShiftJis raceKey, 0, raData, 2, 16)
    Array.Copy(encodeShiftJis "TestRace", 0, raData, 18, 8) // RaceName required
    Array.Copy(encodeShiftJis "01", 0, raData, 68, 2) // Racecourse: Sapporo
    Array.Copy(encodeShiftJis "2000", 0, raData, 72, 4) // Distance

    // SE: Runner information
    let seData = Array.create 1446 32uy
    Array.Copy(encodeShiftJis "SE", 0, seData, 0, 2)
    Array.Copy(encodeShiftJis raceKey, 0, seData, 2, 16)
    Array.Copy(encodeShiftJis "2020105678", 0, seData, 18, 10) // HorseId required
    Array.Copy(encodeShiftJis "TestHorse", 0, seData, 28, 9) // HorseName required
    Array.Copy(encodeShiftJis "03", 0, seData, 65, 2) // Horse number (offset 65)

    // O1: Win odds
    let o1Data = Array.create 100 32uy
    Array.Copy(encodeShiftJis "O1", 0, o1Data, 0, 2)
    Array.Copy(encodeShiftJis raceKey, 0, o1Data, 2, 16)
    Array.Copy(encodeShiftJis "03", 0, o1Data, 18, 2) // Horse number 3
    Array.Copy(encodeShiftJis "0025", 0, o1Data, 20, 4) // Odds 2.5

    // H1: Win payoff
    let h1Data = Array.create 100 32uy
    Array.Copy(encodeShiftJis "H1", 0, h1Data, 0, 2)
    Array.Copy(encodeShiftJis raceKey, 0, h1Data, 2, 16)
    Array.Copy(encodeShiftJis "1", 0, h1Data, 18, 1) // Bet type: Win
    Array.Copy(encodeShiftJis "03", 0, h1Data, 19, 2) // Horse number 3
    Array.Copy(encodeShiftJis "000250", 0, h1Data, 21, 6) // Payoff 250 yen

    // WF: Horse weight
    let wfData = Array.create 100 32uy
    Array.Copy(encodeShiftJis "WF", 0, wfData, 0, 2)
    Array.Copy(encodeShiftJis raceKey, 0, wfData, 2, 16)
    Array.Copy(encodeShiftJis "03", 0, wfData, 18, 2) // Horse number 3
    Array.Copy(encodeShiftJis "480", 0, wfData, 20, 3) // Weight 480kg

    // Parse all records
    let results =
        [ TK.parse tkData |> Result.map (fun r -> "TK", r.RaceKey)
          RA.parse raData |> Result.map (fun r -> "RA", r.RaceKey)
          SE.parse seData |> Result.map (fun r -> "SE", r.RaceKey)
          O1.parse o1Data |> Result.map (fun r -> "O1", r.RaceKey)
          H1.parse h1Data |> Result.map (fun r -> "H1", r.RaceKey)
          WF.parse wfData |> Result.map (fun r -> "WF", r.RaceKey) ]

    // All should parse successfully
    Assert.All(results, fun result -> Assert.True(Result.isOk result))

    // All should have the same race key
    let raceKeys = results |> List.map (Result.bind (fun (_, key) -> Ok key))

    Assert.All(
        raceKeys,
        fun keyResult ->
            match keyResult with
            | Ok key -> Assert.Equal(raceKey, key)
            | Error _ -> failwith "Should not be error"
    )

[<Fact>]
let ``Parse horse master data with race records`` () =
    let horseId = "2020105678"
    let raceKey = "2024050512345678"

    // UM: Horse master data
    let umData = Array.create 500 32uy
    Array.Copy(encodeShiftJis "UM", 0, umData, 0, 2)
    Array.Copy(encodeShiftJis horseId, 0, umData, 2, 10)
    Array.Copy(encodeShiftJis "サンプルホース", 0, umData, 12, encodeShiftJis("サンプルホース").Length)

    // SE: Runner information (references same horse)
    let seData = Array.create 1446 32uy
    Array.Copy(encodeShiftJis "SE", 0, seData, 0, 2)
    Array.Copy(encodeShiftJis raceKey, 0, seData, 2, 16)
    Array.Copy(encodeShiftJis horseId, 0, seData, 18, 10) // HorseId required
    Array.Copy(encodeShiftJis "TestHorse", 0, seData, 28, 9) // HorseName required

    match (UM.parse umData, SE.parse seData) with
    | (Ok um, Ok se) ->
        // Horse master should have correct horse ID
        Assert.Equal(horseId, um.HorseId)
        // SE record should have correct race key
        Assert.Equal(raceKey, se.RaceKey)
    | _ -> failwith "Both records should parse successfully"

[<Fact>]
let ``Handle record type identification`` () =
    let records =
        [ ("TK", Array.create 346 32uy)
          ("RA", Array.create 366 32uy)
          ("SE", Array.create 1446 32uy)
          ("O1", Array.create 100 32uy)
          ("H1", Array.create 100 32uy)
          ("WF", Array.create 100 32uy)
          ("UM", Array.create 500 32uy) ]

    // Set record type identifier for each record
    records
    |> List.iter (fun (recType, data) -> Array.Copy(encodeShiftJis recType, 0, data, 0, 2))

    // Verify record type can be identified
    records
    |> List.iter (fun (expectedType, data) ->
        let actualType = getRecordType data
        Assert.Equal(expectedType, actualType))

[<Fact>]
let ``Parse records with Japanese text correctly`` () =
    let horseId = "2020105678"
    let horseName = "ディープインパクト" // Deep Impact in Japanese

    let umData = Array.create 500 32uy
    Array.Copy(encodeShiftJis "UM", 0, umData, 0, 2)
    Array.Copy(encodeShiftJis horseId, 0, umData, 2, 10)
    Array.Copy(encodeShiftJis horseName, 0, umData, 12, encodeShiftJis(horseName).Length)

    match UM.parse umData with
    | Ok record ->
        Assert.Equal(horseId, record.HorseId)
        Assert.StartsWith("ディープ", record.HorseName)
    | Error err -> failwithf "UM parsing failed: %A" err

[<Fact>]
let ``Parse sequence of records maintains data integrity`` () =
    let raceKey = "2024050512345678"
    let horseNumbers = [ 1; 2; 3; 4; 5 ]

    // Create multiple O1 records for different horses
    let records =
        horseNumbers
        |> List.map (fun horseNum ->
            let data = Array.create 100 32uy
            Array.Copy(encodeShiftJis "O1", 0, data, 0, 2)
            Array.Copy(encodeShiftJis raceKey, 0, data, 2, 16)
            Array.Copy(encodeShiftJis (sprintf "%02d" horseNum), 0, data, 18, 2)
            Array.Copy(encodeShiftJis (sprintf "%04d" (horseNum * 10)), 0, data, 20, 4)
            data)

    // Parse all records
    let results = records |> List.map O1.parse

    // All should succeed
    Assert.All(results, fun result -> Assert.True(Result.isOk result))

    // Extract horse numbers and verify order
    let parsedHorseNumbers =
        results
        |> List.map (fun r ->
            match r with
            | Ok record -> record.HorseNumber
            | Error _ -> None)

    Assert.Equal(horseNumbers.Length, parsedHorseNumbers.Length)

    // Verify each horse number matches
    List.zip horseNumbers parsedHorseNumbers
    |> List.iter (fun (expected, actual) -> Assert.Equal(Some expected, actual))

// ============================================================================
// Integration Tests - Error Recovery and Robustness
// ============================================================================

[<Fact>]
let ``Continue parsing after encountering invalid record`` () =
    let raceKey = "2024050512345678"

    // Valid record 1
    let valid1 = Array.create 100 32uy
    Array.Copy(encodeShiftJis "O1", 0, valid1, 0, 2)
    Array.Copy(encodeShiftJis raceKey, 0, valid1, 2, 16)
    Array.Copy(encodeShiftJis "01", 0, valid1, 18, 2)
    Array.Copy(encodeShiftJis "0025", 0, valid1, 20, 4)

    // Invalid record (too short)
    let invalid = Array.create 10 32uy
    Array.Copy(encodeShiftJis "O1", 0, invalid, 0, 2)

    // Valid record 2
    let valid2 = Array.create 100 32uy
    Array.Copy(encodeShiftJis "O1", 0, valid2, 0, 2)
    Array.Copy(encodeShiftJis raceKey, 0, valid2, 2, 16)
    Array.Copy(encodeShiftJis "02", 0, valid2, 18, 2)
    Array.Copy(encodeShiftJis "0030", 0, valid2, 20, 4)

    let result1 = O1.parse valid1
    let resultInvalid = O1.parse invalid
    let result2 = O1.parse valid2

    // Valid records should succeed
    Assert.True(Result.isOk result1)
    Assert.True(Result.isOk result2)

    // Invalid record should fail
    Assert.True(Result.isError resultInvalid)

    // Verify valid records have correct data
    match (result1, result2) with
    | (Ok r1, Ok r2) ->
        Assert.Equal(Some 1, r1.HorseNumber)
        Assert.Equal(Some 2, r2.HorseNumber)
    | _ -> failwith "Valid records should have parsed successfully"

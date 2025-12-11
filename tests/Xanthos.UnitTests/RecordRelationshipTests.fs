module Xanthos.UnitTests.RecordRelationshipTests

open System
open Xunit
open Xanthos.Core
open Xanthos.Core.Text
open Xanthos.Core.Records

// ============================================================================
// RaceKey Consistency Tests
// ============================================================================

[<Fact>]
let ``TK and RA should share the same RaceKey format`` () =
    let raceKey = "2024050512345678"

    let tkData = Array.create 346 32uy
    Array.Copy(encodeShiftJis "TK", 0, tkData, 0, 2)
    Array.Copy(encodeShiftJis raceKey, 0, tkData, 2, 16)
    Array.Copy(encodeShiftJis "2020105678", 0, tkData, 18, 10) // HorseId
    Array.Copy(encodeShiftJis "TestHorse", 0, tkData, 28, 9) // HorseName required

    let raData = Array.create 366 32uy
    Array.Copy(encodeShiftJis "RA", 0, raData, 0, 2)
    Array.Copy(encodeShiftJis raceKey, 0, raData, 2, 16)
    Array.Copy(encodeShiftJis "TestRace", 0, raData, 18, 8) // RaceName required

    match TK.parse tkData, RA.parse raData with
    | Ok tkRecord, Ok raRecord ->
        Assert.Equal(tkRecord.RaceKey, raRecord.RaceKey)
        Assert.Equal(raceKey, tkRecord.RaceKey)
        Assert.Equal(raceKey, raRecord.RaceKey)
    | _ -> failwith "Both parsers should succeed"

[<Fact>]
let ``TK and SE should share the same RaceKey format`` () =
    let raceKey = "2024050512345678"

    let tkData = Array.create 346 32uy
    Array.Copy(encodeShiftJis "TK", 0, tkData, 0, 2)
    Array.Copy(encodeShiftJis raceKey, 0, tkData, 2, 16)
    Array.Copy(encodeShiftJis "2020105678", 0, tkData, 18, 10) // HorseId
    Array.Copy(encodeShiftJis "TestHorse", 0, tkData, 28, 9) // HorseName required

    let seData = Array.create 1446 32uy
    Array.Copy(encodeShiftJis "SE", 0, seData, 0, 2)
    Array.Copy(encodeShiftJis raceKey, 0, seData, 2, 16)
    Array.Copy(encodeShiftJis "2020105678", 0, seData, 18, 10) // HorseId
    Array.Copy(encodeShiftJis "TestHorse", 0, seData, 28, 9) // HorseName required

    match TK.parse tkData, SE.parse seData with
    | Ok tkRecord, Ok seRecord ->
        Assert.Equal(tkRecord.RaceKey, seRecord.RaceKey)
        Assert.Equal(raceKey, tkRecord.RaceKey)
        Assert.Equal(raceKey, seRecord.RaceKey)
    | _ -> failwith "Both parsers should succeed"

[<Fact>]
let ``O1 and H1 should share the same RaceKey format`` () =
    let raceKey = "2024050512345678"

    let o1Data = Array.create 100 32uy
    Array.Copy(encodeShiftJis "O1", 0, o1Data, 0, 2)
    Array.Copy(encodeShiftJis raceKey, 0, o1Data, 2, 16)

    let h1Data = Array.create 100 32uy
    Array.Copy(encodeShiftJis "H1", 0, h1Data, 0, 2)
    Array.Copy(encodeShiftJis raceKey, 0, h1Data, 2, 16)

    match O1.parse o1Data, H1.parse h1Data with
    | Ok o1Record, Ok h1Record ->
        Assert.Equal(o1Record.RaceKey, h1Record.RaceKey)
        Assert.Equal(raceKey, o1Record.RaceKey)
        Assert.Equal(raceKey, h1Record.RaceKey)
    | _ -> failwith "Both parsers should succeed"

// ============================================================================
// HorseId Consistency Tests
// ============================================================================

[<Fact>]
let ``TK and SE should share the same HorseId format`` () =
    let horseId = "2020105678"

    let tkData = Array.create 346 32uy
    Array.Copy(encodeShiftJis "TK", 0, tkData, 0, 2)
    Array.Copy(encodeShiftJis "2024050512345678", 0, tkData, 2, 16)
    Array.Copy(encodeShiftJis horseId, 0, tkData, 18, 10) // HorseId
    Array.Copy(encodeShiftJis "TestHorse", 0, tkData, 28, 9) // HorseName required

    let seData = Array.create 1446 32uy
    Array.Copy(encodeShiftJis "SE", 0, seData, 0, 2)
    Array.Copy(encodeShiftJis "2024050512345678", 0, seData, 2, 16)
    Array.Copy(encodeShiftJis horseId, 0, seData, 18, 10) // HorseId
    Array.Copy(encodeShiftJis "TestHorse", 0, seData, 28, 9) // HorseName required

    match TK.parse tkData, SE.parse seData with
    | Ok tkRecord, Ok seRecord ->
        Assert.Equal(tkRecord.HorseId, seRecord.HorseId)
        Assert.Equal(horseId, tkRecord.HorseId)
        Assert.Equal(horseId, seRecord.HorseId)
    | _ -> failwith "Both parsers should succeed"

[<Fact>]
let ``TK and UM should share the same HorseId format`` () =
    let horseId = "2020105678"

    let tkData = Array.create 346 32uy
    Array.Copy(encodeShiftJis "TK", 0, tkData, 0, 2)
    Array.Copy(encodeShiftJis "2024050512345678", 0, tkData, 2, 16)
    Array.Copy(encodeShiftJis horseId, 0, tkData, 18, 10) // HorseId
    Array.Copy(encodeShiftJis "TestHorse", 0, tkData, 28, 9) // HorseName required

    let umData = Array.create 500 32uy
    Array.Copy(encodeShiftJis "UM", 0, umData, 0, 2)
    Array.Copy(encodeShiftJis horseId, 0, umData, 2, 10) // HorseId
    Array.Copy(encodeShiftJis "TestHorse", 0, umData, 12, 9) // HorseName required

    match TK.parse tkData, UM.parse umData with
    | Ok tkRecord, Ok umRecord ->
        Assert.Equal(tkRecord.HorseId, umRecord.HorseId)
        Assert.Equal(horseId, tkRecord.HorseId)
        Assert.Equal(horseId, umRecord.HorseId)
    | _ -> failwith "Both parsers should succeed"

// ============================================================================
// Cross-Record Data Integrity Tests
// ============================================================================

[<Fact>]
let ``Multiple SE records can share the same RaceKey`` () =
    let raceKey = "2024050512345678"

    let se1Data = Array.create 1446 32uy
    Array.Copy(encodeShiftJis "SE", 0, se1Data, 0, 2)
    Array.Copy(encodeShiftJis raceKey, 0, se1Data, 2, 16)
    Array.Copy(encodeShiftJis "2020105678", 0, se1Data, 18, 10) // HorseId
    Array.Copy(encodeShiftJis "TestHorse1", 0, se1Data, 28, 10) // HorseName required

    let se2Data = Array.create 1446 32uy
    Array.Copy(encodeShiftJis "SE", 0, se2Data, 0, 2)
    Array.Copy(encodeShiftJis raceKey, 0, se2Data, 2, 16)
    Array.Copy(encodeShiftJis "2020105679", 0, se2Data, 18, 10) // HorseId
    Array.Copy(encodeShiftJis "TestHorse2", 0, se2Data, 28, 10) // HorseName required

    match SE.parse se1Data, SE.parse se2Data with
    | Ok se1Record, Ok se2Record ->
        // Same race, different horses
        Assert.Equal(se1Record.RaceKey, se2Record.RaceKey)
        Assert.NotEqual<string>(se1Record.HorseId, se2Record.HorseId)
    | _ -> failwith "Both parsers should succeed"

[<Fact>]
let ``WF weight data should relate to SE horse data`` () =
    let raceKey = "2024050512345678"

    let seData = Array.create 1446 32uy
    Array.Copy(encodeShiftJis "SE", 0, seData, 0, 2)
    Array.Copy(encodeShiftJis raceKey, 0, seData, 2, 16)
    Array.Copy(encodeShiftJis "2020105678", 0, seData, 18, 10) // HorseId
    Array.Copy(encodeShiftJis "TestHorse", 0, seData, 28, 9) // HorseName required

    let wfData = Array.create 100 32uy
    Array.Copy(encodeShiftJis "WF", 0, wfData, 0, 2)
    Array.Copy(encodeShiftJis raceKey, 0, wfData, 2, 16)

    match SE.parse seData, WF.parse wfData with
    | Ok seRecord, Ok wfRecord ->
        // Both records should refer to the same race
        Assert.Equal(seRecord.RaceKey, wfRecord.RaceKey)
    | _ -> failwith "Both parsers should succeed"

// ============================================================================
// RaceKey Format Validation
// ============================================================================

[<Fact>]
let ``RaceKey should be 16 characters across all record types`` () =
    let raceKey = "2024050512345678"
    Assert.Equal(16, raceKey.Length)

    // Test that all record types handle 16-character RaceKeys
    let testData recordType =
        let data = Array.create 1500 32uy
        Array.Copy(encodeShiftJis recordType, 0, data, 0, 2)
        Array.Copy(encodeShiftJis raceKey, 0, data, 2, 16)
        data

    let _ = TK.parse (testData "TK")
    let _ = RA.parse (testData "RA")
    let _ = SE.parse (testData "SE")
    let _ = O1.parse (testData "O1")
    let _ = H1.parse (testData "H1")
    let _ = WF.parse (testData "WF")

    () // All should parse without exception

[<Fact>]
let ``HorseId should be 10 characters across all record types`` () =
    let horseId = "2020105678"
    Assert.Equal(10, horseId.Length)

    // Verify format consistency
    let tkData = Array.create 346 32uy
    Array.Copy(encodeShiftJis "TK", 0, tkData, 0, 2)
    Array.Copy(encodeShiftJis "2024050512345678", 0, tkData, 2, 16)
    Array.Copy(encodeShiftJis horseId, 0, tkData, 18, 10) // HorseId
    Array.Copy(encodeShiftJis "TestHorse", 0, tkData, 28, 9) // HorseName required

    let seData = Array.create 1446 32uy
    Array.Copy(encodeShiftJis "SE", 0, seData, 0, 2)
    Array.Copy(encodeShiftJis "2024050512345678", 0, seData, 2, 16)
    Array.Copy(encodeShiftJis horseId, 0, seData, 18, 10) // HorseId
    Array.Copy(encodeShiftJis "TestHorse", 0, seData, 28, 9) // HorseName required

    let umData = Array.create 500 32uy
    Array.Copy(encodeShiftJis "UM", 0, umData, 0, 2)
    Array.Copy(encodeShiftJis horseId, 0, umData, 2, 10) // HorseId
    Array.Copy(encodeShiftJis "TestHorse", 0, umData, 12, 9) // HorseName required

    match TK.parse tkData, SE.parse seData, UM.parse umData with
    | Ok tkRecord, Ok seRecord, Ok umRecord ->
        Assert.Equal(10, tkRecord.HorseId.Trim().Length)
        Assert.Equal(10, seRecord.HorseId.Trim().Length)
        Assert.Equal(10, umRecord.HorseId.Trim().Length)
    | _ -> failwith "All parsers should succeed"

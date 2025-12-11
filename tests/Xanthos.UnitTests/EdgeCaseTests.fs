module Xanthos.UnitTests.EdgeCaseTests

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
// Core Function Error Handling Tests
// ============================================================================

[<Fact>]
let ``extractBytes returns error when offset exceeds data length`` () =
    let data = [| 1uy; 2uy; 3uy; 4uy; 5uy |]

    match extractBytes data 10 2 with
    | Error(RecordTooShort _) -> Assert.True(true)
    | Ok _ -> failwith "Should have returned error"
    | Error _ -> failwith "Wrong error type"

[<Fact>]
let ``extractBytes returns error when offset plus length exceeds data`` () =
    let data = [| 1uy; 2uy; 3uy; 4uy; 5uy |]

    match extractBytes data 3 5 with
    | Error(RecordTooShort(expected, actual)) ->
        Assert.Equal(8, expected)
        Assert.Equal(5, actual)
    | Ok _ -> failwith "Should have returned error"
    | Error _ -> failwith "Wrong error type"

[<Fact>]
let ``parseInt returns None for empty string`` () =
    let bytes = encodeShiftJis ""
    Assert.Equal(None, parseInt bytes)

[<Fact>]
let ``parseInt returns None for whitespace only`` () =
    let bytes = encodeShiftJis "   "
    Assert.Equal(None, parseInt bytes)

[<Fact>]
let ``parseInt returns None for non-numeric text`` () =
    let bytes = encodeShiftJis "ABC"
    Assert.Equal(None, parseInt bytes)

[<Fact>]
let ``parseInt returns None for mixed alphanumeric`` () =
    let bytes = encodeShiftJis "12A34"
    Assert.Equal(None, parseInt bytes)

[<Fact>]
let ``parseDecimal returns None for empty string`` () =
    let bytes = encodeShiftJis ""
    Assert.Equal(None, parseDecimal bytes 1)

[<Fact>]
let ``parseDecimal returns None for non-numeric text`` () =
    let bytes = encodeShiftJis "XYZ"
    Assert.Equal(None, parseDecimal bytes 2)

[<Fact>]
let ``parseDecimal handles zero precision correctly`` () =
    let bytes = encodeShiftJis "123"

    match parseDecimal bytes 0 with
    | Some value -> Assert.Equal(123M, value)
    | None -> failwith "Expected Some decimal"

[<Fact>]
let ``parseDate returns None for invalid date format`` () =
    let bytes = encodeShiftJis "20241399" // Invalid: month 13, day 99
    Assert.Equal(None, parseDate bytes "yyyyMMdd")

[<Fact>]
let ``parseDate returns None for incomplete date`` () =
    let bytes = encodeShiftJis "202412" // Only 6 chars, needs 8
    Assert.Equal(None, parseDate bytes "yyyyMMdd")

[<Fact>]
let ``parseDate returns None for empty string`` () =
    let bytes = encodeShiftJis ""
    Assert.Equal(None, parseDate bytes "yyyyMMdd")

[<Fact>]
let ``getRecordType handles short data gracefully`` () =
    let data = encodeShiftJis "T" // Only 1 byte
    let recordType = getRecordType data
    // Should not crash, but may return incomplete type
    Assert.True(recordType.Length <= 2)

// ============================================================================
// Code Table Error Handling Tests
// ============================================================================

[<Fact>]
let ``parseCode returns None for invalid SexCode`` () =
    Assert.Equal(None, parseCode<SexCode> "9")

[<Fact>]
let ``parseCode returns None for empty string`` () =
    Assert.Equal(None, parseCode<SexCode> "")

[<Fact>]
let ``parseCode returns None for non-numeric string`` () =
    Assert.Equal(None, parseCode<RacecourseCode> "XX")

[<Fact>]
let ``parseCode returns None for out of range value`` () =
    Assert.Equal(None, parseCode<RacecourseCode> "99")

// ============================================================================
// Record Parser Error Handling Tests - Record Too Short
// ============================================================================

[<Fact>]
let ``TK parser returns error when record is too short`` () =
    let data = Array.create 100 32uy // Need 346 bytes, only 100
    Array.Copy(encodeShiftJis "TK", 0, data, 0, 2)

    match TK.parse data with
    | Error(ValidationError msg) -> Assert.Contains("too short", msg)
    | Ok _ -> failwith "Should have returned error for short record"
    | Error _ -> failwith "Wrong error type"

[<Fact>]
let ``RA parser returns error when record is too short`` () =
    let data = Array.create 50 32uy // Need 366 bytes
    Array.Copy(encodeShiftJis "RA", 0, data, 0, 2)

    match RA.parse data with
    | Error(ValidationError msg) -> Assert.Contains("too short", msg)
    | Ok _ -> failwith "Should have returned error"
    | Error _ -> failwith "Wrong error type"

[<Fact>]
let ``SE parser returns error when record is too short`` () =
    let data = Array.create 100 32uy // Need 1446 bytes
    Array.Copy(encodeShiftJis "SE", 0, data, 0, 2)

    match SE.parse data with
    | Error(ValidationError msg) -> Assert.Contains("too short", msg)
    | Ok _ -> failwith "Should have returned error"
    | Error _ -> failwith "Wrong error type"

[<Fact>]
let ``HR parser returns error when record is too short`` () =
    let data = Array.create 20 32uy // Need 90 bytes
    Array.Copy(encodeShiftJis "HR", 0, data, 0, 2)

    match HR.parse data with
    | Error(ValidationError msg) -> Assert.Contains("too short", msg)
    | Ok _ -> failwith "Should have returned error"
    | Error _ -> failwith "Wrong error type"

[<Fact>]
let ``O1 parser returns error when record is too short`` () =
    let data = Array.create 20 32uy // Need at least 38 bytes
    Array.Copy(encodeShiftJis "O1", 0, data, 0, 2)

    match O1.parse data with
    | Error(ValidationError msg) -> Assert.Contains("too short", msg)
    | Ok _ -> failwith "Should have returned error"
    | Error _ -> failwith "Wrong error type"

[<Fact>]
let ``H1 parser returns error when record is too short`` () =
    let data = Array.create 15 32uy // Need at least 29 bytes
    Array.Copy(encodeShiftJis "H1", 0, data, 0, 2)

    match H1.parse data with
    | Error(ValidationError msg) -> Assert.Contains("too short", msg)
    | Ok _ -> failwith "Should have returned error"
    | Error _ -> failwith "Wrong error type"

[<Fact>]
let ``WF parser returns error when record is too short`` () =
    let data = Array.create 15 32uy // Need at least 26 bytes
    Array.Copy(encodeShiftJis "WF", 0, data, 0, 2)

    match WF.parse data with
    | Error(ValidationError msg) -> Assert.Contains("too short", msg)
    | Ok _ -> failwith "Should have returned error"
    | Error _ -> failwith "Wrong error type"

[<Fact>]
let ``UM parser returns error when record is too short`` () =
    let data = Array.create 100 32uy // Need 500 bytes
    Array.Copy(encodeShiftJis "UM", 0, data, 0, 2)

    match UM.parse data with
    | Error(ValidationError msg) -> Assert.Contains("too short", msg)
    | Ok _ -> failwith "Should have returned error"
    | Error _ -> failwith "Wrong error type"

// ============================================================================
// Record Parser Error Handling Tests - Invalid Code Values
// ============================================================================

[<Fact>]
let ``RA parser handles invalid racecourse code gracefully`` () =
    let data = Array.create 366 32uy
    let raceKey = "2024050512345678"
    let raceName = "TestRace"
    Array.Copy(encodeShiftJis "RA", 0, data, 0, 2)
    Array.Copy(encodeShiftJis raceKey, 0, data, 2, 16)
    Array.Copy(encodeShiftJis raceName, 0, data, 18, 8) // Required RaceName
    Array.Copy(encodeShiftJis "99", 0, data, 68, 2) // Invalid code

    match RA.parse data with
    | Ok record ->
        Assert.Equal(raceKey, record.RaceKey)
        Assert.Equal(None, record.RacecourseCode) // Should be None for invalid code
    | Error _ -> failwith "Should parse successfully with None for invalid code"

[<Fact>]
let ``SE parser handles invalid sex code gracefully`` () =
    let data = Array.create 1446 32uy
    let raceKey = "2024050512345678"
    let horseId = "2020105678"
    let horseName = "TestHorse"
    Array.Copy(encodeShiftJis "SE", 0, data, 0, 2)
    Array.Copy(encodeShiftJis raceKey, 0, data, 2, 16)
    Array.Copy(encodeShiftJis horseId, 0, data, 18, 10) // HorseId at offset 18
    Array.Copy(encodeShiftJis horseName, 0, data, 28, 9) // HorseName at offset 28
    Array.Copy(encodeShiftJis "9", 0, data, 104, 1) // Invalid sex code

    match SE.parse data with
    | Ok record ->
        Assert.Equal(raceKey, record.RaceKey)
        Assert.Equal(None, record.Sex) // Should be None for invalid code
    | Error err -> failwithf "Should parse successfully with None for invalid code: %A" err

[<Fact>]
let ``UM parser handles invalid hair color code gracefully`` () =
    let data = Array.create 500 32uy
    let horseId = "2020105678"
    let horseName = "TestHorse"
    Array.Copy(encodeShiftJis "UM", 0, data, 0, 2)
    Array.Copy(encodeShiftJis horseId, 0, data, 2, 10)
    Array.Copy(encodeShiftJis horseName, 0, data, 12, 9) // HorseName required
    Array.Copy(encodeShiftJis "Z", 0, data, 49, 1) // Invalid hair color

    match UM.parse data with
    | Ok record ->
        Assert.Equal(horseId, record.HorseId)
        Assert.Equal(None, record.HairColor) // Should be None for invalid code
    | Error err -> failwithf "Should parse successfully with None for invalid code: %A" err

// ============================================================================
// Record Parser Error Handling Tests - Boundary Values
// ============================================================================

[<Fact>]
let ``O1 parser handles maximum odds value`` () =
    let data = Array.create 100 32uy
    let raceKey = "2024050512345678"
    Array.Copy(encodeShiftJis "O1", 0, data, 0, 2)
    Array.Copy(encodeShiftJis raceKey, 0, data, 2, 16)
    Array.Copy(encodeShiftJis "03", 0, data, 18, 2)
    Array.Copy(encodeShiftJis "9999", 0, data, 20, 4) // Maximum odds 999.9

    match O1.parse data with
    | Ok record -> Assert.Equal(Some 999.9M, record.Odds)
    | Error _ -> failwith "Should handle maximum odds"

[<Fact>]
let ``O1 parser handles minimum odds value`` () =
    let data = Array.create 100 32uy
    let raceKey = "2024050512345678"
    Array.Copy(encodeShiftJis "O1", 0, data, 0, 2)
    Array.Copy(encodeShiftJis raceKey, 0, data, 2, 16)
    Array.Copy(encodeShiftJis "03", 0, data, 18, 2)
    Array.Copy(encodeShiftJis "0010", 0, data, 20, 4) // Minimum odds 1.0

    match O1.parse data with
    | Ok record -> Assert.Equal(Some 1.0M, record.Odds)
    | Error _ -> failwith "Should handle minimum odds"

[<Fact>]
let ``H1 parser handles maximum payoff value`` () =
    let data = Array.create 100 32uy
    let raceKey = "2024050512345678"
    Array.Copy(encodeShiftJis "H1", 0, data, 0, 2)
    Array.Copy(encodeShiftJis raceKey, 0, data, 2, 16)
    Array.Copy(encodeShiftJis "1", 0, data, 18, 1)
    Array.Copy(encodeShiftJis "07", 0, data, 19, 2)
    Array.Copy(encodeShiftJis "999999", 0, data, 21, 6) // Maximum payoff

    match H1.parse data with
    | Ok record -> Assert.Equal(Some 999999, record.Payoff)
    | Error _ -> failwith "Should handle maximum payoff"

[<Fact>]
let ``WF parser handles negative weight difference`` () =
    let data = Array.create 100 32uy
    let raceKey = "2024050512345678"
    Array.Copy(encodeShiftJis "WF", 0, data, 0, 2)
    Array.Copy(encodeShiftJis raceKey, 0, data, 2, 16)
    Array.Copy(encodeShiftJis "05", 0, data, 18, 2)
    Array.Copy(encodeShiftJis "478", 0, data, 20, 3)
    Array.Copy(encodeShiftJis " -8", 0, data, 23, 3) // Negative weight diff

    match WF.parse data with
    | Ok record -> Assert.Equal(Some -8, record.WeightDiff)
    | Error _ -> failwith "Should handle negative weight difference"

// ============================================================================
// Record Parser Error Handling Tests - Empty/Null Fields
// ============================================================================

[<Fact>]
let ``TK parser handles empty optional fields`` () =
    let data = Array.create 346 32uy // All spaces
    Array.Copy(encodeShiftJis "TK", 0, data, 0, 2)
    Array.Copy(encodeShiftJis "2024050512345678", 0, data, 2, 16)
    Array.Copy(encodeShiftJis "2020105678", 0, data, 18, 10) // HorseId required
    Array.Copy(encodeShiftJis "TestHorse", 0, data, 28, 9) // HorseName required

    match TK.parse data with
    | Ok record ->
        Assert.Equal("2024050512345678", record.RaceKey)
        Assert.Equal(None, record.Sex)
        Assert.Equal(None, record.HairColor)
        Assert.Equal(None, record.BirthYear)
    | Error err -> failwithf "Should parse with empty optional fields: %A" err

[<Fact>]
let ``RA parser handles empty optional fields`` () =
    let data = Array.create 366 32uy
    Array.Copy(encodeShiftJis "RA", 0, data, 0, 2)
    Array.Copy(encodeShiftJis "2024050512345678", 0, data, 2, 16)
    Array.Copy(encodeShiftJis "TestRace", 0, data, 18, 8) // RaceName required

    match RA.parse data with
    | Ok record ->
        Assert.Equal("2024050512345678", record.RaceKey)
        Assert.Equal(None, record.Distance)
        Assert.Equal(None, record.Grade)
    | Error err -> failwithf "Should parse with empty optional fields: %A" err

[<Fact>]
let ``SE parser handles all optional fields as None`` () =
    let data = Array.create 1446 32uy
    Array.Copy(encodeShiftJis "SE", 0, data, 0, 2)
    Array.Copy(encodeShiftJis "2024050512345678", 0, data, 2, 16)
    Array.Copy(encodeShiftJis "2020105678", 0, data, 18, 10) // HorseId at offset 18
    Array.Copy(encodeShiftJis "TestHorse", 0, data, 28, 9) // HorseName at offset 28

    match SE.parse data with
    | Ok record ->
        Assert.Equal("2024050512345678", record.RaceKey)
        Assert.Equal(None, record.GateNumber)
        Assert.Equal(None, record.HorseNumber)
        Assert.Equal(None, record.Age)
    | Error err -> failwithf "Should parse with all None optional fields: %A" err

// ============================================================================
// Additional Record Parser Error Handling Tests - Odds Data (O2-O6)
// ============================================================================

[<Fact>]
let ``O2 parser returns error when record is too short`` () =
    let data = Array.create 20 32uy // Need at least 30 bytes
    Array.Copy(encodeShiftJis "O2", 0, data, 0, 2)

    match Xanthos.Core.Records.O2.parse data with
    | Error(ValidationError msg) -> Assert.Contains("too short", msg)
    | Ok _ -> failwith "Should have returned error"
    | Error _ -> failwith "Wrong error type"

[<Fact>]
let ``O3 parser returns error when record is too short`` () =
    let data = Array.create 15 32uy // Need at least 26 bytes
    Array.Copy(encodeShiftJis "O3", 0, data, 0, 2)

    match Xanthos.Core.Records.O3.parse data with
    | Error(ValidationError msg) -> Assert.Contains("too short", msg)
    | Ok _ -> failwith "Should have returned error"
    | Error _ -> failwith "Wrong error type"

[<Fact>]
let ``O4 parser returns error when record is too short`` () =
    let data = Array.create 15 32uy // Need at least 28 bytes
    Array.Copy(encodeShiftJis "O4", 0, data, 0, 2)

    match Xanthos.Core.Records.O4.parse data with
    | Error(ValidationError msg) -> Assert.Contains("too short", msg)
    | Ok _ -> failwith "Should have returned error"
    | Error _ -> failwith "Wrong error type"

[<Fact>]
let ``O5 parser returns error when record is too short`` () =
    let data = Array.create 15 32uy // Need at least 28 bytes
    Array.Copy(encodeShiftJis "O5", 0, data, 0, 2)

    match Xanthos.Core.Records.O5.parse data with
    | Error(ValidationError msg) -> Assert.Contains("too short", msg)
    | Ok _ -> failwith "Should have returned error"
    | Error _ -> failwith "Wrong error type"

[<Fact>]
let ``O6 parser returns error when record is too short`` () =
    let data = Array.create 15 32uy // Need at least 28 bytes
    Array.Copy(encodeShiftJis "O6", 0, data, 0, 2)

    match Xanthos.Core.Records.O6.parse data with
    | Error(ValidationError msg) -> Assert.Contains("too short", msg)
    | Ok _ -> failwith "Should have returned error"
    | Error _ -> failwith "Wrong error type"

// ============================================================================
// Additional Record Parser Error Handling Tests - Payoff Data (H5, H6)
// ============================================================================

[<Fact>]
let ``H5 parser returns error when record is too short`` () =
    let data = Array.create 15 32uy // Need at least 41 bytes
    Array.Copy(encodeShiftJis "H5", 0, data, 0, 2)

    match Xanthos.Core.Records.H5.parse data with
    | Error(ValidationError msg) -> Assert.Contains("too short", msg)
    | Ok _ -> failwith "Should have returned error"
    | Error _ -> failwith "Wrong error type"

[<Fact>]
let ``H6 parser returns error when record is too short`` () =
    let data = Array.create 15 32uy // Need at least 33 bytes
    Array.Copy(encodeShiftJis "H6", 0, data, 0, 2)

    match Xanthos.Core.Records.H6.parse data with
    | Error(ValidationError msg) -> Assert.Contains("too short", msg)
    | Ok _ -> failwith "Should have returned error"
    | Error _ -> failwith "Wrong error type"

// ============================================================================
// Additional Record Parser Error Handling Tests - Real-time Updates
// ============================================================================

[<Fact>]
let ``JC parser returns error when record is too short`` () =
    let data = Array.create 10 32uy // Need at least 24 bytes
    Array.Copy(encodeShiftJis "JC", 0, data, 0, 2)

    match Xanthos.Core.Records.JC.parse data with
    | Error(ValidationError msg) -> Assert.Contains("too short", msg)
    | Ok _ -> failwith "Should have returned error"
    | Error _ -> failwith "Wrong error type"

[<Fact>]
let ``TC parser returns error when record is too short`` () =
    let data = Array.create 10 32uy // Need at least 28 bytes
    Array.Copy(encodeShiftJis "TC", 0, data, 0, 2)

    match Xanthos.Core.Records.TC.parse data with
    | Error(ValidationError msg) -> Assert.Contains("too short", msg)
    | Ok _ -> failwith "Should have returned error"
    | Error _ -> failwith "Wrong error type"

[<Fact>]
let ``CC parser returns error when record is too short`` () =
    let data = Array.create 10 32uy // Need at least 21 bytes
    Array.Copy(encodeShiftJis "CC", 0, data, 0, 2)

    match Xanthos.Core.Records.CC.parse data with
    | Error(ValidationError msg) -> Assert.Contains("too short", msg)
    | Ok _ -> failwith "Should have returned error"
    | Error _ -> failwith "Wrong error type"

[<Fact>]
let ``WE parser returns error when record is too short`` () =
    let data = Array.create 10 32uy // Need at least 20 bytes
    Array.Copy(encodeShiftJis "WE", 0, data, 0, 2)

    match Xanthos.Core.Records.WE.parse data with
    | Error(ValidationError msg) -> Assert.Contains("too short", msg)
    | Ok _ -> failwith "Should have returned error"
    | Error _ -> failwith "Wrong error type"

[<Fact>]
let ``AV parser returns error when record is too short`` () =
    let data = Array.create 10 32uy // Need at least 20 bytes
    Array.Copy(encodeShiftJis "AV", 0, data, 0, 2)

    match Xanthos.Core.Records.AV.parse data with
    | Error(ValidationError msg) -> Assert.Contains("too short", msg)
    | Ok _ -> failwith "Should have returned error"
    | Error _ -> failwith "Wrong error type"

// ============================================================================
// Additional Record Parser Error Handling Tests - Master Data
// ============================================================================

[<Fact>]
let ``KS parser returns error when record is too short`` () =
    let data = Array.create 50 32uy // Need at least 112 bytes
    Array.Copy(encodeShiftJis "KS", 0, data, 0, 2)

    match Xanthos.Core.Records.KS.parse data with
    | Error(ValidationError msg) -> Assert.Contains("too short", msg)
    | Ok _ -> failwith "Should have returned error"
    | Error _ -> failwith "Wrong error type"

[<Fact>]
let ``CH parser returns error when record is too short`` () =
    let data = Array.create 50 32uy // Need at least 80 bytes
    Array.Copy(encodeShiftJis "CH", 0, data, 0, 2)

    match Xanthos.Core.Records.CH.parse data with
    | Error(ValidationError msg) -> Assert.Contains("too short", msg)
    | Ok _ -> failwith "Should have returned error"
    | Error _ -> failwith "Wrong error type"

[<Fact>]
let ``BR parser returns error when record is too short`` () =
    let data = Array.create 30 32uy // Need at least 61 bytes
    Array.Copy(encodeShiftJis "BR", 0, data, 0, 2)

    match Xanthos.Core.Records.BR.parse data with
    | Error(ValidationError msg) -> Assert.Contains("too short", msg)
    | Ok _ -> failwith "Should have returned error"
    | Error _ -> failwith "Wrong error type"

[<Fact>]
let ``BN parser returns error when record is too short`` () =
    let data = Array.create 30 32uy // Need at least 62 bytes
    Array.Copy(encodeShiftJis "BN", 0, data, 0, 2)

    match Xanthos.Core.Records.BN.parse data with
    | Error(ValidationError msg) -> Assert.Contains("too short", msg)
    | Ok _ -> failwith "Should have returned error"
    | Error _ -> failwith "Wrong error type"

[<Fact>]
let ``RC parser returns error when record is too short`` () =
    let data = Array.create 50 32uy // Need at least 77 bytes
    Array.Copy(encodeShiftJis "RC", 0, data, 0, 2)

    match Xanthos.Core.Records.RC.parse data with
    | Error(ValidationError msg) -> Assert.Contains("too short", msg)
    | Ok _ -> failwith "Should have returned error"
    | Error _ -> failwith "Wrong error type"

// ============================================================================
// Additional Boundary Value Tests - Odds Parsers
// ============================================================================

[<Fact>]
let ``O2 parser handles maximum place odds value`` () =
    let data = Array.create 100 32uy
    let raceKey = "2024050512345678"
    Array.Copy(encodeShiftJis "O2", 0, data, 0, 2)
    Array.Copy(encodeShiftJis raceKey, 0, data, 2, 16)
    Array.Copy(encodeShiftJis "05", 0, data, 18, 2)
    Array.Copy(encodeShiftJis "9999", 0, data, 20, 4) // Maximum min odds 999.9
    Array.Copy(encodeShiftJis "9999", 0, data, 24, 4) // Maximum max odds 999.9

    match Xanthos.Core.Records.O2.parse data with
    | Ok record ->
        Assert.Equal(Some 999.9M, record.OddsMin)
        Assert.Equal(Some 999.9M, record.OddsMax)
    | Error _ -> failwith "Should handle maximum odds"

[<Fact>]
let ``O4 parser handles zero odds value`` () =
    let data = Array.create 100 32uy
    let raceKey = "2024050512345678"
    Array.Copy(encodeShiftJis "O4", 0, data, 0, 2)
    Array.Copy(encodeShiftJis raceKey, 0, data, 2, 16)
    Array.Copy(encodeShiftJis "03", 0, data, 18, 2)
    Array.Copy(encodeShiftJis "07", 0, data, 20, 2)
    Array.Copy(encodeShiftJis "0000", 0, data, 22, 4) // Zero odds

    match Xanthos.Core.Records.O4.parse data with
    | Ok record -> Assert.Equal(Some 0.0M, record.Odds)
    | Error _ -> failwith "Should handle zero odds"

// ============================================================================
// Additional Empty Field Tests - Master Data Parsers
// ============================================================================

[<Fact>]
let ``KS parser handles empty optional fields`` () =
    let data = Array.create 112 32uy
    let jockeyCode = "01234"
    let jockeyName = "TestJockey"
    Array.Copy(encodeShiftJis "KS", 0, data, 0, 2)
    Array.Copy(encodeShiftJis jockeyCode, 0, data, 2, 5)
    Array.Copy(encodeShiftJis jockeyName, 0, data, 7, 10) // JockeyName required

    match Xanthos.Core.Records.KS.parse data with
    | Ok record ->
        Assert.Equal(jockeyCode, record.JockeyCode)
        Assert.Equal(jockeyName, record.JockeyName)
        // Optional fields should be None
        Assert.Equal(None, record.JockeyNameKana)
        Assert.Equal(None, record.BelongsTo)
        Assert.Equal(None, record.InitialYear)
        Assert.Equal(None, record.BirthDate)
    | Error err -> failwithf "Should parse with empty optional fields: %A" err

[<Fact>]
let ``CH parser handles empty optional fields`` () =
    let data = Array.create 150 32uy // Use larger buffer to meet minimum requirements
    let trainerCode = "01234"
    let trainerName = "TestTrainer"
    Array.Copy(encodeShiftJis "CH", 0, data, 0, 2)
    Array.Copy(encodeShiftJis trainerCode, 0, data, 2, 5)
    Array.Copy(encodeShiftJis trainerName, 0, data, 7, 11) // TrainerName required

    match Xanthos.Core.Records.CH.parse data with
    | Ok record ->
        Assert.Equal(trainerCode, record.TrainerCode)
        Assert.Equal(trainerName, record.TrainerName)
        // Optional fields should be None
        Assert.Equal(None, record.TrainerNameKana)
        Assert.Equal(None, record.BelongsTo)
        Assert.Equal(None, record.InitialYear)
        Assert.Equal(None, record.BirthDate)
    | Error err -> failwithf "Should parse with empty optional fields: %A" err

// ============================================================================
// Branch Coverage Improvement Tests - Option Field Variations
// ============================================================================

[<Fact>]
let ``RC parser handles empty optional fields returning None`` () =
    let data = Array.create 150 32uy
    Array.Copy(encodeShiftJis "RC", 0, data, 0, 2)
    Array.Copy(encodeShiftJis "1234", 0, data, 2, 4)
    Array.Copy(encodeShiftJis "TestRace", 0, data, 6, 8)
    // Leave Grade (offset 76), RaceCondition (offset 77), Distance (offset 79), TrackSurface (offset 83) as spaces

    match Xanthos.Core.Records.RC.parse data with
    | Ok record ->
        Assert.Equal("1234", record.RaceCode)
        Assert.Equal(None, record.Grade) // Empty code should be None
        Assert.Equal(None, record.RaceCondition)
        Assert.Equal(None, record.Distance) // Empty int should be None
        Assert.Equal(None, record.TrackSurface)
    | Error err -> failwithf "Should parse with empty optional fields: %A" err

[<Fact>]
let ``RC parser handles all optional fields with valid values`` () =
    let data = Array.create 150 32uy
    Array.Copy(encodeShiftJis "RC", 0, data, 0, 2)
    Array.Copy(encodeShiftJis "5678", 0, data, 2, 4)
    Array.Copy(encodeShiftJis "TestRace2", 0, data, 6, 9)
    Array.Copy(encodeShiftJis "ShortName", 0, data, 56, 9)
    Array.Copy(encodeShiftJis "2", 0, data, 76, 1) // Grade: G2
    Array.Copy(encodeShiftJis "01", 0, data, 77, 2) // RaceCondition
    Array.Copy(encodeShiftJis "1800", 0, data, 79, 4) // Distance
    Array.Copy(encodeShiftJis "2", 0, data, 83, 1) // TrackSurface: Dirt

    match Xanthos.Core.Records.RC.parse data with
    | Ok record ->
        Assert.Equal("5678", record.RaceCode)
        Assert.True(record.RaceNameShort.IsSome)
        Assert.Equal(Some GradeCode.G2, record.Grade)
        Assert.Equal(Some 1800, record.Distance)
        Assert.Equal(Some TrackSurfaceCode.Dirt, record.TrackSurface)
    | Error err -> failwithf "Should parse with all fields: %A" err

[<Fact>]
let ``RC parser handles unknown code values`` () =
    let data = Array.create 150 32uy
    Array.Copy(encodeShiftJis "RC", 0, data, 0, 2)
    Array.Copy(encodeShiftJis "9999", 0, data, 2, 4)
    Array.Copy(encodeShiftJis "TestRace", 0, data, 6, 8)
    Array.Copy(encodeShiftJis "9", 0, data, 76, 1) // Unknown grade code
    Array.Copy(encodeShiftJis "99", 0, data, 77, 2) // Unknown race condition
    Array.Copy(encodeShiftJis "9", 0, data, 83, 1) // Unknown track surface

    match Xanthos.Core.Records.RC.parse data with
    | Ok record ->
        // Unknown codes should still parse (as None or Unknown variant)
        Assert.Equal("9999", record.RaceCode)
    | Error err -> failwithf "Should handle unknown codes: %A" err

[<Fact>]
let ``BR parser handles empty optional fields returning None`` () =
    let data = Array.create 300 32uy
    Array.Copy(encodeShiftJis "BR", 0, data, 0, 2)
    Array.Copy(encodeShiftJis "2020123456", 0, data, 2, 10)
    Array.Copy(encodeShiftJis "TestHorse", 0, data, 12, 9)
    // Leave HairColor (offset 48), BirthYear (offset 49) as spaces

    match Xanthos.Core.Records.BR.parse data with
    | Ok record ->
        Assert.Equal("2020123456", record.HorseId)
        Assert.Equal(None, record.HairColor)
        Assert.Equal(None, record.BirthYear)
        Assert.Equal(None, record.FatherName) // Empty text should be None
        Assert.Equal(None, record.MotherName)
    | Error err -> failwithf "Should parse with empty optional fields: %A" err

[<Fact>]
let ``BR parser handles all optional fields with valid values`` () =
    let data = Array.create 300 32uy
    Array.Copy(encodeShiftJis "BR", 0, data, 0, 2)
    Array.Copy(encodeShiftJis "2021654321", 0, data, 2, 10)
    Array.Copy(encodeShiftJis "TestHorse2", 0, data, 12, 10)
    Array.Copy(encodeShiftJis "1", 0, data, 48, 1) // HairColor: Black
    Array.Copy(encodeShiftJis "2019", 0, data, 49, 4) // BirthYear
    Array.Copy(encodeShiftJis "FatherHorse", 0, data, 53, 11) // FatherName
    Array.Copy(encodeShiftJis "MotherHorse", 0, data, 89, 11) // MotherName
    Array.Copy(encodeShiftJis "GrandFather", 0, data, 125, 11) // MotherFatherName

    match Xanthos.Core.Records.BR.parse data with
    | Ok record ->
        Assert.Equal("2021654321", record.HorseId)
        Assert.Equal(Some HairColorCode.Chestnut, record.HairColor)
        Assert.Equal(Some 2019, record.BirthYear)
        Assert.True(record.FatherName.IsSome)
        Assert.True(record.MotherName.IsSome)
        Assert.True(record.MotherFatherName.IsSome)
    | Error err -> failwithf "Should parse with all fields: %A" err

[<Fact>]
let ``BN parser handles empty optional fields returning None`` () =
    let data = Array.create 300 32uy
    Array.Copy(encodeShiftJis "BN", 0, data, 0, 2)
    Array.Copy(encodeShiftJis "2020111111", 0, data, 2, 10)
    Array.Copy(encodeShiftJis "TestBreed", 0, data, 12, 9)
    // Leave Sex (offset 48), HairColor (offset 49), BirthYear (offset 50) as spaces

    match Xanthos.Core.Records.BN.parse data with
    | Ok record ->
        Assert.Equal("2020111111", record.HorseId)
        Assert.Equal(None, record.Sex)
        Assert.Equal(None, record.HairColor)
        Assert.Equal(None, record.BirthYear)
    | Error err -> failwithf "Should parse with empty optional fields: %A" err

[<Fact>]
let ``BN parser handles all optional fields with valid values`` () =
    let data = Array.create 300 32uy
    Array.Copy(encodeShiftJis "BN", 0, data, 0, 2)
    Array.Copy(encodeShiftJis "2021222222", 0, data, 2, 10)
    Array.Copy(encodeShiftJis "TestBreed2", 0, data, 12, 10)
    Array.Copy(encodeShiftJis "2", 0, data, 48, 1) // Sex: Female
    Array.Copy(encodeShiftJis "4", 0, data, 49, 1) // HairColor: Black
    Array.Copy(encodeShiftJis "2018", 0, data, 50, 4) // BirthYear
    Array.Copy(encodeShiftJis "Father", 0, data, 54, 6)

    match Xanthos.Core.Records.BN.parse data with
    | Ok record ->
        Assert.Equal("2021222222", record.HorseId)
        Assert.Equal(Some SexCode.Female, record.Sex)
        Assert.Equal(Some HairColorCode.DarkBay, record.HairColor)
        Assert.Equal(Some 2018, record.BirthYear)
    | Error err -> failwithf "Should parse with all fields: %A" err

[<Fact>]
let ``AV parser handles empty optional fields`` () =
    let data = Array.create 50 32uy
    Array.Copy(encodeShiftJis "AV", 0, data, 0, 2)
    Array.Copy(encodeShiftJis "2024050512345601", 0, data, 2, 16)
    // Leave OldTrackCondition (offset 18), NewTrackCondition (offset 19), UpdateTime (offset 20) as spaces

    match Xanthos.Core.Records.AV.parse data with
    | Ok record ->
        Assert.Equal("2024050512345601", record.RaceKey)
        Assert.Equal(None, record.OldTrackCondition)
        Assert.Equal(None, record.NewTrackCondition)
        Assert.Equal(None, record.UpdateTime)
    | Error err -> failwithf "Should parse with empty optional fields: %A" err

[<Fact>]
let ``AV parser handles all fields with valid values`` () =
    let data = Array.create 50 32uy
    Array.Copy(encodeShiftJis "AV", 0, data, 0, 2)
    Array.Copy(encodeShiftJis "2024050512345602", 0, data, 2, 16)
    Array.Copy(encodeShiftJis "1", 0, data, 18, 1) // OldTrackCondition: Good
    Array.Copy(encodeShiftJis "2", 0, data, 19, 1) // NewTrackCondition: Yielding
    Array.Copy(encodeShiftJis "202405051200", 0, data, 20, 12) // UpdateTime

    match Xanthos.Core.Records.AV.parse data with
    | Ok record ->
        Assert.Equal("2024050512345602", record.RaceKey)
        Assert.Equal(Some TrackConditionCode.Good, record.OldTrackCondition)
        Assert.Equal(Some TrackConditionCode.Yielding, record.NewTrackCondition)
        Assert.True(record.UpdateTime.IsSome)
    | Error err -> failwithf "Should parse with all fields: %A" err

[<Fact>]
let ``WE parser handles empty optional fields`` () =
    let data = Array.create 50 32uy
    Array.Copy(encodeShiftJis "WE", 0, data, 0, 2)
    Array.Copy(encodeShiftJis "2024050512345603", 0, data, 2, 16)
    // Leave OldWeather, NewWeather, UpdateTime as spaces

    match Xanthos.Core.Records.WE.parse data with
    | Ok record ->
        Assert.Equal("2024050512345603", record.RaceKey)
        Assert.Equal(None, record.OldWeather)
        Assert.Equal(None, record.NewWeather)
        Assert.Equal(None, record.UpdateTime)
    | Error err -> failwithf "Should parse with empty optional fields: %A" err

[<Fact>]
let ``WE parser handles all fields with valid values`` () =
    let data = Array.create 50 32uy
    Array.Copy(encodeShiftJis "WE", 0, data, 0, 2)
    Array.Copy(encodeShiftJis "2024050512345604", 0, data, 2, 16)
    Array.Copy(encodeShiftJis "1", 0, data, 18, 1) // OldWeather
    Array.Copy(encodeShiftJis "2", 0, data, 19, 1) // NewWeather
    Array.Copy(encodeShiftJis "202405051430", 0, data, 20, 12) // UpdateTime

    match Xanthos.Core.Records.WE.parse data with
    | Ok record ->
        Assert.Equal("2024050512345604", record.RaceKey)
        Assert.True(record.OldWeather.IsSome)
        Assert.True(record.NewWeather.IsSome)
        Assert.True(record.UpdateTime.IsSome)
    | Error err -> failwithf "Should parse with all fields: %A" err

[<Fact>]
let ``TC parser handles empty optional fields`` () =
    let data = Array.create 100 32uy
    Array.Copy(encodeShiftJis "TC", 0, data, 0, 2)
    Array.Copy(encodeShiftJis "2024050512345678", 0, data, 2, 16)
    // Leave other fields as spaces

    match Xanthos.Core.Records.TC.parse data with
    | Ok record ->
        Assert.Equal("2024050512345678", record.RaceKey)
        Assert.Equal(None, record.HorseNumber)
        Assert.Equal(None, record.TrainingType)
    | Error err -> failwithf "Should parse with empty optional fields: %A" err

[<Fact>]
let ``JC parser handles empty optional fields`` () =
    let data = Array.create 200 32uy
    Array.Copy(encodeShiftJis "JC", 0, data, 0, 2)
    Array.Copy(encodeShiftJis "2024050512345678", 0, data, 2, 16)
    // Leave other fields as spaces

    match Xanthos.Core.Records.JC.parse data with
    | Ok record ->
        Assert.Equal("2024050512345678", record.RaceKey)
        Assert.Equal(None, record.HorseNumber)
        Assert.Equal(None, record.OldJockeyName)
    | Error err -> failwithf "Should parse with empty optional fields: %A" err

[<Fact>]
let ``CC parser handles empty optional fields`` () =
    let data = Array.create 100 32uy
    Array.Copy(encodeShiftJis "CC", 0, data, 0, 2)
    Array.Copy(encodeShiftJis "2024050512345678", 0, data, 2, 16)
    // Leave other fields as spaces

    match Xanthos.Core.Records.CC.parse data with
    | Ok record ->
        Assert.Equal("2024050512345678", record.RaceKey)
        Assert.Equal(None, record.OldTrackSurface)
        Assert.Equal(None, record.OldDistance)
    | Error err -> failwithf "Should parse with empty optional fields: %A" err

// ============================================================================
// Odds Record Branch Coverage Tests
// ============================================================================

[<Fact>]
let ``O2 parser handles empty odds fields`` () =
    let data = Array.create 100 32uy
    Array.Copy(encodeShiftJis "O2", 0, data, 0, 2)
    Array.Copy(encodeShiftJis "2024050512345678", 0, data, 2, 16)
    Array.Copy(encodeShiftJis "01", 0, data, 18, 2)
    // Leave odds fields as spaces

    match Xanthos.Core.Records.O2.parse data with
    | Ok record ->
        Assert.Equal("2024050512345678", record.RaceKey)
        Assert.Equal(None, record.OddsMin)
        Assert.Equal(None, record.OddsMax)
    | Error err -> failwithf "Should parse with empty odds: %A" err

[<Fact>]
let ``O3 parser handles empty odds fields`` () =
    let data = Array.create 100 32uy
    Array.Copy(encodeShiftJis "O3", 0, data, 0, 2)
    Array.Copy(encodeShiftJis "2024050512345678", 0, data, 2, 16)
    Array.Copy(encodeShiftJis "1", 0, data, 18, 1) // BracketNumber1
    Array.Copy(encodeShiftJis "2", 0, data, 19, 1) // BracketNumber2
    // Leave Odds (offset 20) as spaces

    match Xanthos.Core.Records.O3.parse data with
    | Ok record ->
        Assert.Equal("2024050512345678", record.RaceKey)
        Assert.Equal(None, record.Odds)
    | Error err -> failwithf "Should parse with empty odds: %A" err

[<Fact>]
let ``O4 parser handles empty odds fields`` () =
    let data = Array.create 100 32uy
    Array.Copy(encodeShiftJis "O4", 0, data, 0, 2)
    Array.Copy(encodeShiftJis "2024050512345678", 0, data, 2, 16)
    Array.Copy(encodeShiftJis "01", 0, data, 18, 2)
    Array.Copy(encodeShiftJis "02", 0, data, 20, 2)
    // Leave odds as spaces

    match Xanthos.Core.Records.O4.parse data with
    | Ok record ->
        Assert.Equal("2024050512345678", record.RaceKey)
        Assert.Equal(None, record.Odds)
    | Error err -> failwithf "Should parse with empty odds: %A" err

[<Fact>]
let ``O5 parser handles empty odds fields`` () =
    let data = Array.create 100 32uy
    Array.Copy(encodeShiftJis "O5", 0, data, 0, 2)
    Array.Copy(encodeShiftJis "2024050512345678", 0, data, 2, 16)
    Array.Copy(encodeShiftJis "01", 0, data, 18, 2)
    Array.Copy(encodeShiftJis "02", 0, data, 20, 2)
    // Leave odds as spaces

    match Xanthos.Core.Records.O5.parse data with
    | Ok record ->
        Assert.Equal("2024050512345678", record.RaceKey)
        Assert.Equal(None, record.OddsMin)
        Assert.Equal(None, record.OddsMax)
    | Error err -> failwithf "Should parse with empty odds: %A" err

[<Fact>]
let ``O6 parser handles empty odds fields`` () =
    let data = Array.create 100 32uy
    Array.Copy(encodeShiftJis "O6", 0, data, 0, 2)
    Array.Copy(encodeShiftJis "2024050512345678", 0, data, 2, 16)
    Array.Copy(encodeShiftJis "01", 0, data, 18, 2)
    // Leave popularity and odds as spaces

    match Xanthos.Core.Records.O6.parse data with
    | Ok record ->
        Assert.Equal("2024050512345678", record.RaceKey)
        Assert.Equal(None, record.Popularity)
    | Error err -> failwithf "Should parse with empty odds: %A" err

[<Fact>]
let ``H5 parser handles empty optional fields`` () =
    let data = Array.create 100 32uy
    Array.Copy(encodeShiftJis "H5", 0, data, 0, 2)
    Array.Copy(encodeShiftJis "2024050512345678", 0, data, 2, 16)
    // Leave optional fields as spaces

    match Xanthos.Core.Records.H5.parse data with
    | Ok record ->
        Assert.Equal("2024050512345678", record.RaceKey)
        Assert.Equal(None, record.Payoff)
        Assert.Equal(None, record.Popularity)
    | Error err -> failwithf "Should parse with empty fields: %A" err

[<Fact>]
let ``H6 parser handles empty optional fields`` () =
    let data = Array.create 100 32uy
    Array.Copy(encodeShiftJis "H6", 0, data, 0, 2)
    Array.Copy(encodeShiftJis "2024050512345678", 0, data, 2, 16)
    // Leave optional fields as spaces

    match Xanthos.Core.Records.H6.parse data with
    | Ok record ->
        Assert.Equal("2024050512345678", record.RaceKey)
        Assert.Equal(None, record.Payoff)
        Assert.Equal(None, record.Popularity)
    | Error err -> failwithf "Should parse with empty fields: %A" err

[<Fact>]
let ``HR parser handles empty optional fields`` () =
    let data = Array.create 100 32uy
    Array.Copy(encodeShiftJis "HR", 0, data, 0, 2)
    Array.Copy(encodeShiftJis "2024050512345678", 0, data, 2, 16)
    // Leave optional fields as spaces

    match Xanthos.Core.Records.HR.parse data with
    | Ok record ->
        Assert.Equal("2024050512345678", record.RaceKey)
        Assert.Equal(None, record.BetType)
        Assert.Equal(None, record.Payoff)
    | Error err -> failwithf "Should parse with empty fields: %A" err

[<Fact>]
let ``HR parser handles all optional fields with values`` () =
    let data = Array.create 100 32uy
    Array.Copy(encodeShiftJis "HR", 0, data, 0, 2)
    Array.Copy(encodeShiftJis "2024050512345679", 0, data, 2, 16)
    Array.Copy(encodeShiftJis "1", 0, data, 18, 1) // BetType
    Array.Copy(encodeShiftJis "05", 0, data, 19, 2) // HorseNumber1
    Array.Copy(encodeShiftJis "000012345", 0, data, 25, 9) // Payoff

    match Xanthos.Core.Records.HR.parse data with
    | Ok record ->
        Assert.Equal("2024050512345679", record.RaceKey)
        Assert.Equal(Some 1, record.BetType)
        Assert.Equal(Some 5, record.HorseNumber1)
        Assert.Equal(Some 12345, record.Payoff)
    | Error err -> failwithf "Should parse with all fields: %A" err

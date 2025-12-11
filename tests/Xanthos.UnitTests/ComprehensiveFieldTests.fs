module Xanthos.UnitTests.ComprehensiveFieldTests

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
open Xanthos.Core.Records.O1
open Xanthos.Core.Records.H1

// ============================================================================
// TK Record - Comprehensive Field Tests (12 fields)
// ============================================================================

[<Fact>]
let ``TK record parses all fields correctly`` () =
    let data = Array.create 346 32uy
    let raceKey = "2024050512345678"
    let horseId = "2020105678"
    let horseName = "テストホース"
    let fatherName = "テスト父"
    let motherName = "テスト母"
    let motherFatherName = "テスト母父"
    let trainerName = "テスト調教師"
    let ownerName = "テスト馬主"
    let breederName = "テスト生産者"

    // Record type and required fields
    Array.Copy(encodeShiftJis "TK", 0, data, 0, 2)
    Array.Copy(encodeShiftJis raceKey, 0, data, 2, 16)
    Array.Copy(encodeShiftJis horseId, 0, data, 18, 10)
    Array.Copy(encodeShiftJis horseName, 0, data, 28, min 36 (encodeShiftJis(horseName).Length))

    // Optional code fields
    Array.Copy(encodeShiftJis "1", 0, data, 64, 1) // Sex: Male
    Array.Copy(encodeShiftJis "1", 0, data, 65, 1) // Hair: Chestnut

    // Optional text fields
    Array.Copy(encodeShiftJis fatherName, 0, data, 66, min 36 (encodeShiftJis(fatherName).Length))
    Array.Copy(encodeShiftJis motherName, 0, data, 102, min 36 (encodeShiftJis(motherName).Length))
    Array.Copy(encodeShiftJis motherFatherName, 0, data, 138, min 36 (encodeShiftJis(motherFatherName).Length))

    // Optional int field
    Array.Copy(encodeShiftJis "2020", 0, data, 174, 4) // Birth year

    // More optional text fields
    Array.Copy(encodeShiftJis trainerName, 0, data, 178, min 34 (encodeShiftJis(trainerName).Length))
    Array.Copy(encodeShiftJis ownerName, 0, data, 212, min 64 (encodeShiftJis(ownerName).Length))
    Array.Copy(encodeShiftJis breederName, 0, data, 276, min 70 (encodeShiftJis(breederName).Length))

    match TK.parse data with
    | Ok record ->
        // Required fields
        Assert.Equal(raceKey, record.RaceKey)
        Assert.Equal(horseId, record.HorseId)
        Assert.StartsWith("テスト", record.HorseName)

        // Optional code fields
        Assert.Equal(Some SexCode.Male, record.Sex)
        Assert.Equal(Some HairColorCode.Chestnut, record.HairColor)

        // Optional text fields
        Assert.True(record.FatherName.IsSome)
        Assert.True(record.MotherName.IsSome)
        Assert.True(record.MotherFatherName.IsSome)

        // Optional int field
        Assert.Equal(Some 2020, record.BirthYear)

        // More optional text fields
        Assert.True(record.TrainerName.IsSome)
        Assert.True(record.OwnerName.IsSome)
        Assert.True(record.BreederName.IsSome)
    | Error err -> failwithf "TK parsing failed: %A" err

// ============================================================================
// RA Record - Comprehensive Field Tests
// ============================================================================

[<Fact>]
let ``RA record parses all major fields correctly`` () =
    let data = Array.create 366 32uy
    let raceKey = "2024050512345678"
    let raceName = "TestRace"

    Array.Copy(encodeShiftJis "RA", 0, data, 0, 2)
    Array.Copy(encodeShiftJis raceKey, 0, data, 2, 16)
    Array.Copy(encodeShiftJis raceName, 0, data, 18, 8) // RaceName required
    Array.Copy(encodeShiftJis "05", 0, data, 68, 2) // Racecourse: Tokyo
    Array.Copy(encodeShiftJis "1", 0, data, 70, 1) // Track surface: Turf
    Array.Copy(encodeShiftJis "1", 0, data, 71, 1) // Track condition: Good
    Array.Copy(encodeShiftJis "2000", 0, data, 72, 4) // Distance
    Array.Copy(encodeShiftJis "1", 0, data, 76, 1) // Grade: G1
    Array.Copy(encodeShiftJis "12", 0, data, 91, 2) // Entry count

    match RA.parse data with
    | Ok record ->
        Assert.Equal(raceKey, record.RaceKey)
        Assert.Equal(Some RacecourseCode.Tokyo, record.RacecourseCode)
        Assert.Equal(Some TrackSurfaceCode.Turf, record.TrackSurface)
        Assert.Equal(Some TrackConditionCode.Good, record.TrackCondition)
        Assert.Equal(Some 2000, record.Distance)
        Assert.Equal(Some GradeCode.G1, record.Grade)
        Assert.Equal(Some 12, record.EntryCount)
    | Error err -> failwithf "RA parsing failed: %A" err

// ============================================================================
// SE Record - Comprehensive Field Tests
// ============================================================================

[<Fact>]
let ``SE record parses all major fields correctly`` () =
    let data = Array.create 1446 32uy
    let raceKey = "2024050512345678"
    let horseId = "2020105678"

    Array.Copy(encodeShiftJis "SE", 0, data, 0, 2)
    Array.Copy(encodeShiftJis raceKey, 0, data, 2, 16)
    Array.Copy(encodeShiftJis horseId, 0, data, 18, 10)
    Array.Copy(encodeShiftJis "テストホース", 0, data, 28, encodeShiftJis("テストホース").Length)
    Array.Copy(encodeShiftJis "2", 0, data, 64, 1) // Gate number
    Array.Copy(encodeShiftJis "03", 0, data, 65, 2) // Horse number
    Array.Copy(encodeShiftJis "テスト騎手", 0, data, 67, encodeShiftJis("テスト騎手").Length) // Jockey name
    Array.Copy(encodeShiftJis "540", 0, data, 101, 3) // Jockey weight 54.0kg
    Array.Copy(encodeShiftJis "1", 0, data, 104, 1) // Sex: Male
    Array.Copy(encodeShiftJis "04", 0, data, 105, 2) // Age
    Array.Copy(encodeShiftJis "480", 0, data, 107, 3) // Horse weight 480kg
    Array.Copy(encodeShiftJis "  0", 0, data, 110, 3) // Weight diff 0
    Array.Copy(encodeShiftJis "00125", 0, data, 113, 5) // Odds 12.5
    Array.Copy(encodeShiftJis "02", 0, data, 118, 2) // Popularity

    match SE.parse data with
    | Ok record ->
        Assert.Equal(raceKey, record.RaceKey)
        Assert.Equal(horseId, record.HorseId)
        Assert.StartsWith("テスト", record.HorseName)
        Assert.Equal(Some 2, record.GateNumber)
        Assert.Equal(Some 3, record.HorseNumber)
        Assert.True(record.JockeyName.IsSome)
        Assert.Equal(Some 54.0M, record.JockeyWeight)
        Assert.Equal(Some SexCode.Male, record.Sex)
        Assert.Equal(Some 4, record.Age)
        Assert.Equal(Some 480, record.Weight)
        Assert.Equal(Some 0, record.WeightDiff)
        Assert.Equal(Some 12.5M, record.Odds)
        Assert.Equal(Some 2, record.Popularity)
    | Error err -> failwithf "SE parsing failed: %A" err

// ============================================================================
// O1 Record - Comprehensive Field Tests
// ============================================================================

[<Fact>]
let ``O1 record parses all fields correctly`` () =
    let data = Array.create 100 32uy
    let raceKey = "2024050512345678"

    Array.Copy(encodeShiftJis "O1", 0, data, 0, 2)
    Array.Copy(encodeShiftJis raceKey, 0, data, 2, 16)
    Array.Copy(encodeShiftJis "05", 0, data, 18, 2) // Horse number
    Array.Copy(encodeShiftJis "0125", 0, data, 20, 4) // Odds 12.5
    Array.Copy(encodeShiftJis "02", 0, data, 24, 2) // Popularity
    Array.Copy(encodeShiftJis "20241126133000", 0, data, 26, 14) // Update time

    match O1.parse data with
    | Ok record ->
        Assert.Equal(raceKey, record.RaceKey)
        Assert.Equal(Some 5, record.HorseNumber)
        Assert.Equal(Some 12.5M, record.Odds)
        Assert.Equal(Some 2, record.Popularity)
        Assert.True(record.UpdateTime.IsSome)
    | Error err -> failwithf "O1 parsing failed: %A" err

// ============================================================================
// H1 Record - Comprehensive Field Tests
// ============================================================================

[<Fact>]
let ``H1 record parses all fields correctly`` () =
    let data = Array.create 100 32uy
    let raceKey = "2024050512345678"

    Array.Copy(encodeShiftJis "H1", 0, data, 0, 2)
    Array.Copy(encodeShiftJis raceKey, 0, data, 2, 16)
    Array.Copy(encodeShiftJis "1", 0, data, 18, 1) // Bet type: Win
    Array.Copy(encodeShiftJis "05", 0, data, 19, 2) // Horse number
    Array.Copy(encodeShiftJis "001250", 0, data, 21, 6) // Payoff 1250 yen
    Array.Copy(encodeShiftJis "02", 0, data, 27, 2) // Popularity

    match H1.parse data with
    | Ok record ->
        Assert.Equal(raceKey, record.RaceKey)
        Assert.Equal(Some 1, record.BetType)
        Assert.Equal(Some 5, record.HorseNumber)
        Assert.Equal(Some 1250, record.Payoff)
        Assert.Equal(Some 2, record.Popularity)
    | Error err -> failwithf "H1 parsing failed: %A" err

// ============================================================================
// Field Validation Tests - Code Tables
// ============================================================================

[<Fact>]
let ``TK record validates all SexCode values`` () =
    let sexCodes =
        [ ("1", Some SexCode.Male)
          ("2", Some SexCode.Female)
          ("3", Some SexCode.Gelding)
          ("9", None) ] // Invalid

    sexCodes
    |> List.iter (fun (code, expected) ->
        let data = Array.create 346 32uy
        Array.Copy(encodeShiftJis "TK", 0, data, 0, 2)
        Array.Copy(encodeShiftJis "2024050512345678", 0, data, 2, 16)
        Array.Copy(encodeShiftJis "2020105678", 0, data, 18, 10) // HorseId required
        Array.Copy(encodeShiftJis "TestHorse", 0, data, 28, 9) // HorseName required
        Array.Copy(encodeShiftJis code, 0, data, 64, 1)

        match TK.parse data with
        | Ok record -> Assert.Equal(expected, record.Sex)
        | Error err -> failwithf "Should parse successfully: %A" err)

[<Fact>]
let ``TK record validates all HairColorCode values`` () =
    let hairColors =
        [ ("1", Some HairColorCode.Chestnut)
          ("2", Some HairColorCode.Liver)
          ("3", Some HairColorCode.Bay)
          ("4", Some HairColorCode.DarkBay)
          ("5", Some HairColorCode.Brown)
          ("6", Some HairColorCode.Black)
          ("7", Some HairColorCode.Gray)
          ("8", Some HairColorCode.Roan)
          ("9", Some HairColorCode.Palomino)
          ("Z", None) ] // Invalid

    hairColors
    |> List.iter (fun (code, expected) ->
        let data = Array.create 346 32uy
        Array.Copy(encodeShiftJis "TK", 0, data, 0, 2)
        Array.Copy(encodeShiftJis "2024050512345678", 0, data, 2, 16)
        Array.Copy(encodeShiftJis "2020105678", 0, data, 18, 10) // HorseId required
        Array.Copy(encodeShiftJis "TestHorse", 0, data, 28, 9) // HorseName required
        Array.Copy(encodeShiftJis code, 0, data, 65, 1)

        match TK.parse data with
        | Ok record -> Assert.Equal(expected, record.HairColor)
        | Error err -> failwithf "Should parse successfully: %A" err)

[<Fact>]
let ``RA record validates all TrackSurfaceCode values`` () =
    let trackSurfaces =
        [ ("1", Some TrackSurfaceCode.Turf)
          ("2", Some TrackSurfaceCode.Dirt)
          ("3", Some TrackSurfaceCode.Obstacle)
          ("9", None) ] // Invalid

    trackSurfaces
    |> List.iter (fun (code, expected) ->
        let data = Array.create 366 32uy
        Array.Copy(encodeShiftJis "RA", 0, data, 0, 2)
        Array.Copy(encodeShiftJis "2024050512345678", 0, data, 2, 16)
        Array.Copy(encodeShiftJis "TestRace", 0, data, 18, 8) // RaceName required
        Array.Copy(encodeShiftJis code, 0, data, 70, 1) // Correct offset

        match RA.parse data with
        | Ok record -> Assert.Equal(expected, record.TrackSurface)
        | Error err -> failwithf "Should parse successfully: %A" err)

[<Fact>]
let ``RA record validates all TrackConditionCode values`` () =
    let trackConditions =
        [ ("1", Some TrackConditionCode.Good)
          ("2", Some TrackConditionCode.Yielding)
          ("3", Some TrackConditionCode.Soft)
          ("4", Some TrackConditionCode.Heavy)
          ("9", None) ] // Invalid

    trackConditions
    |> List.iter (fun (code, expected) ->
        let data = Array.create 366 32uy
        Array.Copy(encodeShiftJis "RA", 0, data, 0, 2)
        Array.Copy(encodeShiftJis "2024050512345678", 0, data, 2, 16)
        Array.Copy(encodeShiftJis "TestRace", 0, data, 18, 8) // RaceName required
        Array.Copy(encodeShiftJis code, 0, data, 71, 1) // Correct offset

        match RA.parse data with
        | Ok record -> Assert.Equal(expected, record.TrackCondition)
        | Error _ -> failwith "Should parse successfully")

[<Fact>]
let ``RA record validates all GradeCode values`` () =
    let grades =
        [ ("1", Some GradeCode.G1)
          ("2", Some GradeCode.G2)
          ("3", Some GradeCode.G3)
          ("4", Some GradeCode.Listed)
          ("5", Some GradeCode.OpenClass) ]

    grades
    |> List.iter (fun (code, expected) ->
        let data = Array.create 366 32uy
        Array.Copy(encodeShiftJis "RA", 0, data, 0, 2)
        Array.Copy(encodeShiftJis "2024050512345678", 0, data, 2, 16)
        Array.Copy(encodeShiftJis "TestRace", 0, data, 18, 8) // RaceName required
        Array.Copy(encodeShiftJis code, 0, data, 76, 1) // Correct offset

        match RA.parse data with
        | Ok record -> Assert.Equal(expected, record.Grade)
        | Error _ -> failwith "Should parse successfully")

// ============================================================================
// Boundary Value Tests for All Field Types
// ============================================================================

[<Fact>]
let ``SE record handles boundary weight values`` () =
    let weights =
        [ ("300", Some 300) // Minimum
          ("600", Some 600) // Maximum
          ("480", Some 480) ] // Typical

    weights
    |> List.iter (fun (weightStr, expected) ->
        let data = Array.create 1446 32uy
        Array.Copy(encodeShiftJis "SE", 0, data, 0, 2)
        Array.Copy(encodeShiftJis "2024050512345678", 0, data, 2, 16)
        Array.Copy(encodeShiftJis "2020105678", 0, data, 18, 10) // HorseId required
        Array.Copy(encodeShiftJis "TestHorse", 0, data, 28, 9) // HorseName required
        Array.Copy(encodeShiftJis weightStr, 0, data, 107, 3)

        match SE.parse data with
        | Ok record -> Assert.Equal(expected, record.Weight)
        | Error _ -> failwith "Should parse successfully")

[<Fact>]
let ``O1 record handles boundary odds values`` () =
    let odds =
        [ ("0001", Some 0.1M) // Minimum
          ("9999", Some 999.9M) // Maximum
          ("0010", Some 1.0M) ] // Even money

    odds
    |> List.iter (fun (oddsStr, expected) ->
        let data = Array.create 100 32uy
        Array.Copy(encodeShiftJis "O1", 0, data, 0, 2)
        Array.Copy(encodeShiftJis "2024050512345678", 0, data, 2, 16)
        Array.Copy(encodeShiftJis "03", 0, data, 18, 2)
        Array.Copy(encodeShiftJis oddsStr, 0, data, 20, 4)

        match O1.parse data with
        | Ok record -> Assert.Equal(expected, record.Odds)
        | Error _ -> failwith "Should parse successfully")

[<Fact>]
let ``H1 record handles boundary payoff values`` () =
    let payoffs =
        [ ("000100", Some 100) // Minimum
          ("999999", Some 999999) // Maximum
          ("001000", Some 1000) ] // Typical

    payoffs
    |> List.iter (fun (payoffStr, expected) ->
        let data = Array.create 100 32uy
        Array.Copy(encodeShiftJis "H1", 0, data, 0, 2)
        Array.Copy(encodeShiftJis "2024050512345678", 0, data, 2, 16)
        Array.Copy(encodeShiftJis "1", 0, data, 18, 1)
        Array.Copy(encodeShiftJis "03", 0, data, 19, 2)
        Array.Copy(encodeShiftJis payoffStr, 0, data, 21, 6)

        match H1.parse data with
        | Ok record -> Assert.Equal(expected, record.Payoff)
        | Error _ -> failwith "Should parse successfully")

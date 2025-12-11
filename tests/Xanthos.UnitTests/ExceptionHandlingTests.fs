module Xanthos.UnitTests.ExceptionHandlingTests

open System
open Xunit
open Xanthos.Core
open Xanthos.Core.Text
open Xanthos.Core.Records
open Xanthos.Core.Records.RecordParser
open Xanthos.Core.Records.TK
open Xanthos.Core.Records.RA
open Xanthos.Core.Records.SE
open Xanthos.Core.Records.HR
open Xanthos.Core.Records.O1
open Xanthos.Core.Records.H1
open Xanthos.Core.Records.WF
open Xanthos.Core.Records.UM

// ============================================================================
// Required Field Validation Tests - Must return Error for missing fields
// ============================================================================

[<Fact>]
let ``TK parser returns Error for empty RaceKey`` () =
    let data = Array.create 346 32uy // All spaces
    Array.Copy(encodeShiftJis "TK", 0, data, 0, 2)
    // RaceKey is all spaces - should return Error

    match TK.parse data with
    | Ok _ -> failwith "Should return Error for empty required field RaceKey"
    | Error err -> Assert.Contains("RaceKey", sprintf "%A" err)

[<Fact>]
let ``TK parser returns Error for empty HorseId`` () =
    let data = Array.create 346 32uy
    Array.Copy(encodeShiftJis "TK", 0, data, 0, 2)
    Array.Copy(encodeShiftJis "2024050512345678", 0, data, 2, 16)
    // HorseId is all spaces - should return Error

    match TK.parse data with
    | Ok _ -> failwith "Should return Error for empty required field HorseId"
    | Error err -> Assert.Contains("HorseId", sprintf "%A" err)

[<Fact>]
let ``TK parser returns Error for empty HorseName`` () =
    let data = Array.create 346 32uy
    Array.Copy(encodeShiftJis "TK", 0, data, 0, 2)
    Array.Copy(encodeShiftJis "2024050512345678", 0, data, 2, 16)
    Array.Copy(encodeShiftJis "2020105678", 0, data, 18, 10)
    // HorseName is all spaces - should return Error

    match TK.parse data with
    | Ok _ -> failwith "Should return Error for empty required field HorseName"
    | Error err -> Assert.Contains("HorseName", sprintf "%A" err)

[<Fact>]
let ``RA parser returns Error for empty RaceKey`` () =
    let data = Array.create 366 32uy // All spaces
    Array.Copy(encodeShiftJis "RA", 0, data, 0, 2)

    match RA.parse data with
    | Ok _ -> failwith "Should return Error for empty required field RaceKey"
    | Error err -> Assert.Contains("RaceKey", sprintf "%A" err)

[<Fact>]
let ``RA parser returns Error for empty RaceName`` () =
    let data = Array.create 366 32uy
    Array.Copy(encodeShiftJis "RA", 0, data, 0, 2)
    Array.Copy(encodeShiftJis "2024050512345678", 0, data, 2, 16)
    // RaceName is all spaces - should return Error

    match RA.parse data with
    | Ok _ -> failwith "Should return Error for empty required field RaceName"
    | Error err -> Assert.Contains("RaceName", sprintf "%A" err)

[<Fact>]
let ``SE parser returns Error for empty RaceKey`` () =
    let data = Array.create 1446 32uy // All spaces
    Array.Copy(encodeShiftJis "SE", 0, data, 0, 2)

    match SE.parse data with
    | Ok _ -> failwith "Should return Error for empty required field RaceKey"
    | Error err -> Assert.Contains("RaceKey", sprintf "%A" err)

[<Fact>]
let ``SE parser returns Error for empty HorseId`` () =
    let data = Array.create 1446 32uy
    Array.Copy(encodeShiftJis "SE", 0, data, 0, 2)
    Array.Copy(encodeShiftJis "2024050512345678", 0, data, 2, 16)
    // HorseId is all spaces

    match SE.parse data with
    | Ok _ -> failwith "Should return Error for empty required field HorseId"
    | Error err -> Assert.Contains("HorseId", sprintf "%A" err)

[<Fact>]
let ``SE parser returns Error for empty HorseName`` () =
    let data = Array.create 1446 32uy
    Array.Copy(encodeShiftJis "SE", 0, data, 0, 2)
    Array.Copy(encodeShiftJis "2024050512345678", 0, data, 2, 16)
    Array.Copy(encodeShiftJis "2020105678", 0, data, 18, 10) // HorseId at offset 18
    // HorseName at offset 28 is all spaces - should return Error

    match SE.parse data with
    | Ok _ -> failwith "Should return Error for empty required field HorseName"
    | Error err -> Assert.Contains("HorseName", sprintf "%A" err)

[<Fact>]
let ``O1 parser returns Error for empty RaceKey`` () =
    let data = Array.create 100 32uy // All spaces
    Array.Copy(encodeShiftJis "O1", 0, data, 0, 2)

    match O1.parse data with
    | Ok _ -> failwith "Should return Error for empty required field RaceKey"
    | Error err -> Assert.Contains("RaceKey", sprintf "%A" err)

[<Fact>]
let ``H1 parser returns Error for empty RaceKey`` () =
    let data = Array.create 100 32uy // All spaces
    Array.Copy(encodeShiftJis "H1", 0, data, 0, 2)

    match H1.parse data with
    | Ok _ -> failwith "Should return Error for empty required field RaceKey"
    | Error err -> Assert.Contains("RaceKey", sprintf "%A" err)

[<Fact>]
let ``WF parser returns Error for empty RaceKey`` () =
    let data = Array.create 100 32uy // All spaces
    Array.Copy(encodeShiftJis "WF", 0, data, 0, 2)

    match WF.parse data with
    | Ok _ -> failwith "Should return Error for empty required field RaceKey"
    | Error err -> Assert.Contains("RaceKey", sprintf "%A" err)

[<Fact>]
let ``UM parser returns Error for empty HorseId`` () =
    let data = Array.create 500 32uy // All spaces
    Array.Copy(encodeShiftJis "UM", 0, data, 0, 2)

    match UM.parse data with
    | Ok _ -> failwith "Should return Error for empty required field HorseId"
    | Error err -> Assert.Contains("HorseId", sprintf "%A" err)

[<Fact>]
let ``UM parser returns Error for empty HorseName`` () =
    let data = Array.create 500 32uy
    Array.Copy(encodeShiftJis "UM", 0, data, 0, 2)
    Array.Copy(encodeShiftJis "2020105678", 0, data, 2, 10) // HorseId

    match UM.parse data with
    | Ok _ -> failwith "Should return Error for empty required field HorseName"
    | Error err -> Assert.Contains("HorseName", sprintf "%A" err)

// ============================================================================
// Null/Empty Data Robustness Tests - Should return Error for null-filled data
// ============================================================================

[<Fact>]
let ``TK parser returns Error for null-filled data`` () =
    let data = Array.create 346 0uy // All nulls
    Array.Copy(encodeShiftJis "TK", 0, data, 0, 2)

    match TK.parse data with
    | Ok _ -> failwith "Should return Error for null-filled data"
    | Error _ -> Assert.True(true)

[<Fact>]
let ``RA parser returns Error for null-filled data`` () =
    let data = Array.create 366 0uy // All nulls
    Array.Copy(encodeShiftJis "RA", 0, data, 0, 2)

    match RA.parse data with
    | Ok _ -> failwith "Should return Error for null-filled data"
    | Error _ -> Assert.True(true)

[<Fact>]
let ``SE parser returns Error for null-filled data`` () =
    let data = Array.create 1446 0uy // All nulls
    Array.Copy(encodeShiftJis "SE", 0, data, 0, 2)

    match SE.parse data with
    | Ok _ -> failwith "Should return Error for null-filled data"
    | Error _ -> Assert.True(true)

// ============================================================================
// Mixed Valid/Invalid Data Tests - Error when required fields missing
// ============================================================================

[<Fact>]
let ``TK parser returns Error when only RaceKey provided`` () =
    let data = Array.create 346 32uy
    Array.Copy(encodeShiftJis "TK", 0, data, 0, 2)
    Array.Copy(encodeShiftJis "2024050512345678", 0, data, 2, 16)
    // HorseId and HorseName are empty - should return Error

    match TK.parse data with
    | Ok _ -> failwith "Should return Error when required fields missing"
    | Error err -> Assert.Contains("HorseId", sprintf "%A" err)

[<Fact>]
let ``RA parser returns Error when only RaceKey provided`` () =
    let data = Array.create 366 32uy
    Array.Copy(encodeShiftJis "RA", 0, data, 0, 2)
    Array.Copy(encodeShiftJis "2024050512345678", 0, data, 2, 16)
    // RaceName is empty - should return Error

    match RA.parse data with
    | Ok _ -> failwith "Should return Error when required fields missing"
    | Error err -> Assert.Contains("RaceName", sprintf "%A" err)

[<Fact>]
let ``SE parser returns Error when only RaceKey provided`` () =
    let data = Array.create 1446 32uy
    Array.Copy(encodeShiftJis "SE", 0, data, 0, 2)
    Array.Copy(encodeShiftJis "2024050512345678", 0, data, 2, 16)
    // HorseId and HorseName are empty - should return Error

    match SE.parse data with
    | Ok _ -> failwith "Should return Error when required fields missing"
    | Error err -> Assert.Contains("HorseId", sprintf "%A" err)

// ============================================================================
// Option Type None Branch Coverage - With valid required fields
// ============================================================================

[<Fact>]
let ``TK parser returns None for all optional fields when empty`` () =
    let data = Array.create 346 32uy
    Array.Copy(encodeShiftJis "TK", 0, data, 0, 2)
    Array.Copy(encodeShiftJis "2024050512345678", 0, data, 2, 16)
    Array.Copy(encodeShiftJis "2020105678", 0, data, 18, 10) // HorseId
    Array.Copy(encodeShiftJis "TestHorse", 0, data, 28, 9) // HorseName

    match TK.parse data with
    | Ok record ->
        // All optional fields should be None
        Assert.Equal(None, record.Sex)
        Assert.Equal(None, record.HairColor)
        Assert.Equal(None, record.FatherName)
        Assert.Equal(None, record.MotherName)
        Assert.Equal(None, record.MotherFatherName)
        Assert.Equal(None, record.BirthYear)
        Assert.Equal(None, record.TrainerName)
        Assert.Equal(None, record.OwnerName)
        Assert.Equal(None, record.BreederName)
    | Error err -> failwithf "Should parse successfully: %A" err

[<Fact>]
let ``SE parser returns None for all optional fields when empty`` () =
    let data = Array.create 1446 32uy
    Array.Copy(encodeShiftJis "SE", 0, data, 0, 2)
    Array.Copy(encodeShiftJis "2024050512345678", 0, data, 2, 16)
    Array.Copy(encodeShiftJis "2020105678", 0, data, 18, 10) // HorseId at offset 18
    Array.Copy(encodeShiftJis "TestHorse", 0, data, 28, 9) // HorseName at offset 28

    match SE.parse data with
    | Ok record ->
        // All optional fields should be None
        Assert.Equal(None, record.GateNumber)
        Assert.Equal(None, record.HorseNumber)
        Assert.Equal(None, record.JockeyName)
        Assert.Equal(None, record.JockeyWeight)
        Assert.Equal(None, record.Sex)
        Assert.Equal(None, record.Age)
        Assert.Equal(None, record.Weight)
        Assert.Equal(None, record.WeightDiff)
        Assert.Equal(None, record.Odds)
        Assert.Equal(None, record.Popularity)
        Assert.Equal(None, record.FinishPosition)
        Assert.Equal(None, record.RunningStyle)
        Assert.Equal(None, record.Time)
        Assert.Equal(None, record.TrainerName)
    | Error err -> failwithf "Should parse successfully: %A" err

[<Fact>]
let ``O1 parser returns None for all optional fields when empty`` () =
    let data = Array.create 100 32uy
    Array.Copy(encodeShiftJis "O1", 0, data, 0, 2)
    Array.Copy(encodeShiftJis "2024050512345678", 0, data, 2, 16)

    match O1.parse data with
    | Ok record ->
        Assert.Equal(None, record.HorseNumber)
        Assert.Equal(None, record.Odds)
        Assert.Equal(None, record.Popularity)
        Assert.Equal(None, record.UpdateTime)
    | Error err -> failwithf "Should parse successfully: %A" err

module Xanthos.UnitTests.LibraryTests

open Xunit
open Xanthos
open Xanthos.Core

// ============================================================================
// createRace Tests
// ============================================================================

// Note: createRace uses the name parameter for both RaceId and Name fields.
// Since RaceId requires a valid date prefix (yyyyMMdd), test values must start with a valid date.
// This is a design limitation - a future API might separate ID and name parameters.

let validRaceId = "20240101" // Minimum valid format
let validFullRaceId = "2024010106010801" // Full format with suffix

[<Fact>]
let ``createRace should succeed with valid race id format`` () =
    match createRace validRaceId with
    | Ok raceInfo ->
        Assert.Equal(validRaceId, raceInfo.Name)
        Assert.Equal(None, raceInfo.Course)
        Assert.Equal(None, raceInfo.DistanceMeters)
        Assert.Equal(TrackSurface.UnknownSurface, raceInfo.Surface)
        Assert.Equal(TrackCondition.UnknownCondition, raceInfo.Condition)
        Assert.Equal(None, raceInfo.ScheduledStart)
    | Error err -> failwithf "Should succeed with valid race id format: %A" err

[<Fact>]
let ``createRace should accept id with whitespace`` () =
    match createRace $"  {validRaceId}  " with
    | Ok raceInfo ->
        // Name field stores original value
        Assert.Equal($"  {validRaceId}  ", raceInfo.Name)
        // But RaceId is trimmed
        let idValue = RaceId.value raceInfo.Id
        Assert.Equal(validRaceId, idValue)
    | Error _ -> failwith "Should succeed with whitespace"

[<Fact>]
let ``createRace should succeed with full race key format`` () =
    match createRace validFullRaceId with
    | Ok raceInfo -> Assert.Equal(validFullRaceId, raceInfo.Name)
    | Error _ -> failwith "Should succeed with full race key format"

[<Fact>]
let ``createRace should succeed with alphanumeric suffix`` () =
    match createRace "20240101R123" with
    | Ok raceInfo -> Assert.Equal("20240101R123", raceInfo.Name)
    | Error _ -> failwith "Should succeed with alphanumeric suffix"

[<Fact>]
let ``createRace should fail with empty string`` () =
    match createRace "" with
    | Error _ -> () // Expected - RaceId cannot be empty
    | Ok _ -> failwith "Should fail with empty string"

[<Fact>]
let ``createRace should fail with whitespace only`` () =
    match createRace "   " with
    | Error _ -> () // Expected - RaceId cannot be empty
    | Ok _ -> failwith "Should fail with whitespace only"

[<Fact>]
let ``createRace should fail with tab only`` () =
    match createRace "\t" with
    | Error _ -> () // Expected - RaceId cannot be empty
    | Ok _ -> failwith "Should fail with tab only"

[<Fact>]
let ``createRace should fail with newline only`` () =
    match createRace "\n" with
    | Error _ -> () // Expected - RaceId cannot be empty
    | Ok _ -> failwith "Should fail with newline only"

[<Fact>]
let ``createRace should fail with invalid date prefix`` () =
    match createRace "TestRace" with
    | Error(ValidationError msg) -> Assert.Contains("valid date", msg)
    | Error _ -> failwith "Should be ValidationError with invalid date message"
    | Ok _ -> failwith "Should fail with invalid date prefix"

[<Fact>]
let ``createRace should fail with too short value`` () =
    match createRace "2024010" with // 7 characters
    | Error(ValidationError msg) -> Assert.Contains("at least 8 characters", msg)
    | Error _ -> failwith "Should be ValidationError"
    | Ok _ -> failwith "Should fail with too short value"

[<Fact>]
let ``createRace should set all optional fields to None`` () =
    match createRace validRaceId with
    | Ok raceInfo ->
        Assert.True(Option.isNone raceInfo.Course, "Course should be None")
        Assert.True(Option.isNone raceInfo.DistanceMeters, "DistanceMeters should be None")
        Assert.True(Option.isNone raceInfo.ScheduledStart, "ScheduledStart should be None")
    | Error _ -> failwith "Should succeed"

[<Fact>]
let ``createRace should set Surface to UnknownSurface`` () =
    match createRace validRaceId with
    | Ok raceInfo -> Assert.Equal(TrackSurface.UnknownSurface, raceInfo.Surface)
    | Error _ -> failwith "Should succeed"

[<Fact>]
let ``createRace should set Condition to UnknownCondition`` () =
    match createRace validRaceId with
    | Ok raceInfo -> Assert.Equal(TrackCondition.UnknownCondition, raceInfo.Condition)
    | Error _ -> failwith "Should succeed"

[<Fact>]
let ``createRace should preserve RaceId in Id field`` () =
    match createRace validRaceId with
    | Ok raceInfo ->
        // The RaceId should be created correctly
        let idValue = RaceId.value raceInfo.Id
        Assert.Equal(validRaceId, idValue)
    | Error _ -> failwith "Should succeed"

[<Fact>]
let ``createRace should handle long id with alphanumeric suffix`` () =
    let longId = validRaceId + String.replicate 50 "A"

    match createRace longId with
    | Ok raceInfo -> Assert.Equal(longId, raceInfo.Name)
    | Error _ -> failwith "Should succeed with long alphanumeric suffix"

[<Fact>]
let ``createRace error should be validation error for empty`` () =
    match createRace "" with
    | Error(ValidationError msg) ->
        // The error should be a validation error from RaceId.create
        Assert.Contains("RaceId cannot be empty", msg)
    | Error _ -> failwith "Should be ValidationError"
    | Ok _ -> failwith "Should fail"

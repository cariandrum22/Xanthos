module Xanthos.UnitTests.SerializationTests

open System
open System.Text.Json
open Xunit
open Xanthos.Core
open Xanthos.Core.Text
open Xanthos.Core.Serialization

// ============================================================================
// parseSurface - All Surface Type Branches
// ============================================================================

[<Theory>]
[<InlineData("Turf")>]
[<InlineData("turf")>]
[<InlineData("TURF")>]
[<InlineData(" Turf ")>]
let ``parseRaceCard should parse Turf surface`` (surfaceText: string) =
    let json =
        sprintf """[{"id":"20240505010101","name":"Test Race","surface":"%s","condition":"Fast"}]""" surfaceText

    let payload = encodeShiftJis json

    match parseRaceCard payload with
    | Ok races ->
        Assert.NotEmpty(races)
        Assert.Equal(TrackSurface.Turf, races.[0].Surface)
    | Error err -> failwith $"Parse failed: {err}"

[<Theory>]
[<InlineData("Dirt")>]
[<InlineData("dirt")>]
[<InlineData("DIRT")>]
[<InlineData(" Dirt ")>]
let ``parseRaceCard should parse Dirt surface`` (surfaceText: string) =
    let json =
        sprintf """[{"id":"20240505010101","name":"Test Race","surface":"%s","condition":"Fast"}]""" surfaceText

    let payload = encodeShiftJis json

    match parseRaceCard payload with
    | Ok races ->
        Assert.NotEmpty(races)
        Assert.Equal(TrackSurface.Dirt, races.[0].Surface)
    | Error err -> failwith $"Parse failed: {err}"

[<Theory>]
[<InlineData("Synthetic")>]
[<InlineData("synthetic")>]
[<InlineData("SYNTHETIC")>]
[<InlineData(" Synthetic ")>]
let ``parseRaceCard should parse Synthetic surface`` (surfaceText: string) =
    let json =
        sprintf """[{"id":"20240505010101","name":"Test Race","surface":"%s","condition":"Fast"}]""" surfaceText

    let payload = encodeShiftJis json

    match parseRaceCard payload with
    | Ok races ->
        Assert.NotEmpty(races)
        Assert.Equal(TrackSurface.Synthetic, races.[0].Surface)
    | Error err -> failwith $"Parse failed: {err}"

[<Theory>]
[<InlineData("Unknown")>]
[<InlineData("Invalid")>]
[<InlineData("XYZ")>]
let ``parseRaceCard should parse unknown surface as UnknownSurface`` (surfaceText: string) =
    let json =
        sprintf """[{"id":"20240505010101","name":"Test Race","surface":"%s","condition":"Fast"}]""" surfaceText

    let payload = encodeShiftJis json

    match parseRaceCard payload with
    | Ok races ->
        Assert.NotEmpty(races)
        Assert.Equal(TrackSurface.UnknownSurface, races.[0].Surface)
    | Error err -> failwith $"Parse failed: {err}"

// Empty surface strings cause validation errors in the implementation
[<Fact>]
let ``parseRaceCard should fail when surface is empty`` () =
    let json =
        """[{"id":"20240505010101","name":"Test Race","surface":"","condition":"Fast"}]"""

    let payload = encodeShiftJis json

    match parseRaceCard payload with
    | Error _ -> () // Expected - empty surface causes validation error
    | Ok _ -> failwith "Should have failed with empty surface"

// ============================================================================
// parseCondition - All Condition Type Branches
// ============================================================================

[<Theory>]
[<InlineData("Fast")>]
[<InlineData("fast")>]
[<InlineData("FAST")>]
[<InlineData(" Fast ")>]
let ``parseRaceCard should parse Fast condition`` (conditionText: string) =
    let json =
        sprintf """[{"id":"20240505010101","name":"Test Race","surface":"Turf","condition":"%s"}]""" conditionText

    let payload = encodeShiftJis json

    match parseRaceCard payload with
    | Ok races ->
        Assert.NotEmpty(races)
        Assert.Equal(TrackCondition.Fast, races.[0].Condition)
    | Error err -> failwith $"Parse failed: {err}"

[<Theory>]
[<InlineData("Good")>]
[<InlineData("good")>]
[<InlineData("GOOD")>]
[<InlineData(" Good ")>]
let ``parseRaceCard should parse Good condition`` (conditionText: string) =
    let json =
        sprintf """[{"id":"20240505010101","name":"Test Race","surface":"Turf","condition":"%s"}]""" conditionText

    let payload = encodeShiftJis json

    match parseRaceCard payload with
    | Ok races ->
        Assert.NotEmpty(races)
        Assert.Equal(TrackCondition.Good, races.[0].Condition)
    | Error err -> failwith $"Parse failed: {err}"

[<Theory>]
[<InlineData("Yielding")>]
[<InlineData("yielding")>]
[<InlineData("YIELDING")>]
[<InlineData(" Yielding ")>]
let ``parseRaceCard should parse Yielding condition`` (conditionText: string) =
    let json =
        sprintf """[{"id":"20240505010101","name":"Test Race","surface":"Turf","condition":"%s"}]""" conditionText

    let payload = encodeShiftJis json

    match parseRaceCard payload with
    | Ok races ->
        Assert.NotEmpty(races)
        Assert.Equal(TrackCondition.Yielding, races.[0].Condition)
    | Error err -> failwith $"Parse failed: {err}"

[<Theory>]
[<InlineData("Soft")>]
[<InlineData("soft")>]
[<InlineData("SOFT")>]
[<InlineData(" Soft ")>]
let ``parseRaceCard should parse Soft condition`` (conditionText: string) =
    let json =
        sprintf """[{"id":"20240505010101","name":"Test Race","surface":"Turf","condition":"%s"}]""" conditionText

    let payload = encodeShiftJis json

    match parseRaceCard payload with
    | Ok races ->
        Assert.NotEmpty(races)
        Assert.Equal(TrackCondition.Soft, races.[0].Condition)
    | Error err -> failwith $"Parse failed: {err}"

[<Theory>]
[<InlineData("Heavy")>]
[<InlineData("heavy")>]
[<InlineData("HEAVY")>]
[<InlineData(" Heavy ")>]
let ``parseRaceCard should parse Heavy condition`` (conditionText: string) =
    let json =
        sprintf """[{"id":"20240505010101","name":"Test Race","surface":"Turf","condition":"%s"}]""" conditionText

    let payload = encodeShiftJis json

    match parseRaceCard payload with
    | Ok races ->
        Assert.NotEmpty(races)
        Assert.Equal(TrackCondition.Heavy, races.[0].Condition)
    | Error err -> failwith $"Parse failed: {err}"

[<Theory>]
[<InlineData("Unknown")>]
[<InlineData("Invalid")>]
[<InlineData("XYZ")>]
let ``parseRaceCard should parse unknown condition as UnknownCondition`` (conditionText: string) =
    let json =
        sprintf """[{"id":"20240505010101","name":"Test Race","surface":"Turf","condition":"%s"}]""" conditionText

    let payload = encodeShiftJis json

    match parseRaceCard payload with
    | Ok races ->
        Assert.NotEmpty(races)
        Assert.Equal(TrackCondition.UnknownCondition, races.[0].Condition)
    | Error err -> failwith $"Parse failed: {err}"

[<Fact>]
let ``parseRaceCard should fail when condition is empty`` () =
    let json =
        """[{"id":"20240505010101","name":"Test Race","surface":"Turf","condition":""}]"""

    let payload = encodeShiftJis json

    match parseRaceCard payload with
    | Error _ -> () // Expected - empty condition causes validation error
    | Ok _ -> failwith "Should have failed with empty condition"

// ============================================================================
// Error Cases - JSON Property Validation
// ============================================================================

[<Fact>]
let ``parseRaceCard should fail when id is missing`` () =
    let json = """[{"name":"Test Race","surface":"Turf","condition":"Fast"}]"""
    let payload = encodeShiftJis json

    match parseRaceCard payload with
    | Error _ -> () // Expected
    | Ok _ -> failwith "Should have failed with missing id"

[<Fact>]
let ``parseRaceCard should fail when id is null`` () =
    let json =
        """[{"id":null,"name":"Test Race","surface":"Turf","condition":"Fast"}]"""

    let payload = encodeShiftJis json

    match parseRaceCard payload with
    | Error _ -> () // Expected
    | Ok _ -> failwith "Should have failed with null id"

[<Fact>]
let ``parseRaceCard should fail when id is empty`` () =
    let json = """[{"id":"","name":"Test Race","surface":"Turf","condition":"Fast"}]"""
    let payload = encodeShiftJis json

    match parseRaceCard payload with
    | Error _ -> () // Expected
    | Ok _ -> failwith "Should have failed with empty id"

[<Fact>]
let ``parseRaceCard should fail when name is missing`` () =
    let json = """[{"id":"20240505010101","surface":"Turf","condition":"Fast"}]"""
    let payload = encodeShiftJis json

    match parseRaceCard payload with
    | Error _ -> () // Expected
    | Ok _ -> failwith "Should have failed with missing name"

[<Fact>]
let ``parseRaceCard should fail when surface is missing`` () =
    let json = """[{"id":"20240505010101","name":"Test Race","condition":"Fast"}]"""
    let payload = encodeShiftJis json

    match parseRaceCard payload with
    | Error _ -> () // Expected
    | Ok _ -> failwith "Should have failed with missing surface"

[<Fact>]
let ``parseRaceCard should fail when condition is missing`` () =
    let json = """[{"id":"20240505010101","name":"Test Race","surface":"Turf"}]"""
    let payload = encodeShiftJis json

    match parseRaceCard payload with
    | Error _ -> () // Expected
    | Ok _ -> failwith "Should have failed with missing condition"

[<Fact>]
let ``parseRaceCard should handle optional course field missing`` () =
    let json =
        """[{"id":"20240505010101","name":"Test Race","surface":"Turf","condition":"Fast"}]"""

    let payload = encodeShiftJis json

    match parseRaceCard payload with
    | Ok races ->
        Assert.NotEmpty(races)
        Assert.Equal(None, races.[0].Course)
    | Error err -> failwith $"Parse should succeed: {err}"

[<Fact>]
let ``parseRaceCard should handle optional distanceMeters field missing`` () =
    let json =
        """[{"id":"20240505010101","name":"Test Race","surface":"Turf","condition":"Fast"}]"""

    let payload = encodeShiftJis json

    match parseRaceCard payload with
    | Ok races ->
        Assert.NotEmpty(races)
        Assert.Equal(None, races.[0].DistanceMeters)
    | Error err -> failwith $"Parse should succeed: {err}"

[<Fact>]
let ``parseRaceCard should handle optional scheduledStart field missing`` () =
    let json =
        """[{"id":"20240505010101","name":"Test Race","surface":"Turf","condition":"Fast"}]"""

    let payload = encodeShiftJis json

    match parseRaceCard payload with
    | Ok races ->
        Assert.NotEmpty(races)
        Assert.Equal(None, races.[0].ScheduledStart)
    | Error err -> failwith $"Parse should succeed: {err}"

[<Fact>]
let ``parseRaceCard should handle optional course field as null`` () =
    let json =
        """[{"id":"20240505010101","name":"Test Race","surface":"Turf","condition":"Fast","course":null}]"""

    let payload = encodeShiftJis json

    match parseRaceCard payload with
    | Ok races ->
        Assert.NotEmpty(races)
        Assert.Equal(None, races.[0].Course)
    | Error err -> failwith $"Parse should succeed: {err}"

[<Fact>]
let ``parseRaceCard should parse optional distanceMeters as positive integer`` () =
    let json =
        """[{"id":"20240505010101","name":"Test Race","surface":"Turf","condition":"Fast","distanceMeters":1200}]"""

    let payload = encodeShiftJis json

    match parseRaceCard payload with
    | Ok races ->
        Assert.NotEmpty(races)
        Assert.Equal(Some 1200, races.[0].DistanceMeters)
    | Error err -> failwith $"Parse should succeed: {err}"

[<Fact>]
let ``parseRaceCard should preserve negative distanceMeters`` () =
    let json =
        """[{"id":"20240505010101","name":"Test Race","surface":"Turf","condition":"Fast","distanceMeters":-1200}]"""

    let payload = encodeShiftJis json

    match parseRaceCard payload with
    | Ok races ->
        Assert.NotEmpty(races)
        Assert.Equal(Some -1200, races.[0].DistanceMeters)
    | Error err -> failwith $"Parse should succeed: {err}"

[<Fact>]
let ``parseRaceCard should fail when distanceMeters is not a number`` () =
    let json =
        """[{"id":"20240505010101","name":"Test Race","surface":"Turf","condition":"Fast","distanceMeters":"abc"}]"""

    let payload = encodeShiftJis json

    match parseRaceCard payload with
    | Error _ -> () // Expected
    | Ok _ -> failwith "Should have failed with non-numeric distance"

[<Fact>]
let ``parseRaceCard should parse optional scheduledStart as ISO8601`` () =
    let json =
        """[{"id":"20240505010101","name":"Test Race","surface":"Turf","condition":"Fast","scheduledStart":"2024-05-05T15:30:00Z"}]"""

    let payload = encodeShiftJis json

    match parseRaceCard payload with
    | Ok races ->
        Assert.NotEmpty(races)
        Assert.True(races.[0].ScheduledStart.IsSome)
        let dt = races.[0].ScheduledStart.Value
        Assert.Equal(DateTime(2024, 5, 5, 15, 30, 0, DateTimeKind.Utc), dt)
    | Error err -> failwith $"Parse should succeed: {err}"

// ============================================================================
// Edge Cases - Empty and Invalid JSON
// ============================================================================

[<Fact>]
let ``parseRaceCard should return empty list for null payload`` () =
    match parseRaceCard null with
    | Ok races -> Assert.Empty(races)
    | Error err -> failwith $"Should succeed with empty list: {err}"

[<Fact>]
let ``parseRaceCard should return empty list for empty payload`` () =
    match parseRaceCard [||] with
    | Ok races -> Assert.Empty(races)
    | Error err -> failwith $"Should succeed with empty list: {err}"

[<Fact>]
let ``parseRaceCard should fail for invalid JSON`` () =
    let payload = encodeShiftJis "not valid json"

    match parseRaceCard payload with
    | Error _ -> () // Expected
    | Ok _ -> failwith "Should have failed with invalid JSON"

[<Fact>]
let ``parseRaceCard should fail when root is not an array`` () =
    let json =
        """{"id":"20240505010101","name":"Test Race","surface":"Turf","condition":"Fast"}"""

    let payload = encodeShiftJis json

    match parseRaceCard payload with
    | Error _ -> () // Expected
    | Ok _ -> failwith "Should have failed with non-array JSON"

[<Fact>]
let ``parseRaceCard should parse empty JSON array`` () =
    let json = """[]"""
    let payload = encodeShiftJis json

    match parseRaceCard payload with
    | Ok races -> Assert.Empty(races)
    | Error err -> failwith $"Should succeed with empty list: {err}"

// ============================================================================
// parseOdds - Error Cases
// ============================================================================

[<Fact>]
let ``parseOdds should return empty list for null payload`` () =
    match parseOdds null with
    | Ok odds -> Assert.Empty(odds)
    | Error err -> failwith $"Should succeed with empty list: {err}"

[<Fact>]
let ``parseOdds should return empty list for empty payload`` () =
    match parseOdds [||] with
    | Ok odds -> Assert.Empty(odds)
    | Error err -> failwith $"Should succeed with empty list: {err}"

[<Fact>]
let ``parseOdds should fail for invalid JSON`` () =
    let payload = encodeShiftJis "not valid json"

    match parseOdds payload with
    | Error _ -> () // Expected
    | Ok _ -> failwith "Should have failed with invalid JSON"

[<Fact>]
let ``parseOdds should fail when root is not an array`` () =
    let json =
        """{"raceId":"20240505010101","timestamp":"2024-05-05T12:00:00Z","entries":[]}"""

    let payload = encodeShiftJis json

    match parseOdds payload with
    | Error _ -> () // Expected
    | Ok _ -> failwith "Should have failed with non-array JSON"

[<Fact>]
let ``parseOdds should fail when raceId is missing`` () =
    let json = """[{"timestamp":"2024-05-05T12:00:00Z","entries":[]}]"""
    let payload = encodeShiftJis json

    match parseOdds payload with
    | Error _ -> () // Expected
    | Ok _ -> failwith "Should have failed with missing raceId"

[<Fact>]
let ``parseOdds should fail when timestamp is missing`` () =
    let json = """[{"raceId":"20240505010101","entries":[]}]"""
    let payload = encodeShiftJis json

    match parseOdds payload with
    | Error _ -> () // Expected
    | Ok _ -> failwith "Should have failed with missing timestamp"

[<Fact>]
let ``parseOdds should fail when timestamp is null`` () =
    let json = """[{"raceId":"20240505010101","timestamp":null,"entries":[]}]"""
    let payload = encodeShiftJis json

    match parseOdds payload with
    | Error _ -> () // Expected
    | Ok _ -> failwith "Should have failed with null timestamp"

[<Fact>]
let ``parseOdds should fail when entries is missing`` () =
    let json = """[{"raceId":"20240505010101","timestamp":"2024-05-05T12:00:00Z"}]"""
    let payload = encodeShiftJis json

    match parseOdds payload with
    | Error _ -> () // Expected
    | Ok _ -> failwith "Should have failed with missing entries"

[<Fact>]
let ``parseOdds should fail when entries is not an array`` () =
    let json =
        """[{"raceId":"20240505010101","timestamp":"2024-05-05T12:00:00Z","entries":"not-an-array"}]"""

    let payload = encodeShiftJis json

    match parseOdds payload with
    | Error _ -> () // Expected
    | Ok _ -> failwith "Should have failed with non-array entries"

[<Fact>]
let ``parseOdds should parse successfully with valid data`` () =
    let json =
        """[{"raceId":"20240505010101","timestamp":"2024-05-05T12:00:00Z","entries":[]}]"""

    let payload = encodeShiftJis json

    match parseOdds payload with
    | Ok odds ->
        Assert.NotEmpty(odds)
        Assert.Empty(odds.[0].Entries)
    | Error err -> failwith $"Parse should succeed: {err}"

[<Fact>]
let ``parseOdds should fail when runnerIds repeat within a snapshot`` () =
    let json =
        """
        [
          {
            "raceId": "20240505010101",
            "timestamp": "2024-05-05T12:00:00Z",
            "entries": [
              { "runnerId": "01", "winOdds": 1.2, "placeOdds": 1.1 },
              { "runnerId": "01", "winOdds": 2.3, "placeOdds": 2.1 }
            ]
          }
        ]
        """

    let payload = encodeShiftJis json

    match parseOdds payload with
    | Error _ -> () // expected
    | Ok _ -> failwith "Should have failed due to duplicate runnerId entries"

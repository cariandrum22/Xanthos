module Xanthos.UnitTests.ValidationTests

open System
open Xunit
open Xanthos.Runtime.Validation

// ============================================================================
// normalizeDataspec Tests
// ============================================================================

[<Fact>]
let ``normalizeDataspec should accept valid 4-character dataspec`` () =
    match normalizeDataspec "RACE" with
    | Ok normalized -> Assert.Equal("RACE", normalized)
    | Error _ -> failwith "Should succeed with valid dataspec"

[<Fact>]
let ``normalizeDataspec should accept valid 8-character dataspec`` () =
    match normalizeDataspec "RACEodds" with
    | Ok normalized -> Assert.Equal("RACEODDS", normalized)
    | Error _ -> failwith "Should succeed with valid dataspec"

[<Fact>]
let ``normalizeDataspec should normalize to uppercase`` () =
    match normalizeDataspec "race" with
    | Ok normalized -> Assert.Equal("RACE", normalized)
    | Error _ -> failwith "Should succeed with lowercase input"

[<Fact>]
let ``normalizeDataspec should trim whitespace`` () =
    match normalizeDataspec "  RACE  " with
    | Ok normalized -> Assert.Equal("RACE", normalized)
    | Error _ -> failwith "Should succeed with whitespace"

[<Fact>]
let ``normalizeDataspec should accept alphanumeric characters`` () =
    match normalizeDataspec "RAC2" with
    | Ok normalized -> Assert.Equal("RAC2", normalized)
    | Error _ -> failwith "Should succeed with alphanumeric input"

[<Fact>]
let ``normalizeDataspec should fail with empty string`` () =
    match normalizeDataspec "" with
    | Error _ -> () // Expected
    | Ok _ -> failwith "Should fail with empty string"

[<Fact>]
let ``normalizeDataspec should fail with whitespace only`` () =
    match normalizeDataspec "   " with
    | Error _ -> () // Expected
    | Ok _ -> failwith "Should fail with whitespace only"

[<Fact>]
let ``normalizeDataspec should fail with length not multiple of 4`` () =
    match normalizeDataspec "RAC" with
    | Error _ -> () // Expected (length 3)
    | Ok _ -> failwith "Should fail with length 3"

[<Fact>]
let ``normalizeDataspec should fail with 5 characters`` () =
    match normalizeDataspec "RACES" with
    | Error _ -> () // Expected (length 5)
    | Ok _ -> failwith "Should fail with length 5"

[<Fact>]
let ``normalizeDataspec should fail with invalid characters`` () =
    match normalizeDataspec "RAC!" with
    | Error _ -> () // Expected (contains !)
    | Ok _ -> failwith "Should fail with special characters"

[<Fact>]
let ``normalizeDataspec should fail with spaces in middle`` () =
    match normalizeDataspec "RA E" with
    | Error _ -> () // Expected (contains space)
    | Ok _ -> failwith "Should fail with spaces in middle"

[<Fact>]
let ``normalizeDataspec should accept 12-character dataspec`` () =
    match normalizeDataspec "RACEODDSDATA" with
    | Ok normalized -> Assert.Equal("RACEODDSDATA", normalized)
    | Error _ -> failwith "Should succeed with 12 characters"

// ============================================================================
// parseOpenOption Tests
// ============================================================================

[<Fact>]
let ``parseOpenOption should default to 1 when None`` () =
    match parseOpenOption None with
    | Ok value -> Assert.Equal(1, value)
    | Error _ -> failwith "Should default to 1"

[<Fact>]
let ``parseOpenOption should accept 1`` () =
    match parseOpenOption (Some "1") with
    | Ok value -> Assert.Equal(1, value)
    | Error _ -> failwith "Should accept 1"

[<Fact>]
let ``parseOpenOption should accept 2`` () =
    match parseOpenOption (Some "2") with
    | Ok value -> Assert.Equal(2, value)
    | Error _ -> failwith "Should accept 2"

[<Fact>]
let ``parseOpenOption should accept 3`` () =
    match parseOpenOption (Some "3") with
    | Ok value -> Assert.Equal(3, value)
    | Error _ -> failwith "Should accept 3"

[<Fact>]
let ``parseOpenOption should accept 4`` () =
    match parseOpenOption (Some "4") with
    | Ok value -> Assert.Equal(4, value)
    | Error _ -> failwith "Should accept 4"

[<Fact>]
let ``parseOpenOption should trim whitespace`` () =
    match parseOpenOption (Some "  2  ") with
    | Ok value -> Assert.Equal(2, value)
    | Error _ -> failwith "Should accept with whitespace"

[<Fact>]
let ``parseOpenOption should fail with 0`` () =
    match parseOpenOption (Some "0") with
    | Error _ -> () // Expected (out of range 1-4)
    | Ok _ -> failwith "Should fail with 0"

[<Fact>]
let ``parseOpenOption should fail with 5`` () =
    match parseOpenOption (Some "5") with
    | Error _ -> () // Expected (out of range 1-4)
    | Ok _ -> failwith "Should fail with 5"

[<Fact>]
let ``parseOpenOption should fail with negative number`` () =
    match parseOpenOption (Some "-1") with
    | Error _ -> () // Expected
    | Ok _ -> failwith "Should fail with negative number"

[<Fact>]
let ``parseOpenOption should fail with non-numeric string`` () =
    match parseOpenOption (Some "abc") with
    | Error _ -> () // Expected
    | Ok _ -> failwith "Should fail with non-numeric string"

[<Fact>]
let ``parseOpenOption should fail with empty string`` () =
    match parseOpenOption (Some "") with
    | Error _ -> () // Expected
    | Ok _ -> failwith "Should fail with empty string"

// ============================================================================
// parseFromTime Tests
// ============================================================================

[<Fact>]
let ``parseFromTime should parse yyyyMMddHHmmss format`` () =
    match parseFromTime (Some "20240505123456") with
    | Ok dt ->
        Assert.Equal(2024, dt.Year)
        Assert.Equal(5, dt.Month)
        Assert.Equal(5, dt.Day)
        Assert.Equal(12, dt.Hour)
        Assert.Equal(34, dt.Minute)
        Assert.Equal(56, dt.Second)
    | Error _ -> failwith "Should parse yyyyMMddHHmmss format"

[<Fact>]
let ``parseFromTime should parse yyyyMMdd format`` () =
    match parseFromTime (Some "20240505") with
    | Ok dt ->
        Assert.Equal(2024, dt.Year)
        Assert.Equal(5, dt.Month)
        Assert.Equal(5, dt.Day)
        Assert.Equal(0, dt.Hour)
        Assert.Equal(0, dt.Minute)
        Assert.Equal(0, dt.Second)
    | Error _ -> failwith "Should parse yyyyMMdd format"

[<Fact>]
let ``parseFromTime should trim whitespace`` () =
    match parseFromTime (Some "  20240505  ") with
    | Ok dt ->
        Assert.Equal(2024, dt.Year)
        Assert.Equal(5, dt.Month)
        Assert.Equal(5, dt.Day)
    | Error _ -> failwith "Should parse with whitespace"

[<Fact>]
let ``parseFromTime should fail with None`` () =
    match parseFromTime None with
    | Error _ -> () // Expected (required parameter)
    | Ok _ -> failwith "Should fail with None"

[<Fact>]
let ``parseFromTime should fail with invalid format`` () =
    match parseFromTime (Some "2024-05-05") with
    | Error _ -> () // Expected (wrong format)
    | Ok _ -> failwith "Should fail with invalid format"

[<Fact>]
let ``parseFromTime should fail with partial date`` () =
    match parseFromTime (Some "202405") with
    | Error _ -> () // Expected (incomplete date)
    | Ok _ -> failwith "Should fail with partial date"

[<Fact>]
let ``parseFromTime should fail with invalid date`` () =
    match parseFromTime (Some "20241305") with
    | Error _ -> () // Expected (month 13 invalid)
    | Ok _ -> failwith "Should fail with invalid date"

[<Fact>]
let ``parseFromTime should fail with non-numeric string`` () =
    match parseFromTime (Some "abcdefgh") with
    | Error _ -> () // Expected
    | Ok _ -> failwith "Should fail with non-numeric string"

// ============================================================================
// buildOpenRequest Integration Tests
// ============================================================================

[<Fact>]
let ``buildOpenRequest should build valid request with all parameters`` () =
    match buildOpenRequest "RACE" (Some "20240505123456") (Some "2") with
    | Ok request ->
        Assert.Equal("RACE", request.Spec)
        Assert.Equal(2024, request.FromTime.Year)
        Assert.Equal(2, request.Option)
    | Error _ -> failwith "Should build valid request"

[<Fact>]
let ``buildOpenRequest should build request with default option`` () =
    match buildOpenRequest "RACE" (Some "20240505") None with
    | Ok request ->
        Assert.Equal("RACE", request.Spec)
        Assert.Equal(1, request.Option) // Default
    | Error _ -> failwith "Should build request with default option"

[<Fact>]
let ``buildOpenRequest should normalize dataspec`` () =
    match buildOpenRequest "race" (Some "20240505") None with
    | Ok request -> Assert.Equal("RACE", request.Spec) // Normalized to uppercase
    | Error _ -> failwith "Should normalize dataspec"

[<Fact>]
let ``buildOpenRequest should fail with invalid dataspec`` () =
    match buildOpenRequest "RAC" (Some "20240505") None with
    | Error _ -> () // Expected (invalid dataspec)
    | Ok _ -> failwith "Should fail with invalid dataspec"

[<Fact>]
let ``buildOpenRequest should fail with missing fromTime`` () =
    match buildOpenRequest "RACE" None None with
    | Error _ -> () // Expected (fromTime required)
    | Ok _ -> failwith "Should fail with missing fromTime"

[<Fact>]
let ``buildOpenRequest should fail with invalid option`` () =
    match buildOpenRequest "RACE" (Some "20240505") (Some "5") with
    | Error _ -> () // Expected (option out of range)
    | Ok _ -> failwith "Should fail with invalid option"

[<Fact>]
let ``buildOpenRequest should handle complex valid dataspec`` () =
    match buildOpenRequest "RACEODDS" (Some "20240505123456") (Some "3") with
    | Ok request ->
        Assert.Equal("RACEODDS", request.Spec)
        Assert.Equal(3, request.Option)
    | Error _ -> failwith "Should handle 8-character dataspec"

// ============================================================================
// Culture Invariant Date Parsing Regression Tests
// Verifies that DateTime parsing uses CultureInfo.InvariantCulture
// and does not break under non-Gregorian calendar cultures (e.g., Thai, Arabic)
// ============================================================================

[<Fact>]
let ``parseFromTime should work under Thai Buddhist calendar culture`` () =
    let originalCulture = System.Threading.Thread.CurrentThread.CurrentCulture

    try
        // Thai Buddhist calendar uses Buddhist Era (BE = CE + 543)
        // e.g., 2024 CE = 2567 BE
        System.Threading.Thread.CurrentThread.CurrentCulture <- System.Globalization.CultureInfo("th-TH")

        match parseFromTime (Some "20240505123456") with
        | Ok dt ->
            // Must parse as Gregorian year 2024, not Buddhist year 2024
            Assert.Equal(2024, dt.Year)
            Assert.Equal(5, dt.Month)
            Assert.Equal(5, dt.Day)
            Assert.Equal(12, dt.Hour)
            Assert.Equal(34, dt.Minute)
            Assert.Equal(56, dt.Second)
        | Error e -> failwithf "Should parse under Thai culture: %A" e
    finally
        System.Threading.Thread.CurrentThread.CurrentCulture <- originalCulture

[<Fact>]
let ``parseFromTime yyyyMMdd should work under Thai Buddhist calendar culture`` () =
    let originalCulture = System.Threading.Thread.CurrentThread.CurrentCulture

    try
        System.Threading.Thread.CurrentThread.CurrentCulture <- System.Globalization.CultureInfo("th-TH")

        match parseFromTime (Some "20240505") with
        | Ok dt ->
            Assert.Equal(2024, dt.Year)
            Assert.Equal(5, dt.Month)
            Assert.Equal(5, dt.Day)
        | Error e -> failwithf "Should parse under Thai culture: %A" e
    finally
        System.Threading.Thread.CurrentThread.CurrentCulture <- originalCulture

[<Fact>]
let ``parseFromTime should work under Arabic Saudi culture`` () =
    let originalCulture = System.Threading.Thread.CurrentThread.CurrentCulture

    try
        // Arabic Saudi Arabia uses Hijri calendar by default
        System.Threading.Thread.CurrentThread.CurrentCulture <- System.Globalization.CultureInfo("ar-SA")

        match parseFromTime (Some "20240505123456") with
        | Ok dt ->
            // Must parse as Gregorian year 2024, not Hijri
            Assert.Equal(2024, dt.Year)
            Assert.Equal(5, dt.Month)
            Assert.Equal(5, dt.Day)
        | Error e -> failwithf "Should parse under Arabic culture: %A" e
    finally
        System.Threading.Thread.CurrentThread.CurrentCulture <- originalCulture

[<Fact>]
let ``buildOpenRequest should work under non-Gregorian calendar culture`` () =
    let originalCulture = System.Threading.Thread.CurrentThread.CurrentCulture

    try
        System.Threading.Thread.CurrentThread.CurrentCulture <- System.Globalization.CultureInfo("th-TH")

        match buildOpenRequest "RACE" (Some "20240505123456") (Some "2") with
        | Ok request ->
            Assert.Equal("RACE", request.Spec)
            Assert.Equal(2024, request.FromTime.Year)
            Assert.Equal(5, request.FromTime.Month)
            Assert.Equal(5, request.FromTime.Day)
        | Error e -> failwithf "Should build request under Thai culture: %A" e
    finally
        System.Threading.Thread.CurrentThread.CurrentCulture <- originalCulture

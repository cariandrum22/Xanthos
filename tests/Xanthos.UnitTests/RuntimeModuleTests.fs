module Xanthos.UnitTests.RuntimeModuleTests

open System
open System.IO
open Xunit
open Xanthos.Core
open Xanthos.Interop
open Xanthos.Runtime

// ============================================================================
// Validation.normalizeDataspec Tests
// ============================================================================

module NormalizeDataspecTests =

    [<Fact>]
    let ``normalizeDataspec should accept valid 4-character spec`` () =
        match Validation.normalizeDataspec "RACE" with
        | Ok result -> Assert.Equal("RACE", result)
        | Error err -> failwithf "Expected Ok, got %A" err

    [<Fact>]
    let ``normalizeDataspec should accept valid 8-character spec`` () =
        match Validation.normalizeDataspec "RACETOKU" with
        | Ok result -> Assert.Equal("RACETOKU", result)
        | Error err -> failwithf "Expected Ok, got %A" err

    [<Fact>]
    let ``normalizeDataspec should trim whitespace`` () =
        match Validation.normalizeDataspec "  RACE  " with
        | Ok result -> Assert.Equal("RACE", result)
        | Error err -> failwithf "Expected Ok, got %A" err

    [<Fact>]
    let ``normalizeDataspec should convert to uppercase`` () =
        match Validation.normalizeDataspec "race" with
        | Ok result -> Assert.Equal("RACE", result)
        | Error err -> failwithf "Expected Ok, got %A" err

    [<Fact>]
    let ``normalizeDataspec should accept alphanumeric characters`` () =
        match Validation.normalizeDataspec "RAC1" with
        | Ok result -> Assert.Equal("RAC1", result)
        | Error err -> failwithf "Expected Ok, got %A" err

    [<Fact>]
    let ``normalizeDataspec should reject empty string`` () =
        match Validation.normalizeDataspec "" with
        | Error(ValidationError msg) -> Assert.Contains("non-empty", msg)
        | Error other -> failwithf "Expected ValidationError, got %A" other
        | Ok _ -> failwith "Should fail for empty string"

    [<Fact>]
    let ``normalizeDataspec should reject whitespace only`` () =
        match Validation.normalizeDataspec "   " with
        | Error(ValidationError msg) -> Assert.Contains("non-empty", msg)
        | Error other -> failwithf "Expected ValidationError, got %A" other
        | Ok _ -> failwith "Should fail for whitespace only"

    [<Fact>]
    let ``normalizeDataspec should reject non-multiple of 4`` () =
        match Validation.normalizeDataspec "RAC" with
        | Error(ValidationError msg) -> Assert.Contains("multiple of 4", msg)
        | Error other -> failwithf "Expected ValidationError, got %A" other
        | Ok _ -> failwith "Should fail for 3-character string"

    [<Fact>]
    let ``normalizeDataspec should reject 5-character string`` () =
        match Validation.normalizeDataspec "RACES" with
        | Error(ValidationError msg) -> Assert.Contains("multiple of 4", msg)
        | Error other -> failwithf "Expected ValidationError, got %A" other
        | Ok _ -> failwith "Should fail for 5-character string"

    [<Fact>]
    let ``normalizeDataspec should reject special characters`` () =
        match Validation.normalizeDataspec "RAC!" with
        | Error(ValidationError msg) -> Assert.Contains("invalid characters", msg)
        | Error other -> failwithf "Expected ValidationError, got %A" other
        | Ok _ -> failwith "Should fail for special characters"

    [<Fact>]
    let ``normalizeDataspec should reject spaces in middle`` () =
        // "RA CE" is 5 characters, so length check fails first (before invalid char check)
        match Validation.normalizeDataspec "RA CE" with
        | Error(ValidationError msg) -> Assert.Contains("multiple of 4", msg)
        | Error other -> failwithf "Expected ValidationError, got %A" other
        | Ok _ -> failwith "Should fail for spaces in middle"

    [<Fact>]
    let ``normalizeDataspec should reject spaces in 4-char block`` () =
        // "R CE" after trim is 4 characters with invalid space
        match Validation.normalizeDataspec "R CE" with
        | Error(ValidationError msg) -> Assert.Contains("invalid characters", msg)
        | Error other -> failwithf "Expected ValidationError, got %A" other
        | Ok _ -> failwith "Should fail for invalid characters"

// ============================================================================
// Validation.parseOpenOption Tests
// ============================================================================

module ParseOpenOptionTests =

    [<Fact>]
    let ``parseOpenOption should return 1 for None`` () =
        match Validation.parseOpenOption None with
        | Ok result -> Assert.Equal(1, result)
        | Error err -> failwithf "Expected Ok, got %A" err

    [<Fact>]
    let ``parseOpenOption should accept 1`` () =
        match Validation.parseOpenOption (Some "1") with
        | Ok result -> Assert.Equal(1, result)
        | Error err -> failwithf "Expected Ok, got %A" err

    [<Fact>]
    let ``parseOpenOption should accept 2`` () =
        match Validation.parseOpenOption (Some "2") with
        | Ok result -> Assert.Equal(2, result)
        | Error err -> failwithf "Expected Ok, got %A" err

    [<Fact>]
    let ``parseOpenOption should accept 3`` () =
        match Validation.parseOpenOption (Some "3") with
        | Ok result -> Assert.Equal(3, result)
        | Error err -> failwithf "Expected Ok, got %A" err

    [<Fact>]
    let ``parseOpenOption should accept 4`` () =
        match Validation.parseOpenOption (Some "4") with
        | Ok result -> Assert.Equal(4, result)
        | Error err -> failwithf "Expected Ok, got %A" err

    [<Fact>]
    let ``parseOpenOption should trim whitespace`` () =
        match Validation.parseOpenOption (Some "  2  ") with
        | Ok result -> Assert.Equal(2, result)
        | Error err -> failwithf "Expected Ok, got %A" err

    [<Fact>]
    let ``parseOpenOption should reject 0`` () =
        match Validation.parseOpenOption (Some "0") with
        | Error(ValidationError msg) -> Assert.Contains("between 1 and 4", msg)
        | Error other -> failwithf "Expected ValidationError, got %A" other
        | Ok _ -> failwith "Should fail for 0"

    [<Fact>]
    let ``parseOpenOption should reject 5`` () =
        match Validation.parseOpenOption (Some "5") with
        | Error(ValidationError msg) -> Assert.Contains("between 1 and 4", msg)
        | Error other -> failwithf "Expected ValidationError, got %A" other
        | Ok _ -> failwith "Should fail for 5"

    [<Fact>]
    let ``parseOpenOption should reject negative`` () =
        match Validation.parseOpenOption (Some "-1") with
        | Error(ValidationError msg) -> Assert.Contains("between 1 and 4", msg)
        | Error other -> failwithf "Expected ValidationError, got %A" other
        | Ok _ -> failwith "Should fail for negative"

    [<Fact>]
    let ``parseOpenOption should reject non-integer`` () =
        match Validation.parseOpenOption (Some "abc") with
        | Error(ValidationError msg) -> Assert.Contains("not a valid integer", msg)
        | Error other -> failwithf "Expected ValidationError, got %A" other
        | Ok _ -> failwith "Should fail for non-integer"

    [<Fact>]
    let ``parseOpenOption should reject float`` () =
        match Validation.parseOpenOption (Some "1.5") with
        | Error(ValidationError msg) -> Assert.Contains("not a valid integer", msg)
        | Error other -> failwithf "Expected ValidationError, got %A" other
        | Ok _ -> failwith "Should fail for float"

// ============================================================================
// Validation.parseFromTime Tests
// ============================================================================

module ParseFromTimeTests =

    [<Fact>]
    let ``parseFromTime should reject None`` () =
        match Validation.parseFromTime None with
        | Error(ValidationError msg) -> Assert.Contains("required", msg)
        | Error other -> failwithf "Expected ValidationError, got %A" other
        | Ok _ -> failwith "Should fail for None"

    [<Fact>]
    let ``parseFromTime should accept yyyyMMddHHmmss format`` () =
        match Validation.parseFromTime (Some "20240115123045") with
        | Ok result ->
            Assert.Equal(2024, result.Year)
            Assert.Equal(1, result.Month)
            Assert.Equal(15, result.Day)
            Assert.Equal(12, result.Hour)
            Assert.Equal(30, result.Minute)
            Assert.Equal(45, result.Second)
        | Error err -> failwithf "Expected Ok, got %A" err

    [<Fact>]
    let ``parseFromTime should accept yyyyMMdd format`` () =
        match Validation.parseFromTime (Some "20240115") with
        | Ok result ->
            Assert.Equal(2024, result.Year)
            Assert.Equal(1, result.Month)
            Assert.Equal(15, result.Day)
            Assert.Equal(0, result.Hour)
            Assert.Equal(0, result.Minute)
            Assert.Equal(0, result.Second)
        | Error err -> failwithf "Expected Ok, got %A" err

    [<Fact>]
    let ``parseFromTime should trim whitespace`` () =
        match Validation.parseFromTime (Some "  20240115  ") with
        | Ok result -> Assert.Equal(2024, result.Year)
        | Error err -> failwithf "Expected Ok, got %A" err

    [<Fact>]
    let ``parseFromTime should reject invalid format`` () =
        match Validation.parseFromTime (Some "2024-01-15") with
        | Error(ValidationError msg) -> Assert.Contains("Unable to parse", msg)
        | Error other -> failwithf "Expected ValidationError, got %A" other
        | Ok _ -> failwith "Should fail for invalid format"

    [<Fact>]
    let ``parseFromTime should reject partial date`` () =
        match Validation.parseFromTime (Some "202401") with
        | Error(ValidationError msg) -> Assert.Contains("Unable to parse", msg)
        | Error other -> failwithf "Expected ValidationError, got %A" other
        | Ok _ -> failwith "Should fail for partial date"

    [<Fact>]
    let ``parseFromTime should reject text`` () =
        match Validation.parseFromTime (Some "not a date") with
        | Error(ValidationError msg) -> Assert.Contains("Unable to parse", msg)
        | Error other -> failwithf "Expected ValidationError, got %A" other
        | Ok _ -> failwith "Should fail for text"

// ============================================================================
// Validation.buildOpenRequest Tests (also covers ResultOps.bind)
// ============================================================================

module BuildOpenRequestTests =

    [<Fact>]
    let ``buildOpenRequest should succeed with valid inputs`` () =
        match Validation.buildOpenRequest "RACE" (Some "20240115") None with
        | Ok request ->
            Assert.Equal("RACE", request.Spec)
            Assert.Equal(2024, request.FromTime.Year)
            Assert.Equal(1, request.Option)
        | Error err -> failwithf "Expected Ok, got %A" err

    [<Fact>]
    let ``buildOpenRequest should succeed with all parameters`` () =
        match Validation.buildOpenRequest "TOKU" (Some "20240115123045") (Some "3") with
        | Ok request ->
            Assert.Equal("TOKU", request.Spec)
            Assert.Equal(12, request.FromTime.Hour)
            Assert.Equal(3, request.Option)
        | Error err -> failwithf "Expected Ok, got %A" err

    [<Fact>]
    let ``buildOpenRequest should fail on invalid spec`` () =
        match Validation.buildOpenRequest "RAC" (Some "20240115") None with
        | Error(ValidationError msg) -> Assert.Contains("multiple of 4", msg)
        | Error other -> failwithf "Expected ValidationError, got %A" other
        | Ok _ -> failwith "Should fail for invalid spec"

    [<Fact>]
    let ``buildOpenRequest should fail on missing fromTime`` () =
        match Validation.buildOpenRequest "RACE" None None with
        | Error(ValidationError msg) -> Assert.Contains("required", msg)
        | Error other -> failwithf "Expected ValidationError, got %A" other
        | Ok _ -> failwith "Should fail for missing fromTime"

    [<Fact>]
    let ``buildOpenRequest should fail on invalid option`` () =
        match Validation.buildOpenRequest "RACE" (Some "20240115") (Some "9") with
        | Error(ValidationError msg) -> Assert.Contains("between 1 and 4", msg)
        | Error other -> failwithf "Expected ValidationError, got %A" other
        | Ok _ -> failwith "Should fail for invalid option"

    [<Fact>]
    let ``buildOpenRequest should normalize spec to uppercase`` () =
        match Validation.buildOpenRequest "race" (Some "20240115") None with
        | Ok request -> Assert.Equal("RACE", request.Spec)
        | Error err -> failwithf "Expected Ok, got %A" err

// ============================================================================
// TraceLogger Tests
// ============================================================================

module TraceLoggerTests =

    [<Fact>]
    let ``silent logger should have all functions`` () =
        let logger = TraceLogger.silent
        // Should not throw - just execute silently
        logger.Info "test info"
        logger.Warn "test warn"
        logger.Error "test error"
        logger.Debug "test debug"

    [<Fact>]
    let ``ofConsole should create logger with all functions`` () =
        let logger = TraceLogger.ofConsole ()
        // Verify all functions exist
        Assert.NotNull(logger.Info)
        Assert.NotNull(logger.Warn)
        Assert.NotNull(logger.Error)
        Assert.NotNull(logger.Debug)

    [<Fact>]
    let ``ofConsole Info should write to console`` () =
        let logger = TraceLogger.ofConsole ()
        use sw = new StringWriter()
        let originalOut = Console.Out

        try
            Console.SetOut(sw)
            logger.Info "test message"
            let output = sw.ToString()
            Assert.Contains("[INFO ]", output)
            Assert.Contains("test message", output)
        finally
            Console.SetOut(originalOut)

    [<Fact>]
    let ``ofConsole Warn should write to console`` () =
        let logger = TraceLogger.ofConsole ()
        use sw = new StringWriter()
        let originalOut = Console.Out

        try
            Console.SetOut(sw)
            logger.Warn "warning message"
            let output = sw.ToString()
            Assert.Contains("[WARN ]", output)
            Assert.Contains("warning message", output)
        finally
            Console.SetOut(originalOut)

    [<Fact>]
    let ``ofConsole Error should write to console`` () =
        let logger = TraceLogger.ofConsole ()
        use sw = new StringWriter()
        let originalOut = Console.Out

        try
            Console.SetOut(sw)
            logger.Error "error message"
            let output = sw.ToString()
            Assert.Contains("[ERROR]", output)
            Assert.Contains("error message", output)
        finally
            Console.SetOut(originalOut)

    [<Fact>]
    let ``ofConsole Debug should write to console`` () =
        let logger = TraceLogger.ofConsole ()
        use sw = new StringWriter()
        let originalOut = Console.Out

        try
            Console.SetOut(sw)
            logger.Debug "debug message"
            let output = sw.ToString()
            Assert.Contains("[DEBUG]", output)
            Assert.Contains("debug message", output)
        finally
            Console.SetOut(originalOut)

// ============================================================================
// ReadRetryPolicy Tests
// ============================================================================

module ReadRetryPolicyTests =

    [<Fact>]
    let ``defaultPolicy should return RetryAfter for DownloadPending`` () =
        let result = ReadRetryPolicy.defaultPolicy JvReadOutcome.DownloadPending

        match result with
        | RetryAfter delay -> Assert.Equal(TimeSpan.FromMilliseconds(500.), delay)
        | other -> failwithf "Expected RetryAfter, got %A" other

    [<Fact>]
    let ``defaultPolicy should return Skip for FileBoundary`` () =
        let result = ReadRetryPolicy.defaultPolicy JvReadOutcome.FileBoundary

        match result with
        | Skip -> ()
        | other -> failwithf "Expected Skip, got %A" other

    [<Fact>]
    let ``defaultPolicy should return Abort for EndOfStream`` () =
        let result = ReadRetryPolicy.defaultPolicy JvReadOutcome.EndOfStream

        match result with
        | Abort -> ()
        | other -> failwithf "Expected Abort, got %A" other

    [<Fact>]
    let ``defaultPolicy should return Abort for Payload`` () =
        let payload = { Timestamp = None; Data = [| 1uy |] }
        let result = ReadRetryPolicy.defaultPolicy (JvReadOutcome.Payload payload)

        match result with
        | Abort -> ()
        | other -> failwithf "Expected Abort, got %A" other

// ============================================================================
// JvLinkConfig Tests
// ============================================================================

module JvLinkConfigTests =

    [<Fact>]
    let ``create should succeed with valid SID`` () =
        match JvLinkConfig.create "test-sid" None None None with
        | Ok config ->
            Assert.Equal("test-sid", config.Sid)
            Assert.Equal(None, config.SavePath)
            Assert.Equal(None, config.ServiceKey)
        | Error err -> failwithf "Expected Ok, got %A" err

    [<Fact>]
    let ``create should trim SID`` () =
        match JvLinkConfig.create "  test-sid  " None None None with
        | Ok config -> Assert.Equal("test-sid", config.Sid)
        | Error err -> failwithf "Expected Ok, got %A" err

    [<Fact>]
    let ``create should accept SavePath`` () =
        match JvLinkConfig.create "test-sid" (Some "/path/to/save") None None with
        | Ok config -> Assert.Equal(Some "/path/to/save", config.SavePath)
        | Error err -> failwithf "Expected Ok, got %A" err

    [<Fact>]
    let ``create should accept ServiceKey`` () =
        match JvLinkConfig.create "test-sid" None (Some "service-key") None with
        | Ok config -> Assert.Equal(Some "service-key", config.ServiceKey)
        | Error err -> failwithf "Expected Ok, got %A" err

    [<Fact>]
    let ``create should accept all optional parameters`` () =
        match JvLinkConfig.create "test-sid" (Some "/save") (Some "key") None with
        | Ok config ->
            Assert.Equal("test-sid", config.Sid)
            Assert.Equal(Some "/save", config.SavePath)
            Assert.Equal(Some "key", config.ServiceKey)
        | Error err -> failwithf "Expected Ok, got %A" err

    [<Fact>]
    let ``create should fail for empty SID`` () =
        match JvLinkConfig.create "" None None None with
        | Error(ValidationError msg) -> Assert.Contains("non-empty", msg)
        | Error other -> failwithf "Expected ValidationError, got %A" other
        | Ok _ -> failwith "Should fail for empty SID"

    [<Fact>]
    let ``create should fail for whitespace SID`` () =
        match JvLinkConfig.create "   " None None None with
        | Error(ValidationError msg) -> Assert.Contains("non-empty", msg)
        | Error other -> failwithf "Expected ValidationError, got %A" other
        | Ok _ -> failwith "Should fail for whitespace SID"

    [<Fact>]
    let ``create should fail for null SID`` () =
        match JvLinkConfig.create null None None None with
        | Error(ValidationError msg) -> Assert.Contains("non-empty", msg)
        | Error other -> failwithf "Expected ValidationError, got %A" other
        | Ok _ -> failwith "Should fail for null SID"

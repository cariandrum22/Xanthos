module Xanthos.UnitTests.CoreModuleTests

open System
open Xunit
open Xanthos.Core
open Xanthos.Core.Records
open Xanthos.Core.Records.CodeTables

// ============================================================================
// MovieType Tests
// ============================================================================

module MovieTypeTests =

    [<Fact>]
    let ``toCode should return correct code for RaceVideo`` () =
        Assert.Equal("00", MovieType.toCode RaceVideo)

    [<Fact>]
    let ``toCode should return correct code for PaddockVideo`` () =
        Assert.Equal("01", MovieType.toCode PaddockVideo)

    [<Fact>]
    let ``toCode should return correct code for MultiCameraVideo`` () =
        Assert.Equal("02", MovieType.toCode MultiCameraVideo)

    [<Fact>]
    let ``toCode should return correct code for PatrolVideo`` () =
        Assert.Equal("03", MovieType.toCode PatrolVideo)

    [<Fact>]
    let ``toCode should return correct code for WorkoutWeekAll`` () =
        Assert.Equal("11", MovieType.toCode WorkoutWeekAll)

    [<Fact>]
    let ``toCode should return correct code for WorkoutWeekHorse`` () =
        Assert.Equal("12", MovieType.toCode WorkoutWeekHorse)

    [<Fact>]
    let ``toCode should return correct code for WorkoutHorseAll`` () =
        Assert.Equal("13", MovieType.toCode WorkoutHorseAll)

    [<Fact>]
    let ``toCode should return custom code for CustomMovieType`` () =
        Assert.Equal("99", MovieType.toCode (CustomMovieType "99"))

    [<Fact>]
    let ``fromCode should return RaceVideo for 00`` () =
        Assert.Equal(RaceVideo, MovieType.fromCode "00")

    [<Fact>]
    let ``fromCode should return PaddockVideo for 01`` () =
        Assert.Equal(PaddockVideo, MovieType.fromCode "01")

    [<Fact>]
    let ``fromCode should return MultiCameraVideo for 02`` () =
        Assert.Equal(MultiCameraVideo, MovieType.fromCode "02")

    [<Fact>]
    let ``fromCode should return PatrolVideo for 03`` () =
        Assert.Equal(PatrolVideo, MovieType.fromCode "03")

    [<Fact>]
    let ``fromCode should return WorkoutWeekAll for 11`` () =
        Assert.Equal(WorkoutWeekAll, MovieType.fromCode "11")

    [<Fact>]
    let ``fromCode should return WorkoutWeekHorse for 12`` () =
        Assert.Equal(WorkoutWeekHorse, MovieType.fromCode "12")

    [<Fact>]
    let ``fromCode should return WorkoutHorseAll for 13`` () =
        Assert.Equal(WorkoutHorseAll, MovieType.fromCode "13")

    [<Fact>]
    let ``fromCode should return CustomMovieType for unknown code`` () =
        match MovieType.fromCode "99" with
        | CustomMovieType code -> Assert.Equal("99", code)
        | other -> failwithf "Expected CustomMovieType, got %A" other

    [<Fact>]
    let ``isWorkoutType should return true for WorkoutWeekAll`` () =
        Assert.True(MovieType.isWorkoutType WorkoutWeekAll)

    [<Fact>]
    let ``isWorkoutType should return true for WorkoutWeekHorse`` () =
        Assert.True(MovieType.isWorkoutType WorkoutWeekHorse)

    [<Fact>]
    let ``isWorkoutType should return true for WorkoutHorseAll`` () =
        Assert.True(MovieType.isWorkoutType WorkoutHorseAll)

    [<Fact>]
    let ``isWorkoutType should return true for CustomMovieType starting with 1`` () =
        Assert.True(MovieType.isWorkoutType (CustomMovieType "14"))

    [<Fact>]
    let ``isWorkoutType should return false for RaceVideo`` () =
        Assert.False(MovieType.isWorkoutType RaceVideo)

    [<Fact>]
    let ``isWorkoutType should return false for PaddockVideo`` () =
        Assert.False(MovieType.isWorkoutType PaddockVideo)

    [<Fact>]
    let ``isWorkoutType should return false for CustomMovieType not starting with 1`` () =
        Assert.False(MovieType.isWorkoutType (CustomMovieType "99"))

// ============================================================================
// RunnerId Tests
// ============================================================================

module RunnerIdTests =
    // Valid 10-digit blood registration number for testing
    let validRunnerId = "1234567890"

    [<Fact>]
    let ``create should succeed with valid value`` () =
        match RunnerId.create validRunnerId with
        | Ok id -> Assert.Equal(validRunnerId, RunnerId.value id)
        | Error err -> failwithf "Expected Ok, got %A" err

    [<Fact>]
    let ``create should trim whitespace`` () =
        match RunnerId.create $"  {validRunnerId}  " with
        | Ok id -> Assert.Equal(validRunnerId, RunnerId.value id)
        | Error err -> failwithf "Expected Ok, got %A" err

    [<Fact>]
    let ``create should fail for empty string`` () =
        match RunnerId.create "" with
        | Error(ValidationError msg) -> Assert.Contains("empty", msg)
        | Error other -> failwithf "Expected ValidationError, got %A" other
        | Ok _ -> failwith "Should fail for empty string"

    [<Fact>]
    let ``create should fail for whitespace only`` () =
        match RunnerId.create "   " with
        | Error(ValidationError msg) -> Assert.Contains("empty", msg)
        | Error other -> failwithf "Expected ValidationError, got %A" other
        | Ok _ -> failwith "Should fail for whitespace only"

    [<Fact>]
    let ``create should fail for null`` () =
        match RunnerId.create null with
        | Error(ValidationError msg) -> Assert.Contains("empty", msg)
        | Error other -> failwithf "Expected ValidationError, got %A" other
        | Ok _ -> failwith "Should fail for null"

    [<Fact>]
    let ``create should fail for non-digit characters`` () =
        match RunnerId.create "123456789A" with
        | Error(ValidationError msg) -> Assert.Contains("digits", msg)
        | Error other -> failwithf "Expected ValidationError, got %A" other
        | Ok _ -> failwith "Should fail for non-digit characters"

    [<Fact>]
    let ``create should fail for wrong length`` () =
        match RunnerId.create "12345" with
        | Error(ValidationError msg) -> Assert.Contains("10 characters", msg)
        | Error other -> failwithf "Expected ValidationError, got %A" other
        | Ok _ -> failwith "Should fail for wrong length"

    [<Fact>]
    let ``unsafe should create RunnerId without validation`` () =
        let id = RunnerId.unsafe "test"
        Assert.Equal("test", RunnerId.value id)

    [<Fact>]
    let ``value should extract the inner string`` () =
        match RunnerId.create validRunnerId with
        | Ok id -> Assert.Equal(validRunnerId, RunnerId.value id)
        | Error _ -> failwith "Should succeed"

// ============================================================================
// RaceId Tests
// ============================================================================

module RaceIdTests =
    // Valid RaceId format: YYYYMMDD + optional suffix (e.g., YYYYMMDDJJKKHHRR)
    let validRaceId = "20240101" // Minimum: just the date
    let validFullRaceId = "2024010106010801" // Full format: date + venue + kai + hi + race

    [<Fact>]
    let ``create should succeed with valid date only`` () =
        match RaceId.create validRaceId with
        | Ok id -> Assert.Equal(validRaceId, RaceId.value id)
        | Error err -> failwithf "Expected Ok, got %A" err

    [<Fact>]
    let ``create should succeed with full race key`` () =
        match RaceId.create validFullRaceId with
        | Ok id -> Assert.Equal(validFullRaceId, RaceId.value id)
        | Error err -> failwithf "Expected Ok, got %A" err

    [<Fact>]
    let ``create should trim whitespace`` () =
        match RaceId.create $"  {validRaceId}  " with
        | Ok id -> Assert.Equal(validRaceId, RaceId.value id)
        | Error err -> failwithf "Expected Ok, got %A" err

    [<Fact>]
    let ``create should fail for empty string`` () =
        match RaceId.create "" with
        | Error(ValidationError msg) -> Assert.Contains("empty", msg)
        | Error other -> failwithf "Expected ValidationError, got %A" other
        | Ok _ -> failwith "Should fail for empty string"

    [<Fact>]
    let ``create should fail for whitespace only`` () =
        match RaceId.create "   " with
        | Error(ValidationError msg) -> Assert.Contains("empty", msg)
        | Error other -> failwithf "Expected ValidationError, got %A" other
        | Ok _ -> failwith "Should fail for whitespace only"

    [<Fact>]
    let ``create should fail for too short value`` () =
        match RaceId.create "2024010" with // Only 7 characters
        | Error(ValidationError msg) -> Assert.Contains("at least 8 characters", msg)
        | Error other -> failwithf "Expected ValidationError, got %A" other
        | Ok _ -> failwith "Should fail for too short value"

    [<Fact>]
    let ``create should fail for invalid date`` () =
        match RaceId.create "20241301" with // Invalid month 13
        | Error(ValidationError msg) -> Assert.Contains("valid date", msg)
        | Error other -> failwithf "Expected ValidationError, got %A" other
        | Ok _ -> failwith "Should fail for invalid date"

    [<Fact>]
    let ``create should fail for non-alphanumeric suffix`` () =
        match RaceId.create "20240101@@@" with
        | Error(ValidationError msg) -> Assert.Contains("alphanumeric", msg)
        | Error other -> failwithf "Expected ValidationError, got %A" other
        | Ok _ -> failwith "Should fail for non-alphanumeric suffix"

    [<Fact>]
    let ``unsafe should create RaceId without validation`` () =
        let id = RaceId.unsafe "test"
        Assert.Equal("test", RaceId.value id)

// ============================================================================
// WatchEvent Tests
// ============================================================================

module WatchEventTests =

    [<Fact>]
    let ``dataspecForEvent should return 0B12 for PayoffConfirmed`` () =
        Assert.Equal(Some "0B12", WatchEvent.dataspecForEvent PayoffConfirmed)

    [<Fact>]
    let ``dataspecForEvent should return 0B11 for HorseWeight`` () =
        Assert.Equal(Some "0B11", WatchEvent.dataspecForEvent HorseWeight)

    [<Fact>]
    let ``dataspecForEvent should return 0B16 for JockeyChange`` () =
        Assert.Equal(Some "0B16", WatchEvent.dataspecForEvent JockeyChange)

    [<Fact>]
    let ``dataspecForEvent should return 0B16 for WeatherChange`` () =
        Assert.Equal(Some "0B16", WatchEvent.dataspecForEvent WeatherChange)

    [<Fact>]
    let ``dataspecForEvent should return 0B16 for CourseChange`` () =
        Assert.Equal(Some "0B16", WatchEvent.dataspecForEvent CourseChange)

    [<Fact>]
    let ``dataspecForEvent should return 0B16 for AvoidedRace`` () =
        Assert.Equal(Some "0B16", WatchEvent.dataspecForEvent AvoidedRace)

    [<Fact>]
    let ``dataspecForEvent should return 0B16 for StartTimeChange`` () =
        Assert.Equal(Some "0B16", WatchEvent.dataspecForEvent StartTimeChange)

    [<Fact>]
    let ``dataspecForEvent should return None for UnknownEvent`` () =
        Assert.Equal(None, WatchEvent.dataspecForEvent (UnknownEvent "XYZ"))

    [<Fact>]
    let ``toRealtimeRequest should return request for known event`` () =
        let event =
            { Event = PayoffConfirmed
              RawKey = "testkey"
              Timestamp = None
              MeetingDate = None
              CourseCode = None
              RaceNumber = None
              RecordType = None
              ParticipantId = None
              AdditionalData = None }

        match WatchEvent.toRealtimeRequest event with
        | Some req ->
            Assert.Equal("0B12", req.Dataspec)
            Assert.Equal("testkey", req.Key)
        | None -> failwith "Should return Some"

    [<Fact>]
    let ``toRealtimeRequest should return None for UnknownEvent`` () =
        let event =
            { Event = UnknownEvent "XYZ"
              RawKey = "testkey"
              Timestamp = None
              MeetingDate = None
              CourseCode = None
              RaceNumber = None
              RecordType = None
              ParticipantId = None
              AdditionalData = None }

        Assert.Equal(None, WatchEvent.toRealtimeRequest event)

// ============================================================================
// WorkoutVideoListing Tests
// ============================================================================

module WorkoutVideoListingTests =

    [<Fact>]
    let ``parse should extract date from valid key`` () =
        let result = WorkoutVideoListing.parse "20240115REG123"
        Assert.Equal(Some(DateTime(2024, 1, 15)), result.WorkoutDate)
        Assert.Equal(Some "REG123", result.RegistrationId)

    [<Fact>]
    let ``parse should handle key with date only`` () =
        let result = WorkoutVideoListing.parse "20240115"
        Assert.Equal(Some(DateTime(2024, 1, 15)), result.WorkoutDate)
        Assert.Equal(None, result.RegistrationId)

    [<Fact>]
    let ``parse should trim trailing whitespace`` () =
        let result = WorkoutVideoListing.parse "20240115REG123   "
        Assert.Equal("20240115REG123", result.RawKey)

    [<Fact>]
    let ``parse should trim null characters`` () =
        let result = WorkoutVideoListing.parse "20240115\x00\x00"
        Assert.Equal("20240115", result.RawKey)

    [<Fact>]
    let ``parse should handle short key without date`` () =
        let result = WorkoutVideoListing.parse "short"
        Assert.Equal(None, result.WorkoutDate)
        Assert.Equal(None, result.RegistrationId)

    [<Fact>]
    let ``parse should handle empty registration id after date`` () =
        let result = WorkoutVideoListing.parse "20240115   "
        Assert.Equal(Some(DateTime(2024, 1, 15)), result.WorkoutDate)
        Assert.Equal(None, result.RegistrationId)

// ============================================================================
// CodeTables Tests
// ============================================================================

module CodeTablesTests =

    [<Fact>]
    let ``parseCode should parse valid SexCode`` () =
        let result = parseCode<SexCode> "1"
        Assert.Equal(Some SexCode.Male, result)

    [<Fact>]
    let ``parseCode should parse valid SexCode Female`` () =
        let result = parseCode<SexCode> "2"
        Assert.Equal(Some SexCode.Female, result)

    [<Fact>]
    let ``parseCode should parse valid SexCode Gelding`` () =
        let result = parseCode<SexCode> "3"
        Assert.Equal(Some SexCode.Gelding, result)

    [<Fact>]
    let ``parseCode should return None for invalid code`` () =
        let result = parseCode<SexCode> "99"
        Assert.Equal(None, result)

    [<Fact>]
    let ``parseCode should return None for non-numeric string`` () =
        let result = parseCode<SexCode> "abc"
        Assert.Equal(None, result)

    [<Fact>]
    let ``parseCode should return None for empty string`` () =
        let result = parseCode<SexCode> ""
        Assert.Equal(None, result)

    [<Fact>]
    let ``parseCodeOrDefault should return parsed value for valid code`` () =
        let result = parseCodeOrDefault<SexCode> "2" SexCode.Male
        Assert.Equal(SexCode.Female, result)

    [<Fact>]
    let ``parseCodeOrDefault should return default for invalid code`` () =
        let result = parseCodeOrDefault<SexCode> "99" SexCode.Male
        Assert.Equal(SexCode.Male, result)

    [<Fact>]
    let ``parseCodeOrDefault should return default for non-numeric`` () =
        let result = parseCodeOrDefault<SexCode> "abc" SexCode.Female
        Assert.Equal(SexCode.Female, result)

    [<Fact>]
    let ``getAllCodes should return all SexCode values`` () =
        let result = getAllCodes<SexCode> ()
        Assert.Equal(3, result.Length)
        Assert.Contains(SexCode.Male, result)
        Assert.Contains(SexCode.Female, result)
        Assert.Contains(SexCode.Gelding, result)

    [<Fact>]
    let ``getAllCodes should return all TrackConditionCode values`` () =
        let result = getAllCodes<TrackConditionCode> ()
        Assert.Equal(4, result.Length)
        Assert.Contains(TrackConditionCode.Good, result)
        Assert.Contains(TrackConditionCode.Yielding, result)
        Assert.Contains(TrackConditionCode.Soft, result)
        Assert.Contains(TrackConditionCode.Heavy, result)

    [<Fact>]
    let ``parseCode should parse HairColorCode`` () =
        let result = parseCode<HairColorCode> "3"
        Assert.Equal(Some HairColorCode.Bay, result)

    [<Fact>]
    let ``parseCode should parse RacecourseCode`` () =
        let result = parseCode<RacecourseCode> "5"
        Assert.Equal(Some RacecourseCode.Tokyo, result)

    [<Fact>]
    let ``parseCode should parse GradeCode`` () =
        let result = parseCode<GradeCode> "1"
        Assert.Equal(Some GradeCode.G1, result)

    [<Fact>]
    let ``parseCode should parse RunningStyleCode`` () =
        let result = parseCode<RunningStyleCode> "4"
        Assert.Equal(Some RunningStyleCode.Pursuing, result)

// ============================================================================
// RecordParser Additional Tests
// ============================================================================

module RecordParserTests =

    open RecordParser

    [<Fact>]
    let ``toXanthosError should convert RecordTooShort`` () =
        let error = RecordTooShort(100, 50)

        match toXanthosError error with
        | ValidationError msg ->
            Assert.Contains("too short", msg)
            Assert.Contains("100", msg)
            Assert.Contains("50", msg)
        | other -> failwithf "Expected ValidationError, got %A" other

    [<Fact>]
    let ``toXanthosError should convert FieldExtractionFailed`` () =
        let error = FieldExtractionFailed("TestField", "Some reason")

        match toXanthosError error with
        | ValidationError msg ->
            Assert.Contains("TestField", msg)
            Assert.Contains("Some reason", msg)
        | other -> failwithf "Expected ValidationError, got %A" other

    [<Fact>]
    let ``toXanthosError should convert InvalidFieldValue`` () =
        let error = InvalidFieldValue("TestField", "badvalue", "invalid format")

        match toXanthosError error with
        | ValidationError msg ->
            Assert.Contains("TestField", msg)
            Assert.Contains("badvalue", msg)
            Assert.Contains("invalid format", msg)
        | other -> failwithf "Expected ValidationError, got %A" other

    [<Fact>]
    let ``toXanthosError should convert UnknownRecordType`` () =
        let error = UnknownRecordType "XX"

        match toXanthosError error with
        | ValidationError msg ->
            Assert.Contains("Unknown record type", msg)
            Assert.Contains("XX", msg)
        | other -> failwithf "Expected ValidationError, got %A" other

    [<Fact>]
    let ``toXanthosError should convert CodeTableLookupFailed`` () =
        let error = CodeTableLookupFailed("SexCode", "99")

        match toXanthosError error with
        | ValidationError msg ->
            Assert.Contains("SexCode", msg)
            Assert.Contains("99", msg)
        | other -> failwithf "Expected ValidationError, got %A" other

    [<Fact>]
    let ``extractBytes should succeed for valid range`` () =
        let data = [| 1uy; 2uy; 3uy; 4uy; 5uy |]

        match extractBytes data 1 3 with
        | Ok bytes -> Assert.Equal<byte[]>([| 2uy; 3uy; 4uy |], bytes)
        | Error err -> failwithf "Expected Ok, got %A" err

    [<Fact>]
    let ``extractBytes should fail when range exceeds data length`` () =
        let data = [| 1uy; 2uy; 3uy |]

        match extractBytes data 2 5 with
        | Error(RecordTooShort(expected, actual)) ->
            Assert.Equal(7, expected)
            Assert.Equal(3, actual)
        | Error other -> failwithf "Expected RecordTooShort, got %A" other
        | Ok _ -> failwith "Should fail"

    [<Fact>]
    let ``getRecordType should return first 2 bytes`` () =
        let data = Text.Encoding.GetEncoding(932).GetBytes("RAtest")
        let result = getRecordType data
        Assert.Equal("RA", result)

    [<Fact>]
    let ``getRecordType should return empty for short data`` () =
        let data = [| 65uy |] // Single byte
        let result = getRecordType data
        Assert.Equal("", result)

    [<Fact>]
    let ``parseInt should parse valid integer`` () =
        let bytes = Text.Encoding.GetEncoding(932).GetBytes("  123  ")
        let result = parseInt bytes
        Assert.Equal(Some 123, result)

    [<Fact>]
    let ``parseInt should return None for empty`` () =
        let bytes = Text.Encoding.GetEncoding(932).GetBytes("     ")
        let result = parseInt bytes
        Assert.Equal(None, result)

    [<Fact>]
    let ``parseInt should return None for non-numeric`` () =
        let bytes = Text.Encoding.GetEncoding(932).GetBytes("abc")
        let result = parseInt bytes
        Assert.Equal(None, result)

    [<Fact>]
    let ``parseDecimal should parse with precision`` () =
        let bytes = Text.Encoding.GetEncoding(932).GetBytes("12345")
        let result = parseDecimal bytes 2
        Assert.Equal(Some 123.45M, result)

    [<Fact>]
    let ``parseDecimal should return None for empty`` () =
        let bytes = Text.Encoding.GetEncoding(932).GetBytes("     ")
        let result = parseDecimal bytes 2
        Assert.Equal(None, result)

    [<Fact>]
    let ``parseFlag should return true for 1`` () =
        let bytes = Text.Encoding.GetEncoding(932).GetBytes("1")
        let result = parseFlag bytes
        Assert.True(result)

    [<Fact>]
    let ``parseFlag should return false for 0`` () =
        let bytes = Text.Encoding.GetEncoding(932).GetBytes("0")
        let result = parseFlag bytes
        Assert.False(result)

    [<Fact>]
    let ``parseFlag should return false for other values`` () =
        let bytes = Text.Encoding.GetEncoding(932).GetBytes("X")
        let result = parseFlag bytes
        Assert.False(result)

    [<Fact>]
    let ``parseDate should parse valid date`` () =
        let bytes = Text.Encoding.GetEncoding(932).GetBytes("20240115")
        let result = parseDate bytes "yyyyMMdd"
        Assert.Equal(Some(DateTime(2024, 1, 15)), result)

    [<Fact>]
    let ``parseDate should return None for all zeros`` () =
        let bytes = Text.Encoding.GetEncoding(932).GetBytes("00000000")
        let result = parseDate bytes "yyyyMMdd"
        Assert.Equal(None, result)

    [<Fact>]
    let ``parseDate should return None for empty`` () =
        let bytes = Text.Encoding.GetEncoding(932).GetBytes("        ")
        let result = parseDate bytes "yyyyMMdd"
        Assert.Equal(None, result)

    [<Fact>]
    let ``getText should return Some for valid text field`` () =
        let fields = Map.ofList [ "Name", TextValue "TestValue" ]
        let result = getText fields "Name"
        Assert.Equal(Some "TestValue", result)

    [<Fact>]
    let ``getText should return None for missing field`` () =
        let fields = Map.empty
        let result = getText fields "Name"
        Assert.Equal(None, result)

    [<Fact>]
    let ``getText should return None for empty text`` () =
        let fields = Map.ofList [ "Name", TextValue "   " ]
        let result = getText fields "Name"
        Assert.Equal(None, result)

    [<Fact>]
    let ``getRequiredText should succeed for valid field`` () =
        let fields = Map.ofList [ "Name", TextValue "TestValue" ]

        match getRequiredText fields "Name" with
        | Ok value -> Assert.Equal("TestValue", value)
        | Error err -> failwithf "Expected Ok, got %A" err

    [<Fact>]
    let ``getRequiredText should fail for missing field`` () =
        let fields = Map.empty

        match getRequiredText fields "Name" with
        | Error(InvalidFieldValue _) -> ()
        | Error other -> failwithf "Expected InvalidFieldValue, got %A" other
        | Ok _ -> failwith "Should fail"

    [<Fact>]
    let ``getInt should return value for IntValue field`` () =
        let fields = Map.ofList [ "Count", IntValue(Some 42) ]
        let result = getInt fields "Count"
        Assert.Equal(Some 42, result)

    [<Fact>]
    let ``getInt should return None for missing field`` () =
        let fields = Map.empty
        let result = getInt fields "Count"
        Assert.Equal(None, result)

    [<Fact>]
    let ``getDecimal should return value for DecimalValue field`` () =
        let fields = Map.ofList [ "Price", DecimalValue(Some 123.45M) ]
        let result = getDecimal fields "Price"
        Assert.Equal(Some 123.45M, result)

    [<Fact>]
    let ``getDate should return value for DateValue field`` () =
        let date = DateTime(2024, 1, 15)
        let fields = Map.ofList [ "Date", DateValue(Some date) ]
        let result = getDate fields "Date"
        Assert.Equal(Some date, result)

    [<Fact>]
    let ``getBool should return value for BoolValue field`` () =
        let fields = Map.ofList [ "Active", BoolValue true ]
        let result = getBool fields "Active"
        Assert.True(result)

    [<Fact>]
    let ``getBool should return false for missing field`` () =
        let fields = Map.empty
        let result = getBool fields "Active"
        Assert.False(result)

    [<Fact>]
    let ``getBytes should return value for BytesValue field`` () =
        let bytes = [| 1uy; 2uy; 3uy |]
        let fields = Map.ofList [ "Data", BytesValue bytes ]
        let result = getBytes fields "Data"
        Assert.Equal(Some bytes, result)

    [<Fact>]
    let ``getBytes should return None for missing field`` () =
        let fields = Map.empty
        let result = getBytes fields "Data"
        Assert.Equal(None, result)

    [<Fact>]
    let ``getCode should parse CodeValue to enum`` () =
        let fields = Map.ofList [ "Sex", CodeValue "1" ]
        let result = getCode<SexCode> fields "Sex"
        Assert.Equal(Some SexCode.Male, result)

    [<Fact>]
    let ``getCode should return None for invalid code`` () =
        let fields = Map.ofList [ "Sex", CodeValue "99" ]
        let result = getCode<SexCode> fields "Sex"
        Assert.Equal(None, result)

    [<Fact>]
    let ``getCode should return None for missing field`` () =
        let fields = Map.empty
        let result = getCode<SexCode> fields "Sex"
        Assert.Equal(None, result)

// ============================================================================
// Errors Module Tests
// ============================================================================

module ErrorsModuleTests =

    open Xanthos.Core.Errors

    [<Fact>]
    let ``mapComError should map Ok value`` () =
        let result: Result<int, ComError> = Ok 42

        match mapComError result with
        | Ok value -> Assert.Equal(42, value)
        | Error _ -> failwith "Should be Ok"

    [<Fact>]
    let ``mapComError should map Error to InteropError`` () =
        let result: Result<int, ComError> = Error NotInitialized

        match mapComError result with
        | Error(InteropError NotInitialized) -> ()
        | Error other -> failwithf "Expected InteropError NotInitialized, got %A" other
        | Ok _ -> failwith "Should be Error"

    [<Fact>]
    let ``mapComError should preserve InvalidInput error`` () =
        let result: Result<int, ComError> = Error(InvalidInput "test")

        match mapComError result with
        | Error(InteropError(InvalidInput msg)) -> Assert.Equal("test", msg)
        | Error other -> failwithf "Expected InteropError InvalidInput, got %A" other
        | Ok _ -> failwith "Should be Error"

    [<Fact>]
    let ``validation should create ValidationError`` () =
        let error = validation "test message"

        match error with
        | ValidationError msg -> Assert.Equal("test message", msg)
        | other -> failwithf "Expected ValidationError, got %A" other

[<Fact>]
let ``UnexpectedError should create UnexpectedError`` () =
    let error = UnexpectedError "something wrong"

    match error with
    | UnexpectedError msg -> Assert.Equal("something wrong", msg)
    | other -> failwithf "Expected UnexpectedError, got %A" other

// ============================================================================
// Text Module Tests
// ============================================================================

module TextModuleTests =

    open Xanthos.Core.Text

    [<Fact>]
    let ``decodeShiftJis should decode valid Shift-JIS bytes`` () =
        let bytes = System.Text.Encoding.GetEncoding(932).GetBytes("テスト")
        let result = decodeShiftJis bytes
        Assert.Equal("テスト", result)

    [<Fact>]
    let ``decodeShiftJis should return empty for null`` () =
        let result = decodeShiftJis null
        Assert.Equal("", result)

    [<Fact>]
    let ``decodeShiftJis should return empty for empty array`` () =
        let result = decodeShiftJis [||]
        Assert.Equal("", result)

    [<Fact>]
    let ``decodeShiftJis should trim null terminators`` () =
        let text = "test\u0000\u0000"
        let bytes = System.Text.Encoding.GetEncoding(932).GetBytes(text)
        let result = decodeShiftJis bytes
        Assert.Equal("test", result)

    [<Fact>]
    let ``decodeShiftJis should fallback to UTF-8 for invalid Shift-JIS`` () =
        // UTF-8 encoded string that's not valid Shift-JIS
        let bytes = System.Text.Encoding.UTF8.GetBytes("日本語テスト")
        let result = decodeShiftJis bytes
        // Should not throw and return something
        Assert.NotNull(result)

    [<Fact>]
    let ``encodeShiftJis should encode valid text`` () =
        let result = encodeShiftJis "テスト"
        let decoded = System.Text.Encoding.GetEncoding(932).GetString(result)
        Assert.Equal("テスト", decoded)

    [<Fact>]
    let ``encodeShiftJis should return empty for null`` () =
        let result = encodeShiftJis null
        Assert.Empty(result)

    [<Fact>]
    let ``encodeShiftJis should return empty for empty string`` () =
        let result = encodeShiftJis ""
        Assert.Empty(result)

    [<Fact>]
    let ``normalizeJvText should return same for null`` () =
        let result = normalizeJvText null
        Assert.Null(result)

    [<Fact>]
    let ``normalizeJvText should return same for empty`` () =
        let result = normalizeJvText ""
        Assert.Equal("", result)

    [<Fact>]
    let ``normalizeJvText should convert fullwidth digits to ASCII`` () =
        // FF10-FF19 are fullwidth digits 0-9
        let fullwidthDigits = "０１２３４５６７８９"
        let result = normalizeJvText fullwidthDigits
        Assert.Equal("0123456789", result)

    [<Fact>]
    let ``normalizeJvText should convert fullwidth uppercase to ASCII`` () =
        // FF21-FF3A are fullwidth A-Z
        let fullwidthUpper = "ＡＢＣＸＹＺ"
        let result = normalizeJvText fullwidthUpper
        Assert.Equal("ABCXYZ", result)

    [<Fact>]
    let ``normalizeJvText should convert fullwidth lowercase to ASCII`` () =
        // FF41-FF5A are fullwidth a-z
        let fullwidthLower = "ａｂｃｘｙｚ"
        let result = normalizeJvText fullwidthLower
        Assert.Equal("abcxyz", result)

    [<Fact>]
    let ``normalizeJvText should preserve regular text`` () =
        let result = normalizeJvText "ABC123テスト"
        Assert.Equal("ABC123テスト", result)

    [<Fact>]
    let ``normalizeJvText should handle mixed content`` () =
        let mixed = "テスト０１２ＡＢＣ"
        let result = normalizeJvText mixed
        Assert.Equal("テスト012ABC", result)

// ============================================================================
// FieldDefinitions Tests
// ============================================================================

module FieldDefinitionsTests =

    open Xanthos.Core.Records.FieldDefinitions

    [<Fact>]
    let ``text should create Text field spec`` () =
        let spec = text "Name" 10 20
        Assert.Equal("Name", spec.Name)
        Assert.Equal(10, spec.ByteOffset)
        Assert.Equal(20, spec.ByteLength)
        Assert.Equal(Text, spec.Encoding)

    [<Fact>]
    let ``textRaw should create TextRaw field spec`` () =
        let spec = textRaw "RawName" 5 15
        Assert.Equal("RawName", spec.Name)
        Assert.Equal(5, spec.ByteOffset)
        Assert.Equal(15, spec.ByteLength)
        Assert.Equal(TextRaw, spec.Encoding)

    [<Fact>]
    let ``int should create Integer field spec`` () =
        let spec = int "Count" 0 4
        Assert.Equal("Count", spec.Name)
        Assert.Equal(0, spec.ByteOffset)
        Assert.Equal(4, spec.ByteLength)
        Assert.Equal(Integer, spec.Encoding)

    [<Fact>]
    let ``decimal should create Decimal field spec`` () =
        let spec = decimal "Price" 10 8 2
        Assert.Equal("Price", spec.Name)
        Assert.Equal(10, spec.ByteOffset)
        Assert.Equal(8, spec.ByteLength)

        match spec.Encoding with
        | Decimal precision -> Assert.Equal(2, precision)
        | other -> failwithf "Expected Decimal, got %A" other

    [<Fact>]
    let ``date should create Date field spec`` () =
        let spec = date "Birthday" 20 8 "yyyyMMdd"
        Assert.Equal("Birthday", spec.Name)
        Assert.Equal(20, spec.ByteOffset)
        Assert.Equal(8, spec.ByteLength)

        match spec.Encoding with
        | Date format -> Assert.Equal("yyyyMMdd", format)
        | other -> failwithf "Expected Date, got %A" other

    [<Fact>]
    let ``code should create Code field spec`` () =
        let spec = code "Sex" 30 1 "SexCode"
        Assert.Equal("Sex", spec.Name)
        Assert.Equal(30, spec.ByteOffset)
        Assert.Equal(1, spec.ByteLength)

        match spec.Encoding with
        | Code table -> Assert.Equal("SexCode", table)
        | other -> failwithf "Expected Code, got %A" other

    [<Fact>]
    let ``flag should create Flag field spec`` () =
        let spec = flag "Active" 40 1
        Assert.Equal("Active", spec.Name)
        Assert.Equal(40, spec.ByteOffset)
        Assert.Equal(1, spec.ByteLength)
        Assert.Equal(Flag, spec.Encoding)

    [<Fact>]
    let ``bytes should create Bytes field spec`` () =
        let spec = bytes "RawData" 50 100
        Assert.Equal("RawData", spec.Name)
        Assert.Equal(50, spec.ByteOffset)
        Assert.Equal(100, spec.ByteLength)
        Assert.Equal(Bytes, spec.Encoding)

// ============================================================================
// Record Parsing Additional Tests (for branch coverage)
// ============================================================================

module RecordParsingBranchTests =

    open Xanthos.Core.Records.FieldDefinitions
    open Xanthos.Core.Records.RecordParser

    // Register the encoding provider to support Shift-JIS (codepage 932)
    do System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance)

    let private shiftJisEnc = System.Text.Encoding.GetEncoding(932)

    [<Fact>]
    let ``parseField should handle Text encoding`` () =
        let data = shiftJisEnc.GetBytes("  テスト  ")
        let spec = text "Test" 0 (data.Length)

        match parseField data spec with
        | Ok(TextValue value) -> Assert.Equal("テスト", value.Trim())
        | Ok other -> failwithf "Expected TextValue, got %A" other
        | Error err -> failwithf "Expected Ok, got %A" err

    [<Fact>]
    let ``parseField should handle TextRaw encoding`` () =
        let data = shiftJisEnc.GetBytes("  raw text  ")
        let spec = textRaw "Test" 0 (data.Length)

        match parseField data spec with
        | Ok(TextValue value) -> Assert.Equal("raw text", value)
        | Ok other -> failwithf "Expected TextValue, got %A" other
        | Error err -> failwithf "Expected Ok, got %A" err

    [<Fact>]
    let ``parseField should handle Integer encoding`` () =
        let data = shiftJisEnc.GetBytes("  42  ")
        let spec = int "Test" 0 (data.Length)

        match parseField data spec with
        | Ok(IntValue(Some 42)) -> ()
        | Ok other -> failwithf "Expected IntValue Some 42, got %A" other
        | Error err -> failwithf "Expected Ok, got %A" err

    [<Fact>]
    let ``parseField should handle Decimal encoding`` () =
        let data = shiftJisEnc.GetBytes("12345")
        let spec = decimal "Test" 0 (data.Length) 2

        match parseField data spec with
        | Ok(DecimalValue(Some 123.45M)) -> ()
        | Ok other -> failwithf "Expected DecimalValue Some 123.45, got %A" other
        | Error err -> failwithf "Expected Ok, got %A" err

    [<Fact>]
    let ``parseField should handle Date encoding`` () =
        let data = shiftJisEnc.GetBytes("20240115")
        let spec = date "Test" 0 (data.Length) "yyyyMMdd"

        match parseField data spec with
        | Ok(DateValue(Some date)) ->
            Assert.Equal(2024, date.Year)
            Assert.Equal(1, date.Month)
            Assert.Equal(15, date.Day)
        | Ok other -> failwithf "Expected DateValue Some, got %A" other
        | Error err -> failwithf "Expected Ok, got %A" err

    [<Fact>]
    let ``parseField should handle Flag encoding`` () =
        let data = shiftJisEnc.GetBytes("1")
        let spec = flag "Test" 0 1

        match parseField data spec with
        | Ok(BoolValue true) -> ()
        | Ok other -> failwithf "Expected BoolValue true, got %A" other
        | Error err -> failwithf "Expected Ok, got %A" err

    [<Fact>]
    let ``parseField should handle Bytes encoding`` () =
        let data = [| 1uy; 2uy; 3uy |]
        let spec = bytes "Test" 0 3

        match parseField data spec with
        | Ok(BytesValue bytes) -> Assert.Equal<byte[]>([| 1uy; 2uy; 3uy |], bytes)
        | Ok other -> failwithf "Expected BytesValue, got %A" other
        | Error err -> failwithf "Expected Ok, got %A" err

    [<Fact>]
    let ``parseField should handle Code encoding`` () =
        let data = shiftJisEnc.GetBytes("1")
        let spec = code "Test" 0 1 "SexCode"

        match parseField data spec with
        | Ok(CodeValue "1") -> ()
        | Ok other -> failwithf "Expected CodeValue 1, got %A" other
        | Error err -> failwithf "Expected Ok, got %A" err

    [<Fact>]
    let ``parseFields should parse multiple fields`` () =
        // Build test data: RecordType (2) + Name (10) + Count (4)
        let recordType = shiftJisEnc.GetBytes("RA")
        let name = shiftJisEnc.GetBytes("TestName  ")
        let count = shiftJisEnc.GetBytes("0042")
        let data = Array.concat [ recordType; name; count ]

        let specs = [ textRaw "RecordType" 0 2; text "Name" 2 10; int "Count" 12 4 ]

        match parseFields data specs with
        | Ok fields ->
            Assert.Equal(3, fields.Count)

            match getText fields "Name" with
            | Some name -> Assert.Equal("TestName", name)
            | None -> failwith "Name should exist"

            match getInt fields "Count" with
            | Some count -> Assert.Equal(42, count)
            | None -> failwith "Count should exist"
        | Error err -> failwithf "Expected Ok, got %A" err

    [<Fact>]
    let ``parseFields should fail on first error`` () =
        let data = [| 1uy; 2uy |] // Too short

        let specs = [ text "Field1" 0 10 ] // Requires 10 bytes

        match parseFields data specs with
        | Error(RecordTooShort _) -> ()
        | Error other -> failwithf "Expected RecordTooShort, got %A" other
        | Ok _ -> failwith "Should fail"

    [<Fact>]
    let ``parseDecimal should return None for non-numeric`` () =
        let bytes = shiftJisEnc.GetBytes("abc")
        let result = parseDecimal bytes 2
        Assert.Equal(None, result)

    [<Fact>]
    let ``parseDate should return None for invalid date`` () =
        let bytes = shiftJisEnc.GetBytes("99999999")
        let result = parseDate bytes "yyyyMMdd"
        Assert.Equal(None, result)

    [<Fact>]
    let ``getText should return None for non-TextValue`` () =
        let fields = Map.ofList [ "Count", IntValue(Some 42) ]
        let result = getText fields "Count"
        Assert.Equal(None, result)

    [<Fact>]
    let ``getInt should return None for non-IntValue`` () =
        let fields = Map.ofList [ "Name", TextValue "test" ]
        let result = getInt fields "Name"
        Assert.Equal(None, result)

    [<Fact>]
    let ``getDecimal should return None for non-DecimalValue`` () =
        let fields = Map.ofList [ "Name", TextValue "test" ]
        let result = getDecimal fields "Name"
        Assert.Equal(None, result)

    [<Fact>]
    let ``getDate should return None for non-DateValue`` () =
        let fields = Map.ofList [ "Name", TextValue "test" ]
        let result = getDate fields "Name"
        Assert.Equal(None, result)

    [<Fact>]
    let ``getBool should return false for non-BoolValue`` () =
        let fields = Map.ofList [ "Name", TextValue "test" ]
        let result = getBool fields "Name"
        Assert.False(result)

    [<Fact>]
    let ``getBytes should return None for non-BytesValue`` () =
        let fields = Map.ofList [ "Name", TextValue "test" ]
        let result = getBytes fields "Name"
        Assert.Equal(None, result)

    [<Fact>]
    let ``getCode should return None for non-CodeValue`` () =
        let fields = Map.ofList [ "Name", TextValue "test" ]
        let result = getCode<CodeTables.SexCode> fields "Name"
        Assert.Equal(None, result)

// ============================================================================
// Text Module Tests - Branch Coverage Improvement
// ============================================================================

module TextBranchCoverageTests =
    open Xanthos.Core.Text

    [<Fact>]
    let ``decodeShiftJis with null returns empty string`` () =
        let result = decodeShiftJis null
        Assert.Equal(String.Empty, result)

    [<Fact>]
    let ``decodeShiftJis with empty array returns empty string`` () =
        let result = decodeShiftJis [||]
        Assert.Equal(String.Empty, result)

    [<Fact>]
    let ``decodeShiftJis with valid Shift-JIS returns correct string`` () =
        let bytes = encodeShiftJis "テスト"
        let result = decodeShiftJis bytes
        Assert.Equal("テスト", result)

    [<Fact>]
    let ``decodeShiftJis trims null terminators`` () =
        let bytes = Array.concat [| encodeShiftJis "テスト"; [| 0uy; 0uy |] |]
        let result = decodeShiftJis bytes
        Assert.Equal("テスト", result)

    [<Fact>]
    let ``decodeShiftJis with valid UTF-8 fallback`` () =
        // ASCII is valid in both Shift-JIS and UTF-8
        let utf8Bytes = System.Text.Encoding.UTF8.GetBytes("Hello")
        let result = decodeShiftJis utf8Bytes
        Assert.Equal("Hello", result)

    [<Fact>]
    let ``decodeShiftJis with invalid Shift-JIS byte sequence`` () =
        // 0x80 alone is invalid in Shift-JIS (needs to be part of a multi-byte sequence)
        // But 0x80-0xBF are also invalid in strict UTF-8, so falls back to lenient UTF-8
        let invalidShiftJisBytes = [| 0x80uy |]
        // Should not throw - uses fallback decoding
        let result = decodeShiftJis invalidShiftJisBytes
        Assert.NotNull(result)

    [<Fact>]
    let ``decodeShiftJis with completely invalid bytes uses default fallback`` () =
        // Create bytes that are invalid in both Shift-JIS and strict UTF-8
        // 0xFE, 0xFF are never valid in UTF-8
        let invalidBytes = [| 0xFEuy; 0xFFuy |]
        // Should not throw, should return something (using lenient UTF-8 fallback)
        let result = decodeShiftJis invalidBytes
        Assert.NotNull(result)

    [<Fact>]
    let ``encodeShiftJis with null returns empty array`` () =
        let result = encodeShiftJis null
        Assert.Equal(0, result.Length)

    [<Fact>]
    let ``encodeShiftJis with empty string returns empty array`` () =
        let result = encodeShiftJis ""
        Assert.Equal(0, result.Length)

    [<Fact>]
    let ``encodeShiftJis with valid string returns correct bytes`` () =
        let result = encodeShiftJis "ABC"
        Assert.Equal(3, result.Length)
        Assert.Equal(65uy, result.[0])
        Assert.Equal(66uy, result.[1])
        Assert.Equal(67uy, result.[2])

    [<Fact>]
    let ``normalizeJvText with null returns null`` () =
        let result = normalizeJvText null
        Assert.Null(result)

    [<Fact>]
    let ``normalizeJvText with empty string returns empty`` () =
        let result = normalizeJvText ""
        Assert.Equal("", result)

    [<Fact>]
    let ``normalizeJvText converts fullwidth digits to ASCII`` () =
        // Fullwidth digits: ０１２３４５６７８９
        let fullwidthDigits = "\uFF10\uFF11\uFF12\uFF13\uFF14\uFF15\uFF16\uFF17\uFF18\uFF19"
        let result = normalizeJvText fullwidthDigits
        Assert.Equal("0123456789", result)

    [<Fact>]
    let ``normalizeJvText converts fullwidth uppercase to ASCII`` () =
        // Fullwidth uppercase: Ａ～Ｚ
        let fullwidthUpper = "\uFF21\uFF22\uFF23\uFF3A" // A, B, C, Z
        let result = normalizeJvText fullwidthUpper
        Assert.Equal("ABCZ", result)

    [<Fact>]
    let ``normalizeJvText converts fullwidth lowercase to ASCII`` () =
        // Fullwidth lowercase: ａ～ｚ
        let fullwidthLower = "\uFF41\uFF42\uFF43\uFF5A" // a, b, c, z
        let result = normalizeJvText fullwidthLower
        Assert.Equal("abcz", result)

    [<Fact>]
    let ``normalizeJvText preserves regular characters`` () =
        let result = normalizeJvText "テスト123ABC"
        Assert.Equal("テスト123ABC", result)

    [<Fact>]
    let ``normalizeJvText handles mixed fullwidth and regular`` () =
        // Mix of fullwidth digits, uppercase, lowercase, and regular chars
        let mixed = "\uFF10\uFF21\uFF41テスト" // 0, A, a, テスト
        let result = normalizeJvText mixed
        Assert.Equal("0Aaテスト", result)

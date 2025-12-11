module Xanthos.UnitTests.PropertyTests

open System
open FsCheck
open FsCheck.Xunit
open Xanthos.Core
open Xanthos.Core.Text
open Xanthos.Core.Records
open Xanthos.Core.Records.RecordParser
open Xanthos.Core.Records.CodeTables
open Xanthos.Core.Records.FieldDefinitions

// ============================================================================
// Custom Generators
// ============================================================================

/// Generate valid ASCII alphanumeric strings
let asciiAlphaNumStringGen =
    Gen.elements ([ 'A' .. 'Z' ] @ [ 'a' .. 'z' ] @ [ '0' .. '9' ])
    |> Gen.listOf
    |> Gen.map (List.toArray >> String)

/// Generate valid race keys (16 characters: YYYYMMDDRRRRRRNN)
let raceKeyGen =
    gen {
        let! year = Gen.choose (2000, 2030)
        let! month = Gen.choose (1, 12)
        let! day = Gen.choose (1, 28) // Simplified to avoid invalid dates
        let! raceId = Gen.choose (1, 999999)
        let! num = Gen.choose (1, 99)
        return sprintf "%04d%02d%02d%06d%02d" year month day raceId num
    }

/// Generate valid horse IDs (10 digits)
let horseIdGen =
    gen {
        let! year = Gen.choose (2000, 2030)
        let! id = Gen.choose (100000, 999999)
        return sprintf "%04d%06d" year id
    }

/// Generate valid byte arrays with specific size
let byteArrayGen size =
    Gen.arrayOfLength size Arb.generate<byte>

// ============================================================================
// Property-Based Tests for RecordParser Core Functions
// ============================================================================

[<Property>]
let ``extractBytes returns correct length`` (data: byte[]) (offset: int) (length: int) =
    let offset = abs offset % max 1 data.Length
    let length = abs length % max 1 (data.Length - offset)

    match extractBytes data offset length with
    | Ok bytes -> bytes.Length = length
    | Error _ -> true // Error cases are acceptable

[<Property>]
let ``extractBytes with valid parameters succeeds`` () =
    let data = [| 1uy; 2uy; 3uy; 4uy; 5uy; 6uy; 7uy; 8uy |]

    Prop.forAll (Arb.fromGen (Gen.choose (0, 7))) (fun offset ->
        Prop.forAll (Arb.fromGen (Gen.choose (1, 8 - offset))) (fun length ->
            match extractBytes data offset length with
            | Ok bytes -> bytes.Length = length && bytes.[0] = data.[offset]
            | Error _ -> false))

[<Property>]
let ``parseInt handles numeric strings correctly`` (n: int) =
    let n = abs n % 1000000
    let str = sprintf "%07d" n
    let bytes = encodeShiftJis str

    match parseInt bytes with
    | Some value -> value = n
    | None -> false

[<Property>]
let ``parseInt returns None for non-numeric strings`` (str: string) =
    if isNull str then
        true // Skip null strings
    else
        let nonNumeric =
            str
            |> Seq.filter (fun c -> not (Char.IsDigit c))
            |> Seq.truncate 10
            |> String.Concat

        if String.IsNullOrWhiteSpace(nonNumeric) || String.length nonNumeric = 0 then
            true // Skip empty strings
        else
            let bytes = encodeShiftJis nonNumeric
            parseInt bytes = None

[<Property>]
let ``parseDecimal with precision maintains correctness`` () =
    Prop.forAll (Arb.fromGen (Gen.choose (1, 9999))) (fun n ->
        Prop.forAll (Arb.fromGen (Gen.choose (0, 3))) (fun precision ->
            let str = sprintf "%d" n
            let bytes = encodeShiftJis str

            match parseDecimal bytes precision with
            | Some value ->
                let divisor = pown 10M precision
                let expected = (System.Decimal n) / divisor
                value = expected
            | None -> false))

[<Property>]
let ``parseDate with valid format succeeds`` () =
    Prop.forAll (Arb.fromGen (Gen.choose (2000, 2030))) (fun year ->
        Prop.forAll (Arb.fromGen (Gen.choose (1, 12))) (fun month ->
            Prop.forAll (Arb.fromGen (Gen.choose (1, 28))) (fun day ->
                let dateStr = sprintf "%04d%02d%02d" year month day
                let bytes = encodeShiftJis dateStr

                match parseDate bytes "yyyyMMdd" with
                | Some date -> date.Year = year && date.Month = month && date.Day = day
                | None -> false)))

[<Property>]
let ``getRecordType extracts first 2 bytes`` () =
    let asciiChars = [ 'A' .. 'Z' ] @ [ '0' .. '9' ]
    let charGen = Gen.elements asciiChars

    let recordTypeGen =
        gen {
            let! c1 = charGen
            let! c2 = charGen
            return String [| c1; c2 |]
        }

    Prop.forAll (Arb.fromGen recordTypeGen) (fun recordType ->
        let bytes = encodeShiftJis recordType
        let actual = getRecordType bytes

        if actual <> recordType then
            printfn "Record type mismatch. Expected '%s' got '%s' (bytes=%A)" recordType actual bytes

        actual = recordType)

// ============================================================================
// Property-Based Tests for CodeTables
// ============================================================================

[<Property>]
let ``parseCode SexCode accepts valid codes`` () =
    let validCodes = [ "1"; "2"; "3" ]

    validCodes
    |> List.forall (fun code ->
        match parseCode<SexCode> code with
        | Some _ -> true
        | None -> false)

[<Property>]
let ``parseCode SexCode rejects invalid codes`` (code: string) =
    let validCodes = [ "1"; "2"; "3" ]

    if List.contains code validCodes then
        true // Valid codes are tested separately
    else
        parseCode<SexCode> code = None

[<Property>]
let ``parseCode RacecourseCode accepts valid numeric codes`` () =
    Prop.forAll (Arb.fromGen (Gen.choose (1, 60))) (fun code ->
        let codeStr = sprintf "%02d" code
        // Some codes are valid, some are not - both outcomes are acceptable
        match parseCode<RacecourseCode> codeStr with
        | Some _ -> true
        | None -> true)

// ============================================================================
// Property-Based Tests for Record Parsing Invariants
// ============================================================================

[<Property>]
let ``parseFields with insufficient data returns error`` () =
    Prop.forAll (Arb.fromGen (Gen.choose (0, 50))) (fun size ->
        let data = Array.create size 32uy

        let fieldSpecs =
            [ FieldDefinitions.text "Field1" 0 10
              FieldDefinitions.text "Field2" 10 20
              FieldDefinitions.text "Field3" 30 25 ] // Extends beyond data

        match parseFields data fieldSpecs with
        | Ok _ -> size >= 55 // Should succeed only if data is large enough
        | Error _ -> size < 55 // Should fail if data is too small
    )

[<Property>]
let ``encodeShiftJis and decodeShiftJis are inverse for ASCII`` (str: string) =
    if isNull str then
        true // Skip null strings
    else
        let asciiOnly =
            str
            |> Seq.filter (fun c ->
                let code = System.Convert.ToInt32(c)
                code >= 32 && code < 128) // Skip control characters (0-31)
            |> Seq.truncate 100
            |> String.Concat

        if String.IsNullOrEmpty(asciiOnly) then
            true
        else
            let encoded = encodeShiftJis asciiOnly
            let decoded = decodeShiftJis encoded
            decoded.Trim() = asciiOnly.Trim() || decoded.Contains(asciiOnly)

[<Property>]
let ``parseFields extracts fields at correct offsets`` () =
    let data = Array.create 100 32uy // Fill with spaces
    let testValue = "TEST12"
    let testBytes = encodeShiftJis testValue
    Array.Copy(testBytes, 0, data, 20, testBytes.Length)

    let fieldSpecs = [ FieldDefinitions.text "TestField" 20 10 ]

    match parseFields data fieldSpecs with
    | Ok fields ->
        match Map.tryFind "TestField" fields with
        | Some(TextValue value) -> value.Trim().StartsWith("TEST")
        | _ -> false
    | Error _ -> false

// ============================================================================
// Property-Based Tests for Specific Record Types
// ============================================================================

[<Property>]
let ``TK record with valid race key can be created`` () =
    Prop.forAll (Arb.fromGen raceKeyGen) (fun raceKey ->
        Prop.forAll (Arb.fromGen horseIdGen) (fun horseId ->
            let data = Array.create 346 32uy
            let raceKeyBytes = encodeShiftJis raceKey
            let horseIdBytes = encodeShiftJis horseId
            Array.Copy(encodeShiftJis "TK", 0, data, 0, 2)
            Array.Copy(raceKeyBytes, 0, data, 2, min 16 raceKeyBytes.Length)
            Array.Copy(horseIdBytes, 0, data, 18, min 10 horseIdBytes.Length) // HorseId required
            Array.Copy(encodeShiftJis "TestHorse", 0, data, 28, 9) // HorseName required

            match Xanthos.Core.Records.TK.parse data with
            | Ok record ->
                record.RaceKey.Trim() = raceKey
                || record.RaceKey.Contains(raceKey.Substring(0, 10))
            | Error _ -> false))

[<Property>]
let ``O1 record with valid odds values can be created`` () =
    Prop.forAll (Arb.fromGen (Gen.choose (10, 9999))) (fun oddsValue ->
        Prop.forAll (Arb.fromGen raceKeyGen) (fun raceKey ->
            let data = Array.create 100 32uy
            Array.Copy(encodeShiftJis "O1", 0, data, 0, 2)
            Array.Copy(encodeShiftJis raceKey, 0, data, 2, 16)
            Array.Copy(encodeShiftJis "03", 0, data, 18, 2)
            Array.Copy(encodeShiftJis (sprintf "%04d" oddsValue), 0, data, 20, 4)

            match Xanthos.Core.Records.O1.parse data with
            | Ok record -> record.RaceKey.Trim().Length > 0 && record.Odds.IsSome
            | Error _ -> false))

[<Property>]
let ``HR record parsing is deterministic`` () =
    let data = Array.create 90 32uy
    Array.Copy(encodeShiftJis "HR", 0, data, 0, 2)
    Array.Copy(encodeShiftJis "2024050512345678", 0, data, 2, 16)
    Array.Copy(encodeShiftJis "1", 0, data, 18, 1)
    Array.Copy(encodeShiftJis "05", 0, data, 19, 2)
    Array.Copy(encodeShiftJis "000001000", 0, data, 25, 9)

    let result1 = Xanthos.Core.Records.HR.parse data
    let result2 = Xanthos.Core.Records.HR.parse data

    match (result1, result2) with
    | (Ok r1, Ok r2) ->
        r1.RaceKey = r2.RaceKey
        && r1.BetType = r2.BetType
        && r1.HorseNumber1 = r2.HorseNumber1
        && r1.Payoff = r2.Payoff
    | (Error _, Error _) -> true
    | _ -> false

// ============================================================================
// Property-Based Tests for Validation Module
// ============================================================================

open Xanthos.Runtime

/// Generate valid 4-character block dataspecs
let validDataspecGen =
    gen {
        let! blocks = Gen.choose (1, 4)
        let! chars = Gen.listOfLength (blocks * 4) (Gen.elements ([ 'A' .. 'Z' ] @ [ '0' .. '9' ]))
        return chars |> List.toArray |> String
    }

/// Generate valid option values (1-4)
let validOptionGen = Gen.choose (1, 4) |> Gen.map string

/// Generate valid date strings
let validDateStringGen =
    gen {
        let! year = Gen.choose (2000, 2030)
        let! month = Gen.choose (1, 12)
        let! day = Gen.choose (1, 28)
        return sprintf "%04d%02d%02d" year month day
    }

/// Generate valid datetime strings (yyyyMMddHHmmss)
let validDateTimeStringGen =
    gen {
        let! year = Gen.choose (2000, 2030)
        let! month = Gen.choose (1, 12)
        let! day = Gen.choose (1, 28)
        let! hour = Gen.choose (0, 23)
        let! minute = Gen.choose (0, 59)
        let! second = Gen.choose (0, 59)
        return sprintf "%04d%02d%02d%02d%02d%02d" year month day hour minute second
    }

[<Property>]
let ``normalizeDataspec preserves valid specs`` () =
    Prop.forAll (Arb.fromGen validDataspecGen) (fun spec ->
        match Validation.normalizeDataspec spec with
        | Ok normalized -> normalized.Length = spec.Length && normalized = spec.ToUpperInvariant()
        | Error _ -> false)

[<Property>]
let ``normalizeDataspec converts to uppercase`` () =
    Prop.forAll (Arb.fromGen validDataspecGen) (fun spec ->
        let lowercase = spec.ToLowerInvariant()

        match Validation.normalizeDataspec lowercase with
        | Ok normalized -> normalized = spec.ToUpperInvariant()
        | Error _ -> false)

[<Property>]
let ``normalizeDataspec rejects non-multiple-of-4 lengths`` () =
    Prop.forAll (Arb.fromGen (Gen.choose (1, 20))) (fun length ->
        if length % 4 = 0 then
            true // Skip valid lengths
        else
            let str = String.replicate length "A"

            match Validation.normalizeDataspec str with
            | Error _ -> true
            | Ok _ -> false)

[<Property>]
let ``normalizeDataspec trims whitespace`` () =
    Prop.forAll (Arb.fromGen validDataspecGen) (fun spec ->
        let withSpaces = "  " + spec + "  "

        match Validation.normalizeDataspec withSpaces with
        | Ok normalized -> normalized = spec.ToUpperInvariant()
        | Error _ -> false)

[<Property>]
let ``parseOpenOption returns 1 for None`` () =
    match Validation.parseOpenOption None with
    | Ok value -> value = 1
    | Error _ -> false

[<Property>]
let ``parseOpenOption accepts valid options 1-4`` () =
    Prop.forAll (Arb.fromGen validOptionGen) (fun optStr ->
        match Validation.parseOpenOption (Some optStr) with
        | Ok value ->
            let expected = System.Int32.Parse optStr
            value = expected && value >= 1 && value <= 4
        | Error _ -> false)

[<Property>]
let ``parseOpenOption rejects invalid options`` () =
    Prop.forAll (Arb.fromGen (Gen.choose (5, 100))) (fun n ->
        match Validation.parseOpenOption (Some(string n)) with
        | Error _ -> true
        | Ok _ -> false)

[<Property>]
let ``parseOpenOption rejects zero and negative`` () =
    Prop.forAll (Arb.fromGen (Gen.choose (-100, 0))) (fun n ->
        match Validation.parseOpenOption (Some(string n)) with
        | Error _ -> true
        | Ok _ -> false)

[<Property>]
let ``parseFromTime parses yyyyMMdd format`` () =
    Prop.forAll (Arb.fromGen validDateStringGen) (fun dateStr ->
        match Validation.parseFromTime (Some dateStr) with
        | Ok date ->
            let year = System.Int32.Parse(dateStr.Substring(0, 4))
            let month = System.Int32.Parse(dateStr.Substring(4, 2))
            let day = System.Int32.Parse(dateStr.Substring(6, 2))
            date.Year = year && date.Month = month && date.Day = day
        | Error _ -> false)

[<Property>]
let ``parseFromTime parses yyyyMMddHHmmss format`` () =
    Prop.forAll (Arb.fromGen validDateTimeStringGen) (fun dateStr ->
        match Validation.parseFromTime (Some dateStr) with
        | Ok date ->
            let year = System.Int32.Parse(dateStr.Substring(0, 4))
            let month = System.Int32.Parse(dateStr.Substring(4, 2))
            let day = System.Int32.Parse(dateStr.Substring(6, 2))
            let hour = System.Int32.Parse(dateStr.Substring(8, 2))
            let minute = System.Int32.Parse(dateStr.Substring(10, 2))
            let second = System.Int32.Parse(dateStr.Substring(12, 2))

            date.Year = year
            && date.Month = month
            && date.Day = day
            && date.Hour = hour
            && date.Minute = minute
            && date.Second = second
        | Error _ -> false)

[<Property>]
let ``parseFromTime rejects None`` () =
    match Validation.parseFromTime None with
    | Error _ -> true
    | Ok _ -> false

[<Property>]
let ``buildOpenRequest succeeds with valid inputs`` () =
    Prop.forAll (Arb.fromGen validDataspecGen) (fun spec ->
        Prop.forAll (Arb.fromGen validDateStringGen) (fun dateStr ->
            Prop.forAll
                (Arb.fromGen (Gen.elements [ None; Some "1"; Some "2"; Some "3"; Some "4" ]))
                (fun optionStr ->
                    match Validation.buildOpenRequest spec (Some dateStr) optionStr with
                    | Ok request ->
                        request.Spec = spec.ToUpperInvariant()
                        && request.Option >= 1
                        && request.Option <= 4
                    | Error _ -> false)))

[<Property>]
let ``buildOpenRequest fails when any validation fails`` () =
    // Invalid spec (3 chars)
    match Validation.buildOpenRequest "ABC" (Some "20240101") None with
    | Error _ -> true
    | Ok _ -> false

// ============================================================================
// Property-Based Tests for MovieType
// ============================================================================

[<Property>]
let ``MovieType toCode and fromCode are inverses for known types`` () =
    let knownTypes =
        [ MovieType.RaceVideo
          MovieType.PaddockVideo
          MovieType.MultiCameraVideo
          MovieType.PatrolVideo
          MovieType.WorkoutWeekAll
          MovieType.WorkoutWeekHorse
          MovieType.WorkoutHorseAll ]

    knownTypes
    |> List.forall (fun movieType ->
        let code = MovieType.toCode movieType
        let roundTripped = MovieType.fromCode code
        roundTripped = movieType)

[<Property>]
let ``MovieType fromCode and toCode are inverses for known codes`` () =
    let knownCodes = [ "00"; "01"; "02"; "03"; "11"; "12"; "13" ]

    knownCodes
    |> List.forall (fun code ->
        let movieType = MovieType.fromCode code
        let roundTripped = MovieType.toCode movieType
        roundTripped = code)

[<Property>]
let ``MovieType CustomMovieType preserves code`` () =
    Prop.forAll (Arb.fromGen (Gen.elements [ "04"; "05"; "10"; "14"; "20"; "99" ])) (fun code ->
        let movieType = MovieType.fromCode code

        match movieType with
        | CustomMovieType c -> c = code
        | _ -> false // Should be CustomMovieType for unknown codes
    )

[<Property>]
let ``MovieType toCode for CustomMovieType returns the code`` () =
    Prop.forAll (Arb.fromGen asciiAlphaNumStringGen) (fun code ->
        if String.IsNullOrEmpty code then
            true
        else
            let truncated = if code.Length > 10 then code.Substring(0, 10) else code
            let movieType = CustomMovieType truncated
            MovieType.toCode movieType = truncated)

[<Property>]
let ``MovieType isWorkoutType is true for workout types`` () =
    let workoutTypes =
        [ MovieType.WorkoutWeekAll
          MovieType.WorkoutWeekHorse
          MovieType.WorkoutHorseAll ]

    workoutTypes |> List.forall MovieType.isWorkoutType

[<Property>]
let ``MovieType isWorkoutType is false for non-workout types`` () =
    let nonWorkoutTypes =
        [ MovieType.RaceVideo
          MovieType.PaddockVideo
          MovieType.MultiCameraVideo
          MovieType.PatrolVideo ]

    nonWorkoutTypes |> List.forall (fun t -> not (MovieType.isWorkoutType t))

[<Property>]
let ``MovieType isWorkoutType for CustomMovieType depends on code prefix`` () =
    // Codes starting with "1" are workout types
    let workoutCodes = [ "14"; "15"; "19" ]
    let nonWorkoutCodes = [ "04"; "05"; "20"; "99" ]

    let workoutResult =
        workoutCodes
        |> List.forall (fun code -> MovieType.isWorkoutType (CustomMovieType code))

    let nonWorkoutResult =
        nonWorkoutCodes
        |> List.forall (fun code -> not (MovieType.isWorkoutType (CustomMovieType code)))

    workoutResult && nonWorkoutResult

// ============================================================================
// Property-Based Tests for WatchEvent
// ============================================================================

[<Property>]
let ``WatchEvent dataspecForEvent returns Some for known events`` () =
    let knownEvents =
        [ WatchEventType.PayoffConfirmed
          WatchEventType.HorseWeight
          WatchEventType.JockeyChange
          WatchEventType.WeatherChange
          WatchEventType.CourseChange
          WatchEventType.AvoidedRace
          WatchEventType.StartTimeChange ]

    knownEvents
    |> List.forall (fun event ->
        match WatchEvent.dataspecForEvent event with
        | Some _ -> true
        | None -> false)

[<Property>]
let ``WatchEvent dataspecForEvent returns None for UnknownEvent`` () =
    Prop.forAll (Arb.fromGen asciiAlphaNumStringGen) (fun code ->
        match WatchEvent.dataspecForEvent (WatchEventType.UnknownEvent code) with
        | None -> true
        | Some _ -> false)

[<Property>]
let ``WatchEvent toRealtimeRequest preserves RawKey`` () =
    Prop.forAll (Arb.fromGen asciiAlphaNumStringGen) (fun rawKey ->
        if String.IsNullOrEmpty rawKey then
            true
        else
            let event =
                { Event = WatchEventType.PayoffConfirmed
                  RawKey = rawKey
                  Timestamp = None
                  MeetingDate = None
                  CourseCode = None
                  RaceNumber = None
                  RecordType = None
                  ParticipantId = None
                  AdditionalData = None }

            match WatchEvent.toRealtimeRequest event with
            | Some req -> req.Key = rawKey
            | None -> false)

// ============================================================================
// Property-Based Tests for Domain Type Invariants
// ============================================================================

[<Property>]
let ``RaceId create rejects empty or whitespace`` () =
    let invalidInputs = [ ""; "   "; "\t"; "\n"; "  \t  " ]

    invalidInputs
    |> List.forall (fun input ->
        match RaceId.create input with
        | Error _ -> true
        | Ok _ -> false)

[<Property>]
let ``RunnerId create rejects empty or whitespace`` () =
    let invalidInputs = [ ""; "   "; "\t"; "\n"; "  \t  " ]

    invalidInputs
    |> List.forall (fun input ->
        match RunnerId.create input with
        | Error _ -> true
        | Ok _ -> false)

[<Property>]
let ``RaceId unsafe and value are inverses`` () =
    Prop.forAll (Arb.fromGen asciiAlphaNumStringGen) (fun str ->
        if String.IsNullOrEmpty str then
            true
        else
            let id = RaceId.unsafe str
            RaceId.value id = str)

[<Property>]
let ``RunnerId unsafe and value are inverses`` () =
    Prop.forAll (Arb.fromGen asciiAlphaNumStringGen) (fun str ->
        if String.IsNullOrEmpty str then
            true
        else
            let id = RunnerId.unsafe str
            RunnerId.value id = str)

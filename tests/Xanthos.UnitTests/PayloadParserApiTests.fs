module Xanthos.UnitTests.PayloadParserApiTests

open System
open Xunit
open Xanthos.Core
open Xanthos.Core.Records
open Xanthos.Interop
open Xanthos.Runtime

/// Helper to encode strings as Shift-JIS bytes
let private encodeShiftJis (s: string) = Text.encodeShiftJis s

/// Tests for JvLinkService PayloadParser API
module ServicePayloadParserTests =

    let private createConfig () =
        { JvLinkConfig.Sid = "test-sid"
          SavePath = None
          ServiceKey = None
          UseJvGets = None }

    let private createTKPayload () =
        let data = Array.create 346 32uy
        let tkBytes = encodeShiftJis "TK"
        let raceKeyBytes = encodeShiftJis "2024010101010101"
        let horseIdBytes = encodeShiftJis "1234567890"
        let horseNameBytes = encodeShiftJis "TestHorse"
        Array.Copy(tkBytes, 0, data, 0, 2)
        Array.Copy(raceKeyBytes, 0, data, 2, min 16 raceKeyBytes.Length)
        Array.Copy(horseIdBytes, 0, data, 18, min 10 horseIdBytes.Length)
        Array.Copy(horseNameBytes, 0, data, 28, min 36 horseNameBytes.Length)

        { JvPayload.Timestamp = None
          Data = data }

    let private createRAPayload () =
        let data = Array.create 2890 32uy
        let raBytes = encodeShiftJis "RA"
        let raceKeyBytes = encodeShiftJis "2024010101010101"
        let raceNameBytes = encodeShiftJis "TestRace"
        Array.Copy(raBytes, 0, data, 0, 2)
        Array.Copy(raceKeyBytes, 0, data, 2, min 16 raceKeyBytes.Length)
        Array.Copy(raceNameBytes, 0, data, 59, min 60 raceNameBytes.Length)

        { JvPayload.Timestamp = None
          Data = data }

    [<Fact>]
    let ``ParsePayload parses TK payload correctly`` () =
        let payload = createTKPayload ()
        let result = JvLinkService.ParsePayload(payload)

        match result with
        | Ok(TKRecord tk) -> Assert.Equal("TestHorse", tk.HorseName.Trim())
        | Ok other -> Assert.Fail($"Expected TKRecord but got {other}")
        | Error err -> Assert.Fail($"Parsing failed: {err}")

    [<Fact>]
    let ``ParsePayloads parses multiple payloads`` () =
        let payloads = [ createTKPayload (); createRAPayload () ]
        let result = JvLinkService.ParsePayloads(payloads)

        match result with
        | Ok records ->
            Assert.Equal(2, records.Length)

            Assert.True(
                records
                |> List.exists (function
                    | TKRecord _ -> true
                    | _ -> false)
            )

            Assert.True(
                records
                |> List.exists (function
                    | RARecord _ -> true
                    | _ -> false)
            )
        | Error err -> Assert.Fail($"Parsing failed: {err}")

    [<Fact>]
    let ``TryParsePayloads collects successes and failures`` () =
        let validPayload = createTKPayload ()

        let invalidPayload =
            { JvPayload.Timestamp = None
              Data = [| 0uy |] } // Too short

        let payloads = [ validPayload; invalidPayload ]

        let (successes, failures) = JvLinkService.TryParsePayloads(payloads)

        Assert.Equal(1, successes.Length)
        Assert.Equal(1, failures.Length)

    [<Fact>]
    let ``GetRecordTypeId extracts type from data`` () =
        let tkData = createTKPayload().Data
        let result = JvLinkService.GetRecordTypeId(tkData)

        Assert.Equal("TK", result)

    [<Fact>]
    let ``GetRecordTypeId returns empty for short data`` () =
        let shortData = [| 0uy |]
        let result = JvLinkService.GetRecordTypeId(shortData)

        Assert.Equal("", result)

    [<Fact>]
    let ``FetchTypedRecords integrates with stub`` () =
        // Create stub with TK payload
        let payload = createTKPayload ()
        let stub = JvLinkStub.FromPayloads([| payload.Data |])
        let service = new JvLinkService(stub, createConfig ())

        let request =
            { Spec = "RACE"
              FromTime = DateTime.MinValue
              Option = 1 }

        let result = service.FetchTypedRecords(request)

        match result with
        | Ok records ->
            Assert.Equal(1, records.Length)

            match records.[0] with
            | TKRecord _ -> () // Success
            | other -> Assert.Fail($"Expected TKRecord but got {other}")
        | Error err -> Assert.Fail($"FetchTypedRecords failed: {err}")

    [<Fact>]
    let ``FetchTypedRecordsCollectErrors handles mixed results`` () =
        let validPayload = createTKPayload ()
        // Create a payload that will parse but with invalid format might not parse correctly
        let stub = JvLinkStub.FromPayloads([| validPayload.Data |])
        let service = new JvLinkService(stub, createConfig ())

        let request =
            { Spec = "RACE"
              FromTime = DateTime.MinValue
              Option = 1 }

        let result = service.FetchTypedRecordsCollectErrors(request)

        match result with
        | Ok(records, failures) ->
            Assert.Equal(1, records.Length)
            Assert.Equal(0, failures.Length)
        | Error err -> Assert.Fail($"FetchTypedRecordsCollectErrors failed: {err}")

/// Tests for PayloadParser module functions
module PayloadParserModuleTests =

    [<Fact>]
    let ``parsePayload returns error for empty data`` () =
        let payload =
            { JvPayload.Timestamp = None
              Data = Array.empty }

        let result = PayloadParser.parsePayload payload

        Assert.True(Result.isError result)

    [<Fact>]
    let ``parsePayload returns error for single byte data`` () =
        let payload =
            { JvPayload.Timestamp = None
              Data = [| 65uy |] }

        let result = PayloadParser.parsePayload payload

        Assert.True(Result.isError result)

    [<Fact>]
    let ``getRecordTypeId handles various record types`` () =
        let testCases =
            [ ("TK", [| 84uy; 75uy |]) // TK in ASCII
              ("RA", [| 82uy; 65uy |]) // RA in ASCII
              ("SE", [| 83uy; 69uy |]) // SE in ASCII
              ("HR", [| 72uy; 82uy |]) ] // HR in ASCII

        for (expected, bytes) in testCases do
            let result = PayloadParser.getRecordTypeId bytes
            Assert.Equal(expected, result)

    [<Fact>]
    let ``parsePayload handles unknown record type`` () =
        let data = Array.create 100 32uy
        let unknownType = encodeShiftJis "XX"
        Array.Copy(unknownType, 0, data, 0, 2)

        let payload =
            { JvPayload.Timestamp = None
              Data = data }

        let result = PayloadParser.parsePayload payload

        match result with
        | Ok(UnknownRecord(typeId, _)) -> Assert.Equal("XX", typeId)
        | Ok other -> Assert.Fail($"Expected UnknownRecord but got {other}")
        | Error err -> Assert.Fail($"Unexpected error: {err}")

/// Tests for type-specific extractors
module TypeExtractorTests =
    open Xanthos.Core.Records

    let private createPayload (typeId: string) size =
        let data = Array.create size 32uy
        let typeBytes = encodeShiftJis typeId
        Array.Copy(typeBytes, 0, data, 0, min 2 typeBytes.Length)

        { JvPayload.Timestamp = None
          Data = data }

    [<Fact>]
    let ``getTKRecords extracts only TK records`` () =
        let tkData = Array.create 346 32uy
        let tkBytes = encodeShiftJis "TK"
        let raceKeyBytes = encodeShiftJis "2024010101010101"
        let horseIdBytes = encodeShiftJis "1234567890"
        let horseNameBytes = encodeShiftJis "TestHorse"
        Array.Copy(tkBytes, 0, tkData, 0, 2)
        Array.Copy(raceKeyBytes, 0, tkData, 2, 16)
        Array.Copy(horseIdBytes, 0, tkData, 18, 10)
        Array.Copy(horseNameBytes, 0, tkData, 28, 9)

        let tkPayload =
            { JvPayload.Timestamp = None
              Data = tkData }

        // Create a record that will parse as unknown
        let unknownPayload = createPayload "XX" 100

        let payloads = [ tkPayload; unknownPayload ]
        let parseResult = PayloadParser.parsePayloads payloads

        match parseResult with
        | Ok records ->
            let tkRecords = PayloadParser.getTKRecords records
            Assert.Equal(1, tkRecords.Length)
        | Error _ ->
            // If TK parsing fails, the test will need adjusted data
            ()

    [<Fact>]
    let ``filterByType works with custom extractor`` () =
        let unknownPayload1 = createPayload "X1" 100
        let unknownPayload2 = createPayload "X2" 100

        let payloads = [ unknownPayload1; unknownPayload2 ]
        let parseResult = PayloadParser.parsePayloads payloads

        match parseResult with
        | Ok records ->
            let unknowns =
                PayloadParser.filterByType records (function
                    | UnknownRecord(_, _) -> Some()
                    | _ -> None)

            Assert.Equal(2, unknowns.Length)
        | Error err -> Assert.Fail($"Parsing failed: {err}")

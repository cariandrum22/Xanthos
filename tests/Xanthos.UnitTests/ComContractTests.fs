module Xanthos.UnitTests.ComContractTests

/// <summary>
/// Contract tests for IJvLinkClient interface stability.
/// These tests verify the documented behavior defined in IJvLinkClient interface remarks:
/// - JVRead: returns Payload/FileBoundary/DownloadPending/EndOfStream; never throws for EOF/boundary.
/// - JVGets: returns raw byte count (>=0), -1(file boundary), -3(download pending), or &lt; -3 mapped to ComError.
/// - Status/Open/Close/Skip/Cancel: follow ErrorCodes for negative codes; success is Ok().
/// - Movie APIs: return domain types; errors surface as ComError.
/// </summary>

open System
open Xunit
open Xanthos.Core
open Xanthos.Interop

let private defaultRequest =
    { Spec = "TEST"
      FromTime = DateTime(2024, 1, 1)
      Option = 1 }

// ============================================================================
// State Machine Contract Tests
// ============================================================================

[<Fact>]
let ``Init should require non-empty sid`` () =
    let stub = new JvLinkStub()
    let client = stub :> IJvLinkClient

    match client.Init "" with
    | Error(InvalidInput _) -> ()
    | other -> failwithf "Expected InvalidInput, got %A" other

[<Fact>]
let ``Init with valid sid should succeed`` () =
    let stub = new JvLinkStub()
    let client = stub :> IJvLinkClient

    match client.Init "test-sid" with
    | Ok() -> ()
    | Error e -> failwithf "Expected Ok, got %A" e

[<Fact>]
let ``Open should require non-empty spec`` () =
    let stub = new JvLinkStub()
    let client = stub :> IJvLinkClient
    ignore (client.Init "sid")

    match
        client.Open
            { Spec = ""
              FromTime = DateTime(2024, 1, 1)
              Option = 1 }
    with
    | Error(InvalidInput _) -> ()
    | other -> failwithf "Expected InvalidInput, got %A" other

[<Fact>]
let ``Open with valid request should succeed`` () =
    let stub = new JvLinkStub()
    let client = stub :> IJvLinkClient
    ignore (client.Init "sid")

    match client.Open defaultRequest with
    | Ok _ -> ()
    | Error e -> failwithf "Expected Ok, got %A" e

[<Fact>]
let ``Read before Open should return InvalidState`` () =
    let stub = new JvLinkStub()
    let client = stub :> IJvLinkClient
    ignore (client.Init "sid")
    // Not calling Open
    match client.Read() with
    | Error(InvalidState _) -> ()
    | other -> failwithf "Expected InvalidState, got %A" other

[<Fact>]
let ``Gets before Open should return InvalidState`` () =
    let stub = new JvLinkStub()
    let client = stub :> IJvLinkClient
    ignore (client.Init "sid")
    // Not calling Open
    let mutable buff = ""
    let mutable fname = ""

    match client.Gets(&buff, 1024, &fname) with
    | Error(InvalidState _) -> ()
    | other -> failwithf "Expected InvalidState, got %A" other

[<Fact>]
let ``Skip before Open should return InvalidState`` () =
    let stub = new JvLinkStub()
    let client = stub :> IJvLinkClient
    ignore (client.Init "sid")

    match client.Skip() with
    | Error(InvalidState _) -> ()
    | other -> failwithf "Expected InvalidState, got %A" other

[<Fact>]
let ``Close should be idempotent`` () =
    let stub = new JvLinkStub()
    let client = stub :> IJvLinkClient
    ignore (client.Init "sid")
    ignore (client.Open defaultRequest)
    client.Close()
    client.Close() // Should not throw
    ()

// ============================================================================
// JVRead Contract Tests
// ============================================================================

[<Fact>]
let ``Read should return Payload for payload outcomes`` () =
    let data = Text.encodeShiftJis "test data"

    let payload =
        { Timestamp = Some DateTime.UtcNow
          Data = data }

    let stub = new JvLinkStub(Seq.singleton (Ok(Payload payload)))
    let client = stub :> IJvLinkClient
    ignore (client.Init "sid")
    ignore (client.Open defaultRequest)

    match client.Read() with
    | Ok(Payload p) -> Assert.Equal(data.Length, p.Data.Length)
    | other -> failwithf "Expected Payload, got %A" other

[<Fact>]
let ``Read should return FileBoundary`` () =
    let stub = new JvLinkStub(Seq.singleton (Ok FileBoundary))
    let client = stub :> IJvLinkClient
    ignore (client.Init "sid")
    ignore (client.Open defaultRequest)

    match client.Read() with
    | Ok FileBoundary -> ()
    | other -> failwithf "Expected FileBoundary, got %A" other

[<Fact>]
let ``Read should return DownloadPending`` () =
    let stub = new JvLinkStub(Seq.singleton (Ok DownloadPending))
    let client = stub :> IJvLinkClient
    ignore (client.Init "sid")
    ignore (client.Open defaultRequest)

    match client.Read() with
    | Ok DownloadPending -> ()
    | other -> failwithf "Expected DownloadPending, got %A" other

[<Fact>]
let ``Read should return EndOfStream when empty`` () =
    let stub = new JvLinkStub(Seq.empty)
    let client = stub :> IJvLinkClient
    ignore (client.Init "sid")
    ignore (client.Open defaultRequest)

    match client.Read() with
    | Ok EndOfStream -> ()
    | other -> failwithf "Expected EndOfStream, got %A" other

[<Fact>]
let ``Read should propagate ComError`` () =
    let stub =
        new JvLinkStub(Seq.singleton (Error(CommunicationFailure(-401, "test error"))))

    let client = stub :> IJvLinkClient
    ignore (client.Init "sid")
    ignore (client.Open defaultRequest)

    match client.Read() with
    | Error(CommunicationFailure(code, _)) -> Assert.Equal(-401, code)
    | other -> failwithf "Expected CommunicationFailure, got %A" other

// ============================================================================
// JVGets Contract Tests - Return Value Mapping
// ============================================================================

[<Fact>]
let ``Gets should return positive byte count for payload`` () =
    let text = "テストデータ"
    let data = Text.encodeShiftJis text

    let payload =
        { Timestamp = Some DateTime.UtcNow
          Data = data }

    let stub = new JvLinkStub(Seq.singleton (Ok(Payload payload)))
    let client = stub :> IJvLinkClient
    ignore (client.Init "sid")
    ignore (client.Open defaultRequest)
    let mutable buff = ""
    let mutable fname = ""

    match client.Gets(&buff, 1024, &fname) with
    | Ok n when n > 0 -> Assert.Equal(data.Length, n)
    | other -> failwithf "Expected positive byte count, got %A" other

[<Fact>]
let ``Gets should return -1 for FileBoundary`` () =
    let stub = new JvLinkStub(Seq.singleton (Ok FileBoundary))
    let client = stub :> IJvLinkClient
    ignore (client.Init "sid")
    ignore (client.Open defaultRequest)
    let mutable buff = ""
    let mutable fname = ""

    match client.Gets(&buff, 1024, &fname) with
    | Ok code -> Assert.Equal(-1, code)
    | Error e -> failwithf "Expected -1, got error %A" e

[<Fact>]
let ``Gets should return -3 for DownloadPending`` () =
    let stub = new JvLinkStub(Seq.singleton (Ok DownloadPending))
    let client = stub :> IJvLinkClient
    ignore (client.Init "sid")
    ignore (client.Open defaultRequest)
    let mutable buff = ""
    let mutable fname = ""

    match client.Gets(&buff, 1024, &fname) with
    | Ok code -> Assert.Equal(-3, code)
    | Error e -> failwithf "Expected -3, got error %A" e

[<Fact>]
let ``Gets should return 0 for EndOfStream`` () =
    let stub = new JvLinkStub(Seq.singleton (Ok EndOfStream))
    let client = stub :> IJvLinkClient
    ignore (client.Init "sid")
    ignore (client.Open defaultRequest)
    let mutable buff = ""
    let mutable fname = ""

    match client.Gets(&buff, 1024, &fname) with
    | Ok code -> Assert.Equal(0, code)
    | Error e -> failwithf "Expected 0, got error %A" e

[<Fact>]
let ``Gets should return 0 for empty queue`` () =
    let stub = new JvLinkStub(Seq.empty)
    let client = stub :> IJvLinkClient
    ignore (client.Init "sid")
    ignore (client.Open defaultRequest)
    let mutable buff = ""
    let mutable fname = ""

    match client.Gets(&buff, 1024, &fname) with
    | Ok code -> Assert.Equal(0, code)
    | Error e -> failwithf "Expected 0, got error %A" e

// ============================================================================
// Status/Skip/Cancel Contract Tests
// ============================================================================

[<Fact>]
let ``Status should return Ok with count`` () =
    let stub = new JvLinkStub(Seq.empty)
    let client = stub :> IJvLinkClient
    ignore (client.Init "sid")
    ignore (client.Open defaultRequest)

    match client.Status() with
    | Ok n when n >= 0 -> ()
    | other -> failwithf "Expected Ok with non-negative count, got %A" other

[<Fact>]
let ``Status should track completed payloads`` () =
    let payload =
        { Timestamp = None
          Data = Text.encodeShiftJis "test" }

    let stub = new JvLinkStub([ Ok(Payload payload); Ok(Payload payload) ])
    let client = stub :> IJvLinkClient
    ignore (client.Init "sid")
    ignore (client.Open defaultRequest)
    ignore (client.Read())
    ignore (client.Read())

    match client.Status() with
    | Ok n -> Assert.Equal(2, n)
    | Error e -> failwithf "Expected Ok, got %A" e

[<Fact>]
let ``Skip should advance to next payload`` () =
    let payload1 =
        { Timestamp = None
          Data = Text.encodeShiftJis "first" }

    let payload2 =
        { Timestamp = None
          Data = Text.encodeShiftJis "second" }

    let stub = new JvLinkStub([ Ok(Payload payload1); Ok(Payload payload2) ])
    let client = stub :> IJvLinkClient
    ignore (client.Init "sid")
    ignore (client.Open defaultRequest)

    match client.Skip() with
    | Ok() ->
        match client.Read() with
        | Ok(Payload p) -> Assert.Contains("second", Text.decodeShiftJis p.Data)
        | other -> failwithf "Expected second payload, got %A" other
    | Error e -> failwithf "Skip failed: %A" e

[<Fact>]
let ``Cancel should close session`` () =
    let stub =
        new JvLinkStub(Seq.singleton (Ok(Payload { Timestamp = None; Data = Array.empty })))

    let client = stub :> IJvLinkClient
    ignore (client.Init "sid")
    ignore (client.Open defaultRequest)

    match client.Cancel() with
    | Ok() ->
        // After cancel, read should require re-opening
        match client.Read() with
        | Error(InvalidState _) -> ()
        | other -> failwithf "Expected InvalidState after Cancel, got %A" other
    | Error e -> failwithf "Cancel failed: %A" e

// ============================================================================
// Movie API Contract Tests
// ============================================================================

[<Fact>]
let ``MovieCheck should return MovieAvailability`` () =
    let stub = new JvLinkStub()
    stub.UseMovieAvailabilityResponder(fun _ -> Ok MovieAvailability.Available)
    let client = stub :> IJvLinkClient

    match client.MovieCheck "testkey" with
    | Ok MovieAvailability.Available -> ()
    | other -> failwithf "Expected Available, got %A" other

[<Fact>]
let ``MovieCheckWithType should return MovieAvailability`` () =
    let stub = new JvLinkStub()
    stub.UseMovieAvailabilityWithTypeResponder(fun _ -> Ok MovieAvailability.NotFound)
    let client = stub :> IJvLinkClient

    match client.MovieCheckWithType("00", "testkey") with
    | Ok MovieAvailability.NotFound -> ()
    | other -> failwithf "Expected NotFound, got %A" other

[<Fact>]
let ``MoviePlay should return Ok on success`` () =
    let stub = new JvLinkStub()
    stub.UseMoviePlayResponder(fun _ -> Ok())
    let client = stub :> IJvLinkClient

    match client.MoviePlay "testkey" with
    | Ok() -> ()
    | Error e -> failwithf "Expected Ok, got %A" e

[<Fact>]
let ``MoviePlayWithType should return Ok on success`` () =
    let stub = new JvLinkStub()
    stub.UseMoviePlayResponder(fun _ -> Ok())
    let client = stub :> IJvLinkClient

    match client.MoviePlayWithType("00", "testkey") with
    | Ok() -> ()
    | Error e -> failwithf "Expected Ok, got %A" e

[<Fact>]
let ``MovieRead should return MovieEnd when empty`` () =
    let stub = new JvLinkStub()
    let client = stub :> IJvLinkClient

    match client.MovieRead() with
    | Ok MovieEnd -> ()
    | other -> failwithf "Expected MovieEnd, got %A" other

[<Fact>]
let ``MovieRead should return MovieRecord for queued data`` () =
    let stub = new JvLinkStub()

    let listing =
        { RawKey = "test-key-123"
          WorkoutDate = Some DateTime.UtcNow
          RegistrationId = Some "reg001" }

    stub.EnqueueMovieReadOutcome(Ok(MovieRecord listing))
    let client = stub :> IJvLinkClient

    match client.MovieRead() with
    | Ok(MovieRecord l) -> Assert.Equal("test-key-123", l.RawKey)
    | other -> failwithf "Expected MovieRecord, got %A" other

// ============================================================================
// WatchEvent Contract Tests
// ============================================================================

[<Fact>]
let ``WatchEvent should require initialization`` () =
    let stub = new JvLinkStub()
    let client = stub :> IJvLinkClient
    // Not calling Init
    match client.WatchEvent(fun _ -> ()) with
    | Error NotInitialized -> ()
    | other -> failwithf "Expected NotInitialized, got %A" other

[<Fact>]
let ``WatchEvent should succeed after Init`` () =
    let stub = new JvLinkStub()
    let client = stub :> IJvLinkClient
    ignore (client.Init "sid")

    match client.WatchEvent(fun _ -> ()) with
    | Ok() -> ()
    | Error e -> failwithf "Expected Ok, got %A" e

[<Fact>]
let ``WatchEvent callback should be invoked on RaiseEvent`` () =
    let stub = new JvLinkStub()
    let client = stub :> IJvLinkClient
    ignore (client.Init "sid")
    let mutable receivedKey = ""
    ignore (client.WatchEvent(fun key -> receivedKey <- key))
    stub.RaiseEvent "test-event-key"
    Assert.Equal("test-event-key", receivedKey)

[<Fact>]
let ``WatchEventClose should stop notifications`` () =
    let stub = new JvLinkStub()
    let client = stub :> IJvLinkClient
    ignore (client.Init "sid")
    let mutable callCount = 0
    ignore (client.WatchEvent(fun _ -> callCount <- callCount + 1))
    stub.RaiseEvent "event1"
    ignore (client.WatchEventClose())
    stub.RaiseEvent "event2" // Should not be received
    Assert.Equal(1, callCount)

// ============================================================================
// Property Accessor Contract Tests
// ============================================================================

[<Fact>]
let ``SaveFlag property should be readable and writable`` () =
    let stub = new JvLinkStub()
    let client = stub :> IJvLinkClient
    client.SaveFlag <- true
    Assert.True(client.SaveFlag)
    client.SaveFlag <- false
    Assert.False(client.SaveFlag)

[<Fact>]
let ``SavePath property should be readable and writable`` () =
    let stub = new JvLinkStub()
    let client = stub :> IJvLinkClient
    client.SavePath <- "C:\\test\\path"
    Assert.Equal("C:\\test\\path", client.SavePath)

[<Fact>]
let ``ServiceKey property should be readable and writable`` () =
    let stub = new JvLinkStub()
    let client = stub :> IJvLinkClient
    client.ServiceKey <- "test-key-123"
    Assert.Equal("test-key-123", client.ServiceKey)

[<Fact>]
let ``JVLinkVersion property should return version string`` () =
    let stub = new JvLinkStub()
    stub.ConfigureJVLinkVersion("1.2.3")
    let client = stub :> IJvLinkClient
    Assert.Equal("1.2.3", client.JVLinkVersion)

[<Fact>]
let ``TotalReadFileSize should reflect configured value`` () =
    let stub = new JvLinkStub()
    stub.ConfigureTotalReadFileSize(12345L)
    let client = stub :> IJvLinkClient
    Assert.Equal(12345L, client.TotalReadFileSize)

[<Fact>]
let ``ParentWindowHandle property should be readable and writable`` () =
    let stub = new JvLinkStub()
    let client = stub :> IJvLinkClient
    let handle = IntPtr(12345)
    client.ParentWindowHandle <- handle
    Assert.Equal(handle, client.ParentWindowHandle)

[<Fact>]
let ``PayoffDialogSuppressed property should be readable and writable`` () =
    let stub = new JvLinkStub()
    let client = stub :> IJvLinkClient
    client.PayoffDialogSuppressed <- true
    Assert.True(client.PayoffDialogSuppressed)

// ============================================================================
// Realtime Session Contract Tests
// ============================================================================

[<Fact>]
let ``OpenRealtime should enable realtime mode`` () =
    let stub = new JvLinkStub()
    let client = stub :> IJvLinkClient
    ignore (client.Init "sid")

    match client.OpenRealtime("TEST", "20240101") with
    | Ok _ -> ()
    | Error e -> failwithf "Expected Ok, got %A" e

[<Fact>]
let ``OpenRealtime should require non-empty spec`` () =
    let stub = new JvLinkStub()
    let client = stub :> IJvLinkClient
    ignore (client.Init "sid")

    match client.OpenRealtime("", "20240101") with
    | Error(InvalidInput _) -> ()
    | other -> failwithf "Expected InvalidInput, got %A" other

[<Fact>]
let ``OpenRealtime should move payloads to realtime queue`` () =
    let payload =
        { Timestamp = None
          Data = Text.encodeShiftJis "realtime data" }

    let stub = new JvLinkStub(Seq.singleton (Ok(Payload payload)))
    let client = stub :> IJvLinkClient
    ignore (client.Init "sid")
    ignore (client.OpenRealtime("TEST", ""))

    match client.Read() with
    | Ok(Payload p) -> Assert.Contains("realtime", Text.decodeShiftJis p.Data)
    | other -> failwithf "Expected payload from realtime queue, got %A" other

// ============================================================================
// File Operations Contract Tests
// ============================================================================

[<Fact>]
let ``DeleteFile should return Ok`` () =
    let stub = new JvLinkStub()
    let client = stub :> IJvLinkClient

    match client.DeleteFile "testfile.jvd" with
    | Ok() -> ()
    | Error e -> failwithf "Expected Ok, got %A" e

[<Fact>]
let ``CourseFile should return path and explanation`` () =
    let stub = new JvLinkStub()
    stub.ConfigureCourseFileResponse("C:\\course.gif", "Course explanation")
    let client = stub :> IJvLinkClient

    match client.CourseFile "testkey" with
    | Ok(path, explanation) ->
        Assert.Equal("C:\\course.gif", path)
        Assert.Equal("Course explanation", explanation)
    | Error e -> failwithf "Expected Ok, got %A" e

[<Fact>]
let ``CourseFile2 should save to specified filepath`` () =
    let stub = new JvLinkStub()
    let client = stub :> IJvLinkClient
    let outputPath = "C:\\output\\course.gif"

    match client.CourseFile2("testkey", outputPath) with
    | Ok() -> () // JVCourseFile2 saves to filepath - just returns unit on success
    | Error e -> failwithf "Expected Ok, got %A" e

[<Fact>]
let ``SilksFile should invoke responder`` () =
    let stub = new JvLinkStub()
    stub.UseSilksFileResponder(fun (pattern, outputPath) -> Ok(Some $"{outputPath}/{pattern}.bmp"))
    let client = stub :> IJvLinkClient

    match client.SilksFile("01234", "C:\\output") with
    | Ok(Some result) -> Assert.Equal("C:\\output/01234.bmp", result)
    | Ok None -> failwith "Expected Some result, got None"
    | Error e -> failwithf "Expected Ok, got %A" e

[<Fact>]
let ``SilksBinary should invoke responder`` () =
    let stub = new JvLinkStub()
    let expectedBytes = [| 0x42uy; 0x4Duy |] // BMP header
    stub.UseSilksBinaryResponder(fun _ -> Ok(Some expectedBytes))
    let client = stub :> IJvLinkClient

    match client.SilksBinary "testpattern" with
    | Ok(Some bytes) -> Assert.Equal<byte[]>(expectedBytes, bytes)
    | Ok None -> failwith "Expected Some bytes, got None"
    | Error e -> failwithf "Expected Ok, got %A" e

// ============================================================================
// SetUI and Configuration Contract Tests
// ============================================================================

[<Fact>]
let ``SetUiProperties should succeed and track invocations`` () =
    let stub = new JvLinkStub()
    let client = stub :> IJvLinkClient

    match client.SetUiProperties() with
    | Ok() -> Assert.Equal(1, stub.UiDialogInvocationCount)
    | Error e -> failwithf "Expected Ok, got %A" e

[<Fact>]
let ``SetSaveFlag should succeed`` () =
    let stub = new JvLinkStub()
    let client = stub :> IJvLinkClient

    match client.SetSaveFlag true with
    | Ok() -> ()
    | Error e -> failwithf "Expected Ok, got %A" e

[<Fact>]
let ``SetServiceKeyDirect should succeed and track key`` () =
    let stub = new JvLinkStub()
    let client = stub :> IJvLinkClient

    match client.SetServiceKeyDirect "new-service-key" with
    | Ok() -> Assert.Equal(Some "new-service-key", stub.LastServiceKey)
    | Error e -> failwithf "Expected Ok, got %A" e

[<Fact>]
let ``SetSavePathDirect should succeed and update path`` () =
    let stub = new JvLinkStub()
    let client = stub :> IJvLinkClient

    match client.SetSavePathDirect "C:\\new\\path" with
    | Ok() -> Assert.Equal("C:\\new\\path", stub.CurrentSavePath)
    | Error e -> failwithf "Expected Ok, got %A" e

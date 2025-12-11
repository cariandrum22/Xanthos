module Xanthos.UnitTests.Tests

open System
open System.Text
open System.Threading
open System.Threading.Tasks
open Xunit
open Xanthos.Core
open Xanthos.Core.Errors
open Xanthos.Core.ErrorCodes
open Xanthos.Core.ErrorCatalog
open Xanthos.Core.Serialization
open Xanthos.Interop
open Xanthos.Runtime

let private samplePayload bytes =
    Ok(JvReadOutcome.Payload { Timestamp = None; Data = bytes })

let private stubWith payloads =
    payloads
    |> Seq.map samplePayload
    |> Seq.toArray
    |> fun arr -> new JvLinkStub(arr) :> IJvLinkClient

let private assertCatalogEntry methodName code expectedCategory =
    match ErrorCatalog.tryFind methodName code with
    | Some info ->
        Assert.Equal(code, info.Base.Code)
        Assert.Equal(expectedCategory, info.Base.Category)
    | None -> failwithf "No catalog entry for %s (%d)" methodName code

let private defaultRequest =
    { Spec = "SPEC"
      FromTime = DateTime(2024, 1, 1)
      Option = 1 }

[<Fact>]
let ``RaceId create succeeds for valid date format`` () =
    // RaceId must start with valid yyyyMMdd date
    match RaceId.create "2024010106010801" with
    | Ok raceId -> Assert.Equal("2024010106010801", RaceId.value raceId)
    | Error err -> failwithf "expected Ok, got %A" err

[<Fact>]
let ``RaceId create rejects whitespace`` () =
    match RaceId.create "   " with
    | Ok _ -> failwith "expected validation failure"
    | Error(ValidationError _) -> ()
    | Error other -> failwithf "unexpected error %A" other

[<Fact>]
let ``JvLinkStub returns queued payloads in order`` () =
    let client = stubWith [ [| 0uy |]; [| 1uy; 2uy |] ]

    Assert.Equal(Ok(), client.Init "test")
    Assert.True(Result.isOk (client.Open defaultRequest))

    match client.Read() with
    | Ok(Payload payload) -> Assert.Equal<byte[]>([| 0uy |], payload.Data)
    | other -> failwithf "unexpected result %A" other

    match client.Read() with
    | Ok(Payload payload) -> Assert.Equal<byte[]>([| 1uy; 2uy |], payload.Data)
    | other -> failwithf "unexpected result %A" other

    match client.Read() with
    | Ok EndOfStream -> ()
    | other -> failwithf "expected end-of-stream, got %A" other

[<Fact>]
let ``JVGets returns payload line by line with byref parameters`` () =
    let testData = "TESTDATA123"
    let bytes = Text.encodeShiftJis testData
    let client = stubWith [ bytes ]

    Assert.Equal(Ok(), client.Init "test")
    Assert.True(Result.isOk (client.Open defaultRequest))

    let mutable buffer = ""
    let mutable filename = ""

    match client.Gets(&buffer, 1024, &filename) with
    | Ok bytesRead ->
        Assert.True(bytesRead > 0)
        Assert.Equal(testData, buffer)
        Assert.Equal("stubfile.jvd", filename)
    | Error err -> failwithf "JVGets failed %A" err

    let mutable buffer2 = ""
    let mutable filename2 = ""

    match client.Gets(&buffer2, 1024, &filename2) with
    | Ok bytesRead -> Assert.Equal(0, bytesRead)
    | Error err -> failwithf "Expected EOF, got error %A" err

[<Fact>]
let ``JVGets handles file boundary correctly`` () =
    let client = new JvLinkStub([ Ok FileBoundary; Ok EndOfStream ]) :> IJvLinkClient

    Assert.Equal(Ok(), client.Init "test")
    Assert.True(Result.isOk (client.Open defaultRequest))

    let mutable buffer = ""
    let mutable filename = ""

    match client.Gets(&buffer, 1024, &filename) with
    | Ok bytesRead -> Assert.Equal(-1, bytesRead)
    | Error err -> failwithf "Expected file boundary marker, got error %A" err

[<Fact>]
let ``JVGets handles download pending correctly`` () =
    let client = new JvLinkStub([ Ok DownloadPending; Ok EndOfStream ]) :> IJvLinkClient

    Assert.Equal(Ok(), client.Init "test")
    Assert.True(Result.isOk (client.Open defaultRequest))

    let mutable buffer = ""
    let mutable filename = ""

    match client.Gets(&buffer, 1024, &filename) with
    | Ok bytesRead -> Assert.Equal(-3, bytesRead)
    | Error err -> failwithf "Expected download pending code, got error %A" err

[<Fact>]
let ``interpret maps success to Ok`` () =
    Assert.Equal(Ok(), interpret "JVInit" 0)

[<Fact>]
let ``interpret maps auth failure`` () =
    match interpret "JVInit" -303 with
    | Error(InvalidState _) -> assertCatalogEntry "JVInit" -303 JvErrorCategory.Authentication
    | other -> failwithf "unexpected mapping %A" other

[<Fact>]
let ``interpret maps maintenance error`` () =
    match interpret "JVOpen" -504 with
    | Error(InvalidState _) -> assertCatalogEntry "JVOpen" -504 JvErrorCategory.Maintenance
    | other -> failwithf "unexpected mapping %A" other

[<Fact>]
let ``interpret maps dataspec errors to InvalidInput`` () =
    match interpret "JVOpen" -111 with
    | Error(InvalidInput _) -> assertCatalogEntry "JVOpen" -111 JvErrorCategory.Input
    | other -> failwithf "expected InvalidInput, got %A" other

[<Fact>]
let ``interpret maps JVInit errors to NotInitialized`` () =
    match interpret "JVOpen" -201 with
    | Error NotInitialized -> assertCatalogEntry "JVOpen" -201 JvErrorCategory.Authentication
    | other -> failwithf "expected NotInitialized, got %A" other

[<Fact>]
let ``JvLinkService fetches all payloads from stub client`` () =
    let stubClient =
        [ [| 10uy |]; [| 20uy; 30uy |] ]
        |> Seq.map samplePayload
        |> Seq.toArray
        |> fun arr -> new JvLinkStub(arr)

    let config =
        match JvLinkConfig.create "TEST_SID" None None None with
        | Ok cfg -> cfg
        | Error err -> failwithf "unexpected config error %A" err

    let service = new JvLinkService(stubClient :> IJvLinkClient, config)

    match service.FetchPayloadsWithSize defaultRequest with
    | Ok(payloads, totalBytes) ->
        Assert.Equal(2, payloads.Length)
        Assert.Equal<byte[]>([| 10uy |], payloads.[0].Data)
        Assert.Equal<byte[]>([| 20uy; 30uy |], payloads.[1].Data)
        Assert.Equal(3L, totalBytes)
    | Error err -> failwithf "unexpected fetch error %A" err

[<Fact>]
let ``FetchPayloads retries when stub reports download pending`` () =
    let stubClient =
        [ Ok DownloadPending; samplePayload [| 0x42uy |]; Ok EndOfStream ]
        |> Seq.toArray
        |> fun arr -> new JvLinkStub(arr)

    let config =
        match JvLinkConfig.create "SID" None None None with
        | Ok cfg -> cfg
        | Error err -> failwithf "unexpected config error %A" err

    let service = new JvLinkService(stubClient :> IJvLinkClient, config)

    match service.FetchPayloadsWithSize defaultRequest with
    | Ok(payloads, totalBytes) ->
        Assert.Equal(1, payloads.Length)
        Assert.Equal<byte[]>([| 0x42uy |], payloads.[0].Data)
        Assert.Equal(1L, totalBytes)
    | Error err -> failwithf "download pending handling failed %A" err

[<Fact>]
let ``FetchPayloads ignores file boundary markers`` () =
    let stubClient =
        [ samplePayload [| 0x10uy |]
          Ok FileBoundary
          samplePayload [| 0x11uy |]
          Ok EndOfStream ]
        |> Seq.toArray
        |> fun arr -> new JvLinkStub(arr)

    let config =
        match JvLinkConfig.create "SID" None None None with
        | Ok cfg -> cfg
        | Error err -> failwithf "unexpected config error %A" err

    let service = new JvLinkService(stubClient :> IJvLinkClient, config)

    match service.FetchPayloads defaultRequest with
    | Ok payloads ->
        Assert.Equal(2, payloads.Length)
        Assert.Equal<byte[]>([| 0x10uy |], payloads.[0].Data)
        Assert.Equal<byte[]>([| 0x11uy |], payloads.[1].Data)
    | Error err -> failwithf "file boundary handling failed %A" err

[<Fact>]
let ``JvLinkService exposes control operations`` () =
    let stubClient =
        [ [| 1uy |]; [| 2uy |] ]
        |> Seq.map samplePayload
        |> Seq.toArray
        |> fun arr -> new JvLinkStub(arr)

    let config =
        match JvLinkConfig.create "SID" None None None with
        | Ok cfg -> cfg
        | Error err -> failwithf "unexpected config error %A" err

    let client = stubClient :> IJvLinkClient

    match client.Init "SID" with
    | Ok() -> ()
    | Error err -> failwithf "init failed %A" err

    match client.Open defaultRequest with
    | Ok _ -> ()
    | Error err -> failwithf "open failed %A" err

    let service = new JvLinkService(client, config)

    match service.GetStatus() with
    | Ok status -> Assert.True(status >= 0)
    | Error err -> failwithf "status failed %A" err

    match service.SkipCurrentFile() with
    | Ok() -> ()
    | Error err -> failwithf "skip failed %A" err

    match service.CancelDownload() with
    | Ok() -> ()
    | Error err -> failwithf "cancel failed %A" err

    match service.DeleteFile "dummy.txt" with
    | Ok() -> ()
    | Error err -> failwithf "delete failed %A" err

    client.Close()

[<Fact>]
let ``Realtime payloads can be streamed via stub`` () =
    let stubClient =
        [ [| 42uy |]; [| 43uy |] ]
        |> Seq.map samplePayload
        |> Seq.toArray
        |> fun arr -> new JvLinkStub(arr)

    let config =
        match JvLinkConfig.create "SID" None None None with
        | Ok cfg -> cfg
        | Error err -> failwithf "unexpected config error %A" err

    let service = new JvLinkService(stubClient :> IJvLinkClient, config)

    let payloads =
        service.StreamRealtimePayloads("REALTIME", "20240101")
        |> Seq.choose (function
            | Ok p -> Some p
            | Error err -> failwithf "unexpected realtime error %A" err)
        |> Seq.toList

    Assert.Equal(2, payloads.Length)
    Assert.Equal<byte[]>([| 42uy |], payloads.[0].Data)
    Assert.Equal<byte[]>([| 43uy |], payloads.[1].Data)

[<Fact>]
let ``WatchEvent parsing normalizes raw keys`` () =
    let raw = "０Ｂ１２20240101010101"
    let parsed = Serialization.parseWatchEvent raw

    match parsed.Event with
    | WatchEventType.PayoffConfirmed -> ()
    | other -> failwithf "unexpected event type %A" other

    Assert.Equal("0B1220240101010101", parsed.RawKey)
    Assert.Equal(DateTime(2024, 1, 1) |> Some, parsed.MeetingDate)
    Assert.Equal<string option>(Some "01", parsed.CourseCode)
    Assert.Equal<string option>(Some "01", parsed.RaceNumber)
    Assert.Equal<string option>(Some "01", parsed.ParticipantId)
    Assert.True(parsed.AdditionalData.IsNone)

    match WatchEvent.toRealtimeRequest parsed with
    | Some req ->
        Assert.Equal("0B12", req.Dataspec)
        Assert.Equal("0B1220240101010101", req.Key)
    | None -> failwith "Expected realtime request"

    let jockeyRaw = "0B16JC20240101990123"
    let jockey = Serialization.parseWatchEvent jockeyRaw

    match jockey.Event with
    | WatchEventType.JockeyChange -> ()
    | other -> failwithf "unexpected event type %A" other

    Assert.Equal(DateTime(2024, 1, 1) |> Some, jockey.MeetingDate)
    Assert.Equal<string option>(Some "99", jockey.CourseCode)
    Assert.Equal<string option>(Some "01", jockey.RaceNumber)
    Assert.Equal<string option>(Some "23", jockey.ParticipantId)
    Assert.Equal<string option>(Some "JC", jockey.RecordType)

    match WatchEvent.toRealtimeRequest jockey with
    | Some req ->
        Assert.Equal("0B16", req.Dataspec)
        Assert.Equal(jockey.RawKey, req.Key)
    | None -> failwith "Expected realtime request"

[<Fact>]
let ``Watch events stream publishes notifications`` () =
    let stubClient = new JvLinkStub(Seq.empty)

    let config =
        match JvLinkConfig.create "SID" None None None with
        | Ok cfg -> cfg
        | Error err -> failwithf "unexpected config error %A" err

    let service = new JvLinkService(stubClient :> IJvLinkClient, config)

    let received = ResizeArray<Result<WatchEvent, XanthosError>>()
    use subscription = service.WatchEvents.Subscribe(received.Add)

    match service.StartWatchEvents() with
    | Ok() -> ()
    | Error err -> failwithf "start watch events failed %A" err

    stubClient.RaiseEvent "0B1220240101010101"

    // Allow async event processing (events are queued to a background thread)
    Thread.Sleep(50)

    Assert.Equal(1, received.Count)

    match received.[0] with
    | Ok evt ->
        Assert.Equal(WatchEventType.PayoffConfirmed, evt.Event)
        Assert.Equal(DateTime(2024, 1, 1) |> Some, evt.MeetingDate)
        Assert.Equal<string option>(Some "01", evt.CourseCode)
        Assert.Equal<string option>(Some "01", evt.RaceNumber)
        Assert.Equal<string option>(Some "01", evt.ParticipantId)

        match WatchEvent.toRealtimeRequest evt with
        | Some req ->
            Assert.Equal("0B12", req.Dataspec)
            Assert.Equal(evt.RawKey, req.Key)
        | None -> failwith "Expected realtime request"
    | Error err -> failwithf "unexpected error %A" err

    match service.StopWatchEvents() with
    | Ok() -> ()
    | Error err -> failwithf "stop watch events failed %A" err

[<Fact>]
let ``Stopping watch events prevents further callbacks`` () =
    let stubClient = new JvLinkStub(Seq.empty)

    let config =
        match JvLinkConfig.create "SID" None None None with
        | Ok cfg -> cfg
        | Error err -> failwithf "unexpected config error %A" err

    let service = new JvLinkService(stubClient :> IJvLinkClient, config)

    let received = ResizeArray<Result<WatchEvent, XanthosError>>()
    use subscription = service.WatchEvents.Subscribe(received.Add)

    match service.StartWatchEvents() with
    | Ok() -> ()
    | Error err -> failwithf "start watch events failed %A" err

    stubClient.RaiseEvent "0B1620240101990101"

    match service.StopWatchEvents() with
    | Ok() -> ()
    | Error err -> failwithf "stop watch events failed %A" err

    stubClient.RaiseEvent "0B1620240101990102"

    Assert.Equal(1, received.Count)

[<Fact>]
let ``Course diagram retrieval returns stubbed values`` () =
    let stubClient = new JvLinkStub(Seq.empty)
    stubClient.ConfigureCourseFileResponse("C:\\course.gif", "Tokyo Turf 2400m")

    let config =
        match JvLinkConfig.create "SID" None None None with
        | Ok cfg -> cfg
        | Error err -> failwithf "unexpected config error %A" err

    let service = new JvLinkService(stubClient :> IJvLinkClient, config)

    match service.GetCourseDiagram("9999999905240011") with
    | Ok diagram ->
        Assert.Equal("C:\\course.gif", diagram.FilePath)
        Assert.Equal<string option>(Some "Tokyo Turf 2400m", diagram.Explanation)
    | Error err -> failwithf "CourseDiagram error %A" err

    // GetCourseDiagramBasic saves to specified filepath
    let outputPath = "C:\\output\\course.gif"

    match service.GetCourseDiagramBasic("9999999905240011", outputPath) with
    | Ok diagram ->
        Assert.Equal(outputPath, diagram.FilePath)
        Assert.Equal<string option>(None, diagram.Explanation)
    | Error err -> failwithf "CourseDiagramBasic error %A" err

[<Fact>]
let ``Silks file generation returns requested path`` () =
    let stubClient = new JvLinkStub(Seq.empty)
    let mutable receivedPattern = None

    stubClient.UseSilksFileResponder(fun (pattern, outputPath) ->
        receivedPattern <- Some pattern
        Ok(Some outputPath))

    let config =
        match JvLinkConfig.create "SID" None None None with
        | Ok cfg -> cfg
        | Error err -> failwithf "unexpected config error %A" err

    let service = new JvLinkService(stubClient :> IJvLinkClient, config)
    let inputPattern = "ｽｲｿﾞｸ,赤山形一本輪,水色袖"
    let outputPath = "C:\\silks\\pattern.bmp"

    match service.GenerateSilksFile(inputPattern, outputPath) with
    | Ok silks ->
        Assert.Equal<string option>(Some outputPath, silks.FilePath)
        Assert.True(silks.Data.IsNone)
    | Error err -> failwithf "Silks file generation error %A" err

    Assert.Equal(Some(Text.normalizeJvText inputPattern), receivedPattern)

[<Fact>]
let ``Silks binary retrieval returns image bytes`` () =
    let stubClient = new JvLinkStub(Seq.empty)
    let mutable receivedPattern = None
    let expectedBytes = [| 0x42uy; 0x4Duy; 0x36uy; 0x28uy |]

    stubClient.UseSilksBinaryResponder(fun pattern ->
        receivedPattern <- Some pattern
        Ok(Some expectedBytes))

    let config =
        match JvLinkConfig.create "SID" None None None with
        | Ok cfg -> cfg
        | Error err -> failwithf "unexpected config error %A" err

    let service = new JvLinkService(stubClient :> IJvLinkClient, config)
    let inputPattern = "水色,赤山形一本輪,水色袖"

    match service.GetSilksBinary(inputPattern) with
    | Ok silks ->
        Assert.True(silks.FilePath.IsNone)
        Assert.Equal<byte[]>(expectedBytes, silks.Data |> Option.defaultValue [||])
    | Error err -> failwithf "Silks binary retrieval error %A" err

    Assert.Equal(Some(Text.normalizeJvText inputPattern), receivedPattern)

[<Fact>]
let ``Control properties flow through runtime service`` () =
    let stubClient = new JvLinkStub(Seq.empty)
    stubClient.ConfigureTotalReadFileSize(16_384L)
    stubClient.ConfigureCurrentReadFileSize(4_096L)
    stubClient.ConfigureJVLinkVersion "0490"
    stubClient.ConfigureParentWindow(IntPtr(1234))
    stubClient.ConfigureSaveFlag false
    stubClient.ConfigurePayoffDialogSuppressed false

    let config =
        match JvLinkConfig.create "SID" None None None with
        | Ok cfg -> cfg
        | Error err -> failwithf "unexpected config error %A" err

    let service = new JvLinkService(stubClient :> IJvLinkClient, config)

    match service.GetJVLinkVersion() with
    | Ok version -> Assert.Equal("0490", version)
    | Error err -> failwithf "GetJVLinkVersion failed %A" err

    match service.GetTotalReadFileSize() with
    | Ok size -> Assert.Equal(16_384L, size)
    | Error err -> failwithf "GetTotalReadFileSize failed %A" err

    match service.GetCurrentReadFileSize() with
    | Ok size -> Assert.Equal(4_096L, size)
    | Error err -> failwithf "GetCurrentReadFileSize failed %A" err

    match service.GetCurrentFileTimestamp() with
    | Ok ts -> Assert.True(ts.IsNone)
    | Error err -> failwithf "GetCurrentFileTimestamp failed %A" err

    match service.GetParentWindowHandle() with
    | Ok handle -> Assert.Equal(IntPtr(1234), handle)
    | Error err -> failwithf "GetParentWindowHandle failed %A" err

    match service.GetSaveDownloadsEnabled() with
    | Ok value -> Assert.False(value)
    | Error err -> failwithf "GetSaveDownloadsEnabled failed %A" err

    match service.GetPayoffDialogSuppressed() with
    | Ok value -> Assert.False(value)
    | Error err -> failwithf "GetPayoffDialogSuppressed failed %A" err

    match service.SetSaveDownloadsEnabled true with
    | Ok() -> ()
    | Error err -> failwithf "SetSaveDownloadsEnabled failed %A" err

    match service.GetSaveDownloadsEnabled() with
    | Ok value -> Assert.True(value)
    | Error err -> failwithf "GetSaveDownloadsEnabled failed %A" err

    match service.SetPayoffDialogSuppressed true with
    | Ok() -> ()
    | Error err -> failwithf "SetPayoffDialogSuppressed failed %A" err

    match service.GetPayoffDialogSuppressed() with
    | Ok value -> Assert.True(value)
    | Error err -> failwithf "GetPayoffDialogSuppressed failed (after set) %A" err

    let newHandle = IntPtr(5678)

    match service.SetParentWindowHandle newHandle with
    | Ok() -> ()
    | Error err -> failwithf "SetParentWindowHandle failed %A" err

    match service.GetParentWindowHandle() with
    | Ok handle -> Assert.Equal(newHandle, handle)
    | Error err -> failwithf "GetParentWindowHandle failed (after set) %A" err

    match service.ShowConfigurationDialog() with
    | Ok() -> Assert.Equal(1, stubClient.UiDialogInvocationCount)
    | Error err -> failwithf "ShowConfigurationDialog failed %A" err

    match service.SetServiceKey "12345678901234567" with
    | Ok() -> Assert.Equal(Some "12345678901234567", stubClient.LastServiceKey)
    | Error err -> failwithf "SetServiceKey failed %A" err

[<Fact>]
let ``All IJvLinkClient properties read and write correctly`` () =
    let stubClient = new JvLinkStub(Seq.empty) :> IJvLinkClient

    stubClient.SaveFlag <- true
    Assert.True(stubClient.SaveFlag)
    stubClient.SaveFlag <- false
    Assert.False(stubClient.SaveFlag)

    stubClient.SavePath <- "C:\\temp\\jvdata"
    Assert.Equal("C:\\temp\\jvdata", stubClient.SavePath)

    stubClient.ServiceKey <- "TESTKEY123456789"
    Assert.Equal("TESTKEY123456789", stubClient.ServiceKey)

    let version = stubClient.JVLinkVersion
    Assert.False(String.IsNullOrWhiteSpace version)

    stubClient.ParentWindowHandle <- IntPtr(9999)
    Assert.Equal(IntPtr(9999), stubClient.ParentWindowHandle)

    stubClient.PayoffDialogSuppressed <- true
    Assert.True(stubClient.PayoffDialogSuppressed)
    stubClient.PayoffDialogSuppressed <- false
    Assert.False(stubClient.PayoffDialogSuppressed)

[<Fact>]
let ``Property TotalReadFileSize returns expected value`` () =
    let stubClient = new JvLinkStub(Seq.empty, totalSize = 123_456L) :> IJvLinkClient
    Assert.Equal(123_456L, stubClient.TotalReadFileSize)

[<Fact>]
let ``Property CurrentReadFileSize updates during read operations`` () =
    let stubClient = new JvLinkStub(Seq.empty)
    stubClient.ConfigureCurrentReadFileSize(8_192L)
    let client = stubClient :> IJvLinkClient
    Assert.Equal(8_192L, client.CurrentReadFileSize)

[<Fact>]
let ``Property CurrentFileTimestamp returns expected value`` () =
    let timestamp = DateTime(2024, 11, 26, 10, 30, 0)

    let stubClient =
        new JvLinkStub(Seq.empty, initialTimestamp = timestamp) :> IJvLinkClient

    Assert.Equal(Some timestamp, stubClient.CurrentFileTimestamp)

[<Fact>]
let ``Movie availability delegates to stub responders`` () =
    let stubClient = new JvLinkStub(Seq.empty)

    stubClient.UseMovieAvailabilityResponder(fun key ->
        if key = "RACEKEY" then
            Ok MovieAvailability.Available
        else
            Ok MovieAvailability.Unavailable)

    stubClient.UseMovieAvailabilityWithTypeResponder(fun (movieType, key) ->
        if movieType = "01" && key = "PADDOCK" then
            Ok MovieAvailability.Available
        else
            Ok MovieAvailability.Unavailable)

    let config =
        match JvLinkConfig.create "SID" None None None with
        | Ok cfg -> cfg
        | Error err -> failwithf "unexpected config error %A" err

    let service = new JvLinkService(stubClient :> IJvLinkClient, config)

    match service.CheckMovieAvailability "RACEKEY" with
    | Ok availability -> Assert.Equal(MovieAvailability.Available, availability)
    | Error err -> failwithf "CheckMovieAvailability failed %A" err

    match service.CheckMovieAvailability(MovieType.PaddockVideo, "PADDOCK") with
    | Ok availability -> Assert.Equal(MovieAvailability.Available, availability)
    | Error err -> failwithf "CheckMovieAvailability with type failed %A" err

[<Fact>]
let ``Movie playback delegates to stub responders`` () =
    let stubClient = new JvLinkStub(Seq.empty)
    let mutable racePlay = false
    let mutable typedPlay = false

    stubClient.UseMoviePlayResponder(fun (key, movieType) ->
        match key, movieType with
        | "RACEKEY", None ->
            racePlay <- true
            Ok()
        | "PADDOCK", Some "01" ->
            typedPlay <- true
            Ok()
        | _ -> Error(Unexpected "Unexpected playback parameters"))

    let config =
        match JvLinkConfig.create "SID" None None None with
        | Ok cfg -> cfg
        | Error err -> failwithf "unexpected config error %A" err

    let service = new JvLinkService(stubClient :> IJvLinkClient, config)

    match service.PlayMovie "RACEKEY" with
    | Ok() -> Assert.True racePlay
    | Error err -> failwithf "PlayMovie failed %A" err

    match service.PlayMovie(MovieType.PaddockVideo, "PADDOCK") with
    | Ok() -> Assert.True typedPlay
    | Error err -> failwithf "PlayMovie with type failed %A" err

[<Fact>]
let ``Workout video listings can be fetched via service`` () =
    let stubClient = new JvLinkStub(Seq.empty)
    stubClient.UseMoviePlayResponder(fun _ -> Ok())
    stubClient.EnqueueMovieReadOutcome(Ok(MovieRecord(WorkoutVideoListing.parse "202401019876543210")))
    stubClient.EnqueueMovieReadOutcome(Ok MovieEnd)

    let config =
        match JvLinkConfig.create "SID" None None None with
        | Ok cfg -> cfg
        | Error err -> failwithf "unexpected config error %A" err

    let service = new JvLinkService(stubClient :> IJvLinkClient, config)

    match service.FetchWorkoutVideos(MovieType.WorkoutWeekAll, "20240101") with
    | Ok listings ->
        Assert.Equal(1, listings.Length)
        let listing = listings.Head
        Assert.Equal("202401019876543210", listing.RawKey)
        Assert.True(listing.WorkoutDate.IsSome)
        Assert.Equal<string option>(Some "9876543210", listing.RegistrationId)
    | Error err -> failwithf "FetchWorkoutVideos failed %A" err

[<Fact>]
let ``StreamRealtimePayloads yields lazy realtime sequence`` () =
    let stubClient =
        [ [| 1uy |]; [| 2uy |] ]
        |> Seq.map samplePayload
        |> Seq.toArray
        |> fun arr -> new JvLinkStub(arr)

    let config =
        match JvLinkConfig.create "SID" None None None with
        | Ok cfg -> cfg
        | Error err -> failwithf "unexpected config error %A" err

    let service = new JvLinkService(stubClient :> IJvLinkClient, config)

    let results = service.StreamRealtimePayloads("REALTIME", "20240101") |> Seq.toList

    Assert.Equal(
        2,
        results
        |> List.choose (function
            | Ok payload -> Some payload.Data
            | _ -> None)
        |> List.length
    )

    Assert.Contains(
        results,
        (fun r ->
            match r with
            | Ok payload -> payload.Data = [| 1uy |]
            | _ -> false)
    )

[<Fact>]
let ``StreamRealtimeAsync reads pre-enqueued data then terminates on EndOfStream`` () : Task =
    task {
        // Create stub with initial payload followed by EndOfStream
        let stubClient =
            new JvLinkStub([ Ok(Payload { Timestamp = None; Data = [| 42uy |] }); Ok EndOfStream ])

        let config =
            match JvLinkConfig.create "SID" None None None with
            | Ok cfg -> cfg
            | Error err -> failwithf "unexpected config error %A" err

        let service = new JvLinkService(stubClient :> IJvLinkClient, config)

        use cts = new CancellationTokenSource(TimeSpan.FromSeconds 2.)

        let stream =
            service.StreamRealtimeAsync(
                "REALTIME",
                "20240101",
                TimeSpan.FromMilliseconds 10.,
                cancellationToken = cts.Token
            )

        use enumerator = stream.GetAsyncEnumerator()

        // First MoveNextAsync should return the payload
        let! hasFirst = enumerator.MoveNextAsync().AsTask()
        Assert.True(hasFirst)

        match enumerator.Current with
        | Ok payload -> Assert.Equal<byte[]>([| 42uy |], payload.Data)
        | Error err -> failwithf "unexpected realtime error %A" err

        // Second MoveNextAsync should return false (EndOfStream terminates the stream)
        let! hasSecond = enumerator.MoveNextAsync().AsTask()
        Assert.False(hasSecond)

        do! enumerator.DisposeAsync().AsTask()
    }

[<Fact>]
let ``decodeShiftJis converts bytes to unicode`` () =
    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)
    let source = "ｶﾀｶﾅ123"
    let bytes = Encoding.GetEncoding("shift_jis").GetBytes(source)
    let decoded = Text.decodeShiftJis bytes
    Assert.Equal(source, decoded)

[<Fact>]
let ``decodeShiftJis falls back to utf8`` () =
    let source = "カタカナ123"
    let bytes = Encoding.UTF8.GetBytes(source)
    let decoded = Text.decodeShiftJis bytes
    Assert.Equal(source, decoded)

[<Fact>]
let ``normalizeJvText expands half-width katakana and digits`` () =
    let input = "ｶﾀｶﾅ１２３ABC"
    let expected = "カタカナ123ABC"
    Assert.Equal(expected, Text.normalizeJvText input)

[<Fact>]
let ``parseRaceCard normalizes shift-jis payload`` () =
    // RaceId must start with valid yyyyMMdd date format
    // Using full-width digits that will be normalized to half-width
    let legacyJson =
        """
        [
          {
            "id": "２０２４１００１０６０１０８０１",
            "name": "ｶﾀｶﾅ１２３",
            "course": "ﾄｳｷｮｳ",
            "distanceMeters": 1800,
            "surface": "Dirt",
            "condition": "Good",
            "scheduledStart": "2024-10-01T12:34:56Z"
          }
        ]
        """

    let payload = Text.encodeShiftJis legacyJson

    match Serialization.parseRaceCard payload with
    | Ok [ race ] ->
        Assert.Equal("2024100106010801", race.Id |> RaceId.value)
        Assert.Equal("カタカナ123", race.Name)
        Assert.Equal<string option>(Some "トウキョウ", race.Course)
        Assert.Equal(1800 |> Some, race.DistanceMeters)
        Assert.Equal(TrackSurface.Dirt, race.Surface)
        Assert.Equal(TrackCondition.Good, race.Condition)
    | Ok other -> failwithf "Unexpected race card result %A" other
    | Error err -> failwithf "parseRaceCard failed %A" err

[<Fact>]
let ``parseOdds normalizes shift-jis payload`` () =
    // RaceId must start with valid yyyyMMdd date format
    // RunnerId must be exactly 10 digits
    let legacyJson =
        """
        [
          {
            "raceId": "２０２４１００１０６０１０８０１",
            "timestamp": "2024-10-01T12:34:56Z",
            "entries": [
              { "runnerId": "１２３４５６７８９０", "winOdds": 1.5, "placeOdds": 0.8 },
              { "runnerId": "０９８７６５４３２１", "winOdds": null, "placeOdds": null }
            ]
          }
        ]
        """

    let payload = Text.encodeShiftJis legacyJson

    match Serialization.parseOdds payload with
    | Ok [ snapshot ] ->
        Assert.Equal("2024100106010801", snapshot.Race |> RaceId.value)
        Assert.Equal(2, snapshot.Entries.Length)
        let first = snapshot.Entries.[0]
        Assert.Equal("1234567890", first.Runner |> RunnerId.value)
        Assert.Equal(Some 1.5M, first.WinOdds)
        Assert.Equal(Some 0.8M, first.PlaceOdds)
    | Ok other -> failwithf "Unexpected odds result %A" other
    | Error err -> failwithf "parseOdds failed %A" err

module StreamRealtimeBoundaryTests =
    open System
    open Xunit
    open Xanthos.Runtime
    open Xanthos.Core
    open Xanthos.Interop

    let private samplePayload bytes =
        Ok(JvReadOutcome.Payload { Timestamp = None; Data = bytes })

    [<Fact>]
    let ``StreamRealtimePayloads handles consecutive FileBoundary without yielding payloads between`` () =
        // Arrange: consecutive file boundaries then a payload and EOF
        let stubClient =
            [ Ok FileBoundary; Ok FileBoundary; samplePayload [| 0xAAuy |]; Ok EndOfStream ]
            |> Seq.toArray
            |> fun arr -> new JvLinkStub(arr)

        let config =
            match JvLinkConfig.create "SID" None None None with
            | Ok cfg -> cfg
            | Error err -> failwithf "unexpected config error %A" err

        let service = new JvLinkService(stubClient :> IJvLinkClient, config)

        // Act
        let results = service.StreamRealtimePayloads("REALTIME", "20240101") |> Seq.toList

        // Assert: only one payload should be yielded
        let payloads =
            results
            |> List.choose (function
                | Ok p -> Some p.Data
                | _ -> None)

        Assert.Equal(1, payloads.Length)
        Assert.Equal<byte[]>([| 0xAAuy |], payloads.Head)

    [<Fact>]
    let ``StreamRealtimePayloads backs off during prolonged DownloadPending and eventually yields payload`` () =
        // Arrange: multiple DownloadPending signals to exercise backoff, then a payload and EOF
        let stubClient =
            [ Ok DownloadPending
              Ok DownloadPending
              Ok DownloadPending
              samplePayload [| 0x42uy |]
              Ok EndOfStream ]
            |> Seq.toArray
            |> fun arr -> new JvLinkStub(arr)

        let config =
            match JvLinkConfig.create "SID" None None None with
            | Ok cfg -> cfg
            | Error err -> failwithf "unexpected config error %A" err

        let service = new JvLinkService(stubClient :> IJvLinkClient, config)

        // Act
        let results = service.StreamRealtimePayloads("REALTIME", "20240101") |> Seq.toList

        // Assert: eventual payload should be present once
        let payloads =
            results
            |> List.choose (function
                | Ok p -> Some p.Data
                | _ -> None)

        Assert.Equal(1, payloads.Length)
        Assert.Equal<byte[]>([| 0x42uy |], payloads.Head)

module Xanthos.UnitTests.ComGetsMappingTests

open System
open Xunit
open Xanthos.Core
open Xanthos.Interop

let private mkPayload (text: string) =
    { Timestamp = Some DateTime.UtcNow
      Data = Xanthos.Core.Text.encodeShiftJis text }

[<Fact>]
let ``Stub.Gets should return byte length and decode buffer`` () =
    let payload = mkPayload "テスト行1"
    let stub = JvLinkStub.FromPayloads [ payload.Data ]
    let mutable buff = ""
    let mutable fname = ""

    match
        (stub :> IJvLinkClient)
            .Open(
                { Spec = "RACE"
                  FromTime = DateTime(2024, 1, 1)
                  Option = 1 }
            )
    with
    | Error e -> failwithf "Open failed: %A" e
    | Ok _ ->
        let result = (stub :> IJvLinkClient).Gets(&buff, 1024, &fname)

        match result with
        | Ok n when n > 0 ->
            Assert.True(n > 0)
            Assert.NotEmpty(buff)
            Assert.Equal("stubfile.jvd", fname)
        | other -> failwithf "Unexpected result %A" other

[<Fact>]
let ``Stub.Gets should map file boundary to -1`` () =
    let stub = new JvLinkStub()
    stub.Enqueue(Ok FileBoundary)
    let mutable buff = ""
    let mutable fname = ""
    let client = stub :> IJvLinkClient
    ignore (client.Init "sid")

    ignore (
        client.Open(
            { Spec = "RACE"
              FromTime = DateTime(2024, 1, 1)
              Option = 1 }
        )
    )

    match client.Gets(&buff, 1024, &fname) with
    | Ok code -> Assert.Equal(-1, code)
    | Error e -> failwithf "Expected -1, got error %A" e

[<Fact>]
let ``Stub.Gets should map download pending to -3`` () =
    let stub = new JvLinkStub()
    stub.Enqueue(Ok DownloadPending)
    let client = stub :> IJvLinkClient
    ignore (client.Init "sid")

    ignore (
        client.Open(
            { Spec = "RACE"
              FromTime = DateTime(2024, 1, 1)
              Option = 1 }
        )
    )

    let mutable buff = ""
    let mutable fname = ""

    match client.Gets(&buff, 1024, &fname) with
    | Ok code -> Assert.Equal(-3, code)
    | Error e -> failwithf "Expected -3, got error %A" e

[<Fact>]
let ``ErrorCodes.interpret should map JVRead negatives to ComError`` () =
    // -401 internal error
    match Xanthos.Core.ErrorCodes.interpret "JVRead" -401 with
    | Error(Unexpected msg) -> Assert.Contains("internal", msg.ToLowerInvariant())
    | other -> failwithf "Unexpected mapping %A" other

#if WINDOWS
let private tryCreateClient () =
    try
        match ComClientFactory.tryCreate None with
        | Ok client -> Some client
        | Error _ -> None
    with _ ->
        None

[<Fact>]
let ``ComJvLinkClient.Gets should activate and not crash even if COM unavailable or uninitialised`` () =
    match tryCreateClient () with
    | None -> () // COM not available or ProgID not registered; skip at runtime
    | Some client ->
        let mutable buff = ""
        let mutable fname = ""
        let result = client.Gets(&buff, 128, &fname)

        match result with
        | Ok _ -> () // Any successful code (>=0, -1, -3) acceptable
        | Error NotInitialized -> () // Expected if JVInit not called
        | Error(InvalidState _) -> () // Read sequence state errors acceptable pre-Init/Open
        | Error other -> failwithf "Unexpected Gets error %A" other

        client.Close()
#else
[<Fact>]
let ``ComJvLinkClient.Gets mapping skipped on non-Windows`` () = ()
#endif

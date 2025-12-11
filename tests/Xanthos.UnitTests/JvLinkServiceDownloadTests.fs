module Xanthos.UnitTests.JvLinkServiceDownloadTests

open System
open System.Threading
open Xunit
open Xanthos.Core
open Xanthos.Interop
open Xanthos.Runtime
open Xanthos.Runtime.ServiceExtensions

[<Fact>]
let ``WaitForDownloads should return completed count using stub Status`` () =
    // Prepare stub with three payloads so Status will increment as Read proceeds
    let p bytes =
        Ok(
            Payload
                { Timestamp = Some DateTime.UtcNow
                  Data = bytes }
        )

    let payloads =
        [| Text.encodeShiftJis "A"; Text.encodeShiftJis "B"; Text.encodeShiftJis "C" |]

    let responses =
        seq {
            yield p payloads.[0]
            yield p payloads.[1]
            yield p payloads.[2]
            yield Ok EndOfStream
        }

    let stub = new JvLinkStub(responses)
    // Create service
    let config =
        { Sid = "sid"
          SavePath = None
          ServiceKey = None
          UseJvGets = None }

    let service = new JvLinkService(stub :> IJvLinkClient, config, TraceLogger.silent)
    // Initialise and open so Status becomes meaningful
    match
        service.FetchPayloads(
            { Spec = "RACE"
              FromTime = DateTime(2024, 1, 1)
              Option = 1 }
        )
    with
    | Ok payloads -> Assert.Equal(3, payloads.Length)
    | Error e -> failwithf "Setup FetchPayloads failed %A" e
    // Wait for completion (already completed but exercise the path) via extension member
    match
        service.WaitForDownloads(3, pollInterval = TimeSpan.FromMilliseconds 5., timeout = TimeSpan.FromSeconds 1.)
    with
    | Ok(Completed completed) -> Assert.Equal(3, completed)
    | Ok(TimedOut _) -> failwith "Unexpected timeout - should have completed"
    | Error err -> failwithf "Unexpected error %A" err

module Xanthos.UnitTests.ComAbnormalTests

open System
open Xunit
open Xanthos.Core
open Xanthos.Interop

[<Fact>]
let ``Stub.Gets should handle empty SAFEARRAY bytes`` () =
    // Empty payload
    let payload =
        { Timestamp = Some DateTime.UtcNow
          Data = Array.empty<byte> }

    let stub = new JvLinkStub(Seq.singleton (Ok(Payload payload)))
    let client = stub :> IJvLinkClient
    ignore (client.Init "sid")

    ignore (
        client.Open(
            { Spec = "TEST"
              FromTime = DateTime(2024, 1, 1)
              Option = 1 }
        )
    )

    let mutable buff = ""
    let mutable fname = ""

    match client.Gets(&buff, 1024, &fname) with
    | Ok n ->
        Assert.Equal(0, n)
        Assert.Equal("", buff)
    | Error e -> failwithf "Unexpected error %A" e

[<Fact>]
let ``Stub.Gets should handle large SAFEARRAY bytes`` () =
    // Large payload
    let text = String.replicate 10000 "あ"

    let payload =
        { Timestamp = Some DateTime.UtcNow
          Data = Text.encodeShiftJis text }

    let stub = new JvLinkStub(Seq.singleton (Ok(Payload payload)))
    let client = stub :> IJvLinkClient
    ignore (client.Init "sid")

    ignore (
        client.Open(
            { Spec = "TEST"
              FromTime = DateTime(2024, 1, 1)
              Option = 1 }
        )
    )

    let mutable buff = ""
    let mutable fname = ""

    match client.Gets(&buff, 65536, &fname) with
    | Ok n ->
        Assert.True(n > 0)
        Assert.True(buff.Length > 0)
    | Error e -> failwithf "Unexpected error %A" e

[<Fact>]
let ``Stub.Read should propagate CommunicationFailure`` () =
    let stub = new JvLinkStub()
    stub.Enqueue(Error(CommunicationFailure(-401, "internal error")))
    let client = stub :> IJvLinkClient
    ignore (client.Init "sid")

    ignore (
        client.Open(
            { Spec = "TEST"
              FromTime = DateTime(2024, 1, 1)
              Option = 1 }
        )
    )

    match client.Read() with
    | Error(CommunicationFailure(code, _)) -> Assert.Equal(-401, code)
    | other -> failwithf "Unexpected outcome %A" other

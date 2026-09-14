namespace Xanthos.UnitTests

open System
open Xunit
open Xanthos
open Xanthos.Data
open Xanthos.Core
open Xanthos.Interop
open Xanthos.Runtime

module ReleaseCompatibilityTests =
    let private ok =
        function
        | Ok value -> value
        | Error error -> failwithf "%A" error

    [<Theory; Trait("Category", "Contract")>]
    [<InlineData("DIFF");
      InlineData("BLOD");
      InlineData("SNAP");
      InlineData("HOSE");
      InlineData("TCOV");
      InlineData("RCOV")>]
    let ``Legacy service parsing options retain old identifier records and partial successes`` spec =
        let _, bytes, _ = IdentifierFormatTests.fixture "UM"
        let payload: JvPayload = { Timestamp = None; Data = bytes }

        let race: JvPayload =
            { Timestamp = None
              Data = RecordOracle.blank (RecordOracle.layout "RA") }

        let bad: JvPayload = { Timestamp = None; Data = [||] }
        let options = DataSpecs.parseOptions spec DateOnly.MaxValue |> Option.get
        Assert.Equal(IdentifierFormat.Legacy, options.IdentifierFormat)
        Assert.True(JvLinkService.ParsePayload payload |> Result.isError)
        let expected = JvLinkService.ParsePayloadWith(options, payload) |> ok

        match expected with
        | UMRecord horse -> Assert.Equal<byte>(bytes, horse.Raw)
        | other -> failwithf "Wrong projection: %A" other

        let records = JvLinkService.ParsePayloadsWith(options, [ race; payload ]) |> ok
        Assert.Equal(2, records.Length)
        Assert.Equal(expected, records[1])

        let good, errors =
            JvLinkService.TryParsePayloadsWith(options, [ race; bad; payload ])

        Assert.Equal<ParsedRecord>(records, good)
        Assert.Equal(bad, (Assert.Single errors |> fst))

        Assert.True(
            JvLinkService.ParsePayloadsWith(options, [ race; bad; payload ])
            |> Result.isError
        )

        let config =
            { JvLinkConfig.Sid = "controlled"
              SavePath = None
              ServiceKey = None
              UseJvGets = Some true }

        let request: JvOpenRequest =
            { Spec = spec
              FromTime = DateTime(2026, 9, 12)
              Option = 1 }

        use service = new JvLinkService(JvLinkStub.FromPayloads [ bytes ], config)
        Assert.Equal(expected, service.FetchTypedRecordsWith(request, options) |> ok |> Assert.Single)
        // The read protocol uses an empty buffer as EOF; use a nonempty malformed record.
        use failures =
            new JvLinkService(JvLinkStub.FromPayloads [ race.Data; [| byte 'U'; byte 'M' |]; bytes ], config)

        let fetched, rejected =
            failures.FetchTypedRecordsCollectErrorsWith(request, options) |> ok

        Assert.Equal<ParsedRecord>(records, fetched)
        Assert.Single rejected |> ignore

    [<Fact; Trait("Category", "Contract")>]
    let ``Legacy service exposes historical odds interpretation without changing default parsing`` () =
        let layout, bytes = OddsContractTests.fixture "O1"
        RecordOracle.write layout "18.b" 0 "0999" bytes
        let payload: JvPayload = { Timestamp = None; Data = bytes }

        let options =
            { Records.ParseOptions.Default with
                OddsLimitFormat = OddsLimitFormat.Before20040814 }

        let minimum =
            function
            | O1Record odds -> odds.Place[0].Minimum.Value
            | _ -> failwith "Expected O1"

        Assert.Equal(OddsValue.AtLeast 99.9M, JvLinkService.ParsePayloadWith(options, payload) |> ok |> minimum)
        Assert.Equal(OddsValue.Quoted 99.9M, JvLinkService.ParsePayload payload |> ok |> minimum)

    [<Theory; Trait("Category", "Contract")>]
    [<InlineData(null, ""); InlineData("", ""); InlineData("sdk-value", "sdk-value")>]
    let ``String SDK properties normalize null BSTR without losing nonempty values`` (value: string) expected =
        use native = new NativeFixture()

        use session =
            new Session(
                { new INativeJvLink with
                    member _.Get _ = Ok(box value)

                    member _.Invoke(name, args, indices) =
                        (native :> INativeJvLink).Invoke(name, args, indices)

                    member _.Put(name, value) =
                        (native :> INativeJvLink).Put(name, value)

                    member _.Watch callback =
                        (native :> INativeJvLink).Watch callback

                    member _.StopWatch() = (native :> INativeJvLink).StopWatch()
                    member _.Dispose() = () }
            )

        for getter in
            [ JvLink.getSavePath
              JvLink.getServiceKey
              JvLink.getVersion
              JvLink.getCurrentFileTimestamp ] do
            Assert.Equal(Ok expected, getter session)

        Assert.True(JvLink.getSaveFlag session |> Result.isError)

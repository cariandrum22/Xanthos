namespace Xanthos.UnitTests

open System
open System.IO
open System.Text.Json
open Xunit
open Xanthos

module DataSpecContractTests =
    [<Theory; Trait("Category", "Contract")>]
    [<InlineData("DIFFRACE", "UM", true);
      InlineData("RACEDIFF", "BR", true);
      InlineData("DIFFBLDN", "HN", false);
      InlineData("DIFFBLDN", "UM", true)>]
    let ``Combined dataspec parsing selects the convention for each record kind`` spec id legacy =
        match DataSpecs.parseOptionsForRecord spec id (DateOnly(2004, 8, 13)) with
        | Ok options ->
            Assert.Equal(
                (if legacy then
                     Data.IdentifierFormat.Legacy
                 else
                     Data.IdentifierFormat.Expanded),
                options.IdentifierFormat
            )

            Assert.Equal(Data.OddsLimitFormat.Before20040814, options.OddsLimitFormat)
        | Error message -> failwith message

    [<Theory; Trait("Category", "Contract")>]
    [<InlineData("DIFFDIFN", "UM"); InlineData("BLODBLDN", "HN"); InlineData("SNAPSNPN", "CK")>]
    let ``Ambiguous identifier streams are rejected before acquisition and during option selection`` spec id =
        let request =
            { Dataspec = spec
              FromTime = DateTime(2026, 9, 12)
              ToTime = None
              Option = 1 }

        for result in
            [ DataSpecs.validateOpen request
              DataSpecs.parseOptionsForRecord spec id DateOnly.MaxValue |> Result.map ignore ] do
            match result with
            | Error message -> Assert.Contains("Ambiguous identifier formats", message)
            | Ok _ -> failwith "Mixed widths were guessed"

    [<Fact; Trait("Category", "Contract")>]
    let ``Unknown records remain preservable while unknown streams and unexplained identifier layouts fail`` () =
        Assert.True(DataSpecs.parseOptionsForRecord "DIFFRACE" "ZZ" DateOnly.MaxValue |> Result.isOk)

        Assert.True(
            DataSpecs.parseOptionsForRecord "RACEZZZZ" "RA" DateOnly.MaxValue
            |> Result.isError
        )

        Assert.True(DataSpecs.parseOptionsForRecord "RACE" "UM" DateOnly.MaxValue |> Result.isError)

    [<Fact; Trait("Category", "Contract")>]
    let ``Every dataspec record set and open option matches the independent spreadsheet`` () =
        use doc =
            JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Contracts", "dataspecs.json")))

        let definitions =
            doc.RootElement.GetProperty("definitions").EnumerateArray() |> Seq.toArray

        Assert.Equal(38, definitions.Length)
        Assert.Equal(definitions.Length, DataSpecs.all.Length)

        for d in definitions do
            let id = d.GetProperty("id").GetString()
            let actual = DataSpecs.tryFind id |> Option.get

            let strings (name: string) =
                d.GetProperty(name).EnumerateArray() |> Seq.map _.GetString() |> Set.ofSeq

            let realtime = strings "realtime"
            Assert.Equal<Set<string>>((if realtime.IsEmpty then strings "normal" else realtime), actual.RecordIds)
            Assert.Equal<Set<string>>(strings "setup", actual.SetupRecordIds)
            Assert.Equal(not realtime.IsEmpty, actual.IsRealtime)

            let options =
                d.GetProperty("options").EnumerateArray() |> Seq.map _.GetInt32() |> Set.ofSeq

            Assert.Equal<Set<int>>(options, actual.OpenOptions)

        let ids = DataSpecs.all |> Seq.collect _.RecordIds |> Set.ofSeq
        Assert.Equal(38, ids.Count)
        Assert.DoesNotContain("H5", ids)

        Assert.True(
            ids
            |> Seq.forall (fun id ->
                match RecordKinds.ofId id with
                | RecordKind.Unknown _ -> false
                | _ -> true)
        )

    [<Fact; Trait("Category", "Contract")>]
    let ``Preflight handles combined specs prohibited end times realtime and invalid options`` () =
        let request =
            { Dataspec = "RACESNPN"
              FromTime = DateTime(2026, 9, 5)
              ToTime = Some(DateTime(2026, 9, 12))
              Option = 1 }

        Assert.Equal(Ok(), DataSpecs.validateOpen request)

        for spec in [ "TOKU"; "DIFF"; "DIFN"; "HOSE"; "HOSN"; "HOYU"; "COMM" ] do
            Assert.True(DataSpecs.validateOpen { request with Dataspec = spec } |> Result.isError)

            Assert.Equal(
                Ok(),
                DataSpecs.validateOpen
                    { request with
                        Dataspec = spec
                        ToTime = None }
            )

        for spec, option in [ "RCVN", 1; "BLDN", 2; "RACE", 0; "0B11", 1; "ZZZZ", 1; "RAC", 1 ] do
            Assert.True(
                DataSpecs.validateOpen
                    { request with
                        Dataspec = spec
                        Option = option }
                |> Result.isError
            )

        Assert.True(
            DataSpecs.validateOpen
                { request with
                    ToTime = Some(DateTime(2026, 9, 4)) }
            |> Result.isError
        )

        Assert.Equal(
            Ok(),
            DataSpecs.validateOpen
                { request with
                    ToTime = Some request.FromTime }
        )

    [<Fact; Trait("Category", "Contract")>]
    let ``Identifier format follows acquisition stream and odds format follows race date`` () =
        for old, current in
            [ "DIFF", "DIFN"
              "BLOD", "BLDN"
              "SNAP", "SNPN"
              "HOSE", "HOSN"
              "TCOV", "TCVN"
              "RCOV", "RCVN" ] do
            let a = DataSpecs.parseOptions old (DateOnly(2026, 9, 12)) |> Option.get
            let b = DataSpecs.parseOptions current (DateOnly(2004, 8, 13)) |> Option.get
            Assert.Equal(Data.IdentifierFormat.Legacy, a.IdentifierFormat)
            Assert.Equal(Data.IdentifierFormat.Expanded, b.IdentifierFormat)
            Assert.Equal(Data.OddsLimitFormat.Before20040814, b.OddsLimitFormat)
            Assert.Equal(Some(DateOnly(2023, 8, 8)), (DataSpecs.tryFind current |> Option.get).DeliveryStart)

        let after = DataSpecs.parseOptions "RACE" (DateOnly(2004, 8, 14)) |> Option.get
        Assert.Equal(Data.OddsLimitFormat.Current, after.OddsLimitFormat)
        Assert.Equal(None, DataSpecs.parseOptions "ZZZZ" (DateOnly(2026, 9, 12)))

namespace Xanthos.UnitTests

open System
open System.IO
open System.Text
open System.Text.Json
open Xunit
open Xanthos

module OfficialFoundationTests =
    [<Theory>]
    [<InlineData(537)>]
    [<InlineData(545)>]
    let ``Breeder header stays byte aligned across documented legacy and current lengths`` length =
        // 537-byte BR: change-history row 317; 545-byte BR: current format.
        // This verifies only the common prefix, not selection/validation of either body layout.
        let bytes = Array.create length 32uy
        Encoding.ASCII.GetBytes("BR120230808").CopyTo(bytes, 0)
        bytes[length - 2] <- 13uy
        bytes[length - 1] <- 10uy

        match RecordBytes.envelope bytes with
        | Error error -> failwithf "%A" error
        | Ok envelope ->
            Assert.Equal("BR", envelope.Header.RecordId)
            Assert.Equal("1", envelope.Header.DataCategory)
            Assert.Equal(Some(DateOnly(2023, 8, 8)), envelope.Header.CreatedDate)
            Assert.Equal<byte>(bytes, envelope.Raw)
            Assert.True(RecordBytes.crlf "BR" (length - 1) bytes |> Result.isOk)

    [<Fact>]
    let ``Independent field oracle contains all thirty eight layouts and 1270 field entries`` () =
        use document =
            JsonDocument.Parse(
                File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Contracts", "record-layouts.json"))
            )

        let records =
            document.RootElement.GetProperty("records").EnumerateArray() |> Seq.toArray

        Assert.Equal(38, records.Length)
        Assert.Equal(1270, records |> Array.sumBy (fun r -> r.GetProperty("fields").GetArrayLength()))

        for record in records do
            let id = record.GetProperty("id").GetString()
            Assert.NotEqual(RecordKind.Unknown id, RecordKinds.ofId id)

            for field in record.GetProperty("fields").EnumerateArray() do
                Assert.StartsWith("フォーマット!E", field.GetProperty("sourceCell").GetString())
                Assert.True(field.GetProperty("position").GetInt32() >= 1)
                Assert.True(field.GetProperty("length").GetInt32() >= 1)
                Assert.True(field.GetProperty("repeat").GetInt32() >= 1)

    [<Fact>]
    let ``O2 is quinella and O3 is wide without conflating their record origins`` () =
        Assert.Equal(RecordKind.QuinellaOdds, RecordKinds.ofId "O2")
        Assert.Equal(RecordKind.WideOdds, RecordKinds.ofId "O3")
        Assert.Equal(RecordKind.Unknown "H5", RecordKinds.ofId "H5")
        Assert.Equal(RecordKind.Unknown "ra", RecordKinds.ofId "ra")

    let private value =
        function
        | Ok value -> value
        | Error error ->
            failwithf
                "OfficialFoundationTests: O2 is quinella and O3 is wide without conflating their record origins: %A"
                error

    let private oracle () =
        JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Contracts", "official-codes.json")))

    let private tables =
        [ CodeTable.Racecourse
          CodeTable.Weekday
          CodeTable.Grade
          CodeTable.RaceKind
          CodeTable.RaceSymbol
          CodeTable.RaceCondition
          CodeTable.WeightRule
          CodeTable.Track
          CodeTable.TrackCondition
          CodeTable.Weather
          CodeTable.Abnormality
          CodeTable.Margin
          CodeTable.Breed
          CodeTable.Sex
          CodeTable.Coat
          CodeTable.HorseSymbol
          CodeTable.Affiliation
          CodeTable.JockeyQualification
          CodeTable.Apprentice ]

    let codes: obj[] seq =
        seq {
            use document = oracle ()

            for row in document.RootElement.GetProperty("codes").EnumerateArray() do
                yield
                    [| box (row.GetProperty("table").GetInt32())
                       box (row.GetProperty("raw").GetString())
                       box (row.GetProperty("cell").GetString()) |]
        }

    let headers: obj[] seq =
        seq {
            use document = oracle ()

            for row in document.RootElement.GetProperty("headers").EnumerateArray() do
                for category in [ "0"; "1"; "9"; "A" ] do
                    yield
                        [| box (row.GetProperty("id").GetString())
                           box category
                           box (row.GetProperty("cell").GetString()) |]
        }

    [<Theory; MemberData(nameof codes)>]
    let ``Every official code remains known and round trips including spaces`` (number: int) raw cell =
        let table = tables |> List.find (fun table -> Codes.number table = number)
        let parsed = Codes.parse table raw |> value
        Assert.True(Codes.isKnown parsed, string cell)
        Assert.Equal(raw, Codes.raw parsed)
        Assert.Equal(table, Codes.table parsed)

    [<Fact>]
    let ``Oracle covers all nineteen tables and thirty eight distinct headers`` () =
        use document = oracle ()

        let ids =
            document.RootElement.GetProperty("headers").EnumerateArray()
            |> Seq.map (fun r -> r.GetProperty("id").GetString())
            |> Set.ofSeq

        Assert.Equal(38, ids.Count)
        Assert.DoesNotContain("H5", ids)
        Assert.Equal(19, codes |> Seq.map (fun row -> unbox<int> row[0]) |> Set.ofSeq |> Set.count)
        Assert.Equal(487, Seq.length codes)

    [<Fact>]
    let ``Unknown codes retain the table and exact bytes`` () =
        for table in tables do
            let raw = String('?', Codes.width table)
            let code = Codes.parse table raw |> value
            Assert.False(Codes.isKnown code)
            Assert.Equal(raw, Codes.raw code)
            Assert.Equal(table, Codes.table code)

    [<Fact>]
    let ``Code parsing rejects null and wrong width without throwing`` () =
        for table in tables do
            for raw in [ null; ""; String('0', Codes.width table + 1); String('Ａ', Codes.width table) ] do
                Assert.True(Codes.parse table raw |> Result.isError)

    [<Fact>]
    let ``Letter grades cannot become integer enums or disappear`` () =
        for raw in [ "A"; "B"; "C"; "D"; "E"; "F"; "G"; "H"; "L"; " " ] do
            Assert.True(Codes.parse CodeTable.Grade raw |> value |> Codes.isKnown)

        Assert.Equal(Some "G1（平地競走）", Codes.parse CodeTable.Grade "A" |> value |> Codes.label)

    [<Theory; MemberData(nameof headers)>]
    let ``All official headers retain category and creation date independently of body`` id category (cell: string) =
        let raw = Encoding.ASCII.GetBytes(id + category + "20260912body\r\n")
        let envelope = RecordBytes.envelope raw |> value
        Assert.Equal(id, envelope.Header.RecordId)
        Assert.Equal(category, envelope.Header.DataCategory)
        Assert.Equal("20260912", envelope.Header.CreatedDateRaw)
        Assert.Equal(Some(DateOnly(2026, 9, 12)), envelope.Header.CreatedDate)
        Assert.Equal<byte>(raw, envelope.Raw)
        raw[0] <- 0uy
        Assert.NotEqual(raw[0], envelope.Raw[0])
        Assert.StartsWith("フォーマット!E", cell)

    [<Fact>]
    let ``Unknown envelopes keep raw bytes and a zero date remains distinguishable`` () =
        let bytes = Encoding.ASCII.GetBytes("ZZ000000000\r\n")
        let envelope = RecordBytes.envelope bytes |> value
        Assert.Equal("ZZ", envelope.Header.RecordId)
        Assert.Equal("00000000", envelope.Header.CreatedDateRaw)
        Assert.Equal(None, envelope.Header.CreatedDate)
        Assert.Equal<byte>(bytes, envelope.Raw)

    [<Fact>]
    let ``Invalid dates identify record field and original byte position`` () =
        for date in [ "20260230"; "20261301"; "        "; "２０２６０１０１" ] do
            let input = Encoding.UTF8.GetBytes("RA1" + date)

            match RecordBytes.header input with
            | Ok _ -> failwith "Invalid date accepted"
            | Error error ->
                Assert.Equal("RA", error.RecordId)
                Assert.Equal("CreatedDate", error.Field)
                Assert.Equal(4, error.Position)
                Assert.Equal(8, error.Length)

    [<Fact>]
    let ``Short null and invalid boundaries return errors without exceptions`` () =
        for raw in [ null; [||]; [| 82uy |]; Encoding.ASCII.GetBytes("RA12026091") ] do
            Assert.True(RecordBytes.header raw |> Result.isError)

        for position, length in [ 0, 1; -1, 1; 1, -1; Int32.MaxValue, 1; 1, Int32.MaxValue ] do
            Assert.True(RecordBytes.field "RA" "example" position length [| 65uy |] |> Result.isError)

    [<Fact>]
    let ``Japanese fields use byte boundaries and preserve both kinds of spaces`` () =
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)
        let bytes = Encoding.GetEncoding(932).GetBytes("X東京　 Y")
        Assert.Equal("東京　 ", RecordBytes.text "RA" "Name" 2 7 bytes |> value)
        Assert.Equal("Y", RecordBytes.text "RA" "AfterName" 9 1 bytes |> value)
        Assert.True(RecordBytes.text "RA" "SplitLeadByte" 2 1 bytes |> Result.isError)

    [<Theory>]
    [<InlineData("000", false, 10, "0")>]
    [<InlineData("-12", true, 10, "-1.2")>]
    [<InlineData("+12", true, 10, "1.2")>]
    [<InlineData("123", false, 100, "1.23")>]
    let ``Numbers distinguish zero sign and implicit scale`` raw signed scale (expected: string) =
        let bytes = Encoding.ASCII.GetBytes(raw: string)

        let parsed =
            RecordBytes.number signed (decimal scale) "SE" "Number" 1 bytes.Length bytes
            |> value

        Assert.Equal(Some(Decimal.Parse(expected, Globalization.CultureInfo.InvariantCulture)), parsed)

    [<Fact>]
    let ``Blanks are missing but malformed numbers are errors`` () =
        Assert.Equal(None, RecordBytes.number false 1M "SE" "Number" 1 3 [| 32uy; 32uy; 32uy |] |> value)

        for raw in [ "-12"; "+12"; "1 2"; "12 "; "1.2"; "ABC" ] do
            Assert.True(
                RecordBytes.number false 1M "SE" "Number" 1 3 (Encoding.ASCII.GetBytes raw)
                |> Result.isError
            )

    [<Fact>]
    let ``CRLF is validated at the explicitly selected layout boundary`` () =
        for length in [ 42; 78; 1272; 12293 ] do
            let bytes = Array.create length 32uy
            bytes[length - 2] <- 13uy
            bytes[length - 1] <- 10uy
            Assert.True(RecordBytes.crlf "RA" (length - 1) bytes |> Result.isOk)
            Assert.True(RecordBytes.crlf "RA" (length - 2) bytes |> Result.isError)

namespace Xanthos.UnitTests

open System
open System.Text
open System.Threading.Tasks
open Xunit
open Xanthos
open Xanthos.Data

module Cp932RecordTests =
    let private value =
        function
        | Ok result -> result
        | Error error -> failwithf "%A" error

    // Independent byte/Unicode expectations from Microsoft's CP932.TXT:
    // https://www.unicode.org/Public/MAPPINGS/VENDORS/MICSFT/WINDOWS/CP932.TXT
    let private aliases =
        [ "8790", "\u2252"
          "8791", "\u2261"
          "8792", "\u222B"
          "8795", "\u221A"
          "8796", "\u22A5"
          "8797", "\u2220"
          "879A", "\u2235"
          "879B", "\u2229"
          "879C", "\u222A"
          "ED40", "\u7E8A"
          "ED7E", "\uFA0F"
          "ED80", "\uFA10"
          "EDFC", "\u72B1"
          "EE40", "\u72BE"
          "EE7E", "\u8559"
          "EE80", "\u856B"
          "EEEC", "\u9ED1"
          "EEEF", "\u2170"
          "EEF8", "\u2179"
          "EEF9", "\uFFE2"
          "EEFA", "\uFFE4"
          "EEFB", "\uFF07"
          "EEFC", "\uFF02"
          "FA4A", "Ⅰ"
          "FA4B", "Ⅱ"
          "FA4C", "Ⅲ"
          "FA4D", "Ⅳ"
          "FA4E", "Ⅴ"
          "FA4F", "Ⅵ"
          "FA50", "Ⅶ"
          "FA51", "Ⅷ"
          "FA52", "Ⅸ"
          "FA53", "Ⅹ"
          "FA54", "\uFFE2"
          "FA58", "\u3231"
          "FA59", "\u2116"
          "FA5A", "\u2121"
          "FA5B", "\u2235" ]

    [<Fact>]
    let ``Documented CP932 aliases decode exactly without changing source bytes or spaces`` () =
        for hex, expected in aliases do
            let input = Convert.FromHexString("588266" + hex + "81402059")
            let original = Array.copy input
            Assert.Equal("Ｇ" + expected + "　 ", RecordBytes.text "BT" "Description" 2 7 input |> value)
            Assert.Equal<byte>(original, input)
            Assert.Equal<byte>(Convert.FromHexString hex, RecordBytes.field "BT" "Symbol" 4 2 input |> value)

        let joined =
            aliases
            |> List.collect (fst >> Convert.FromHexString >> Array.toList)
            |> List.toArray

        Assert.Equal(
            aliases |> List.map snd |> String.concat "",
            RecordBytes.text "BT" "Description" 1 joined.Length joined |> value
        )

    [<Theory>]
    [<InlineData("81")>]
    [<InlineData("FA")>]
    [<InlineData("ED7F")>]
    [<InlineData("EEED")>]
    [<InlineData("EEEE")>]
    [<InlineData("879D")>]
    [<InlineData("FC4C")>]
    [<InlineData("FA00")>]
    [<InlineData("FA4A82")>]
    [<InlineData("81ADFA4A")>]
    [<InlineData("FA4AEEEDFA4B")>]
    let ``Malformed and undefined sequences remain located failures even beside valid aliases`` (hex: string) =
        let bytes = Convert.FromHexString hex
        let input = Array.concat [ [| 88uy |]; bytes; [| 89uy |] ]

        match RecordBytes.text "BT" "Description" 2 bytes.Length input with
        | Ok result -> failwithf "Accepted %s as %A" hex result
        | Error error ->
            Assert.Equal("BT", error.RecordId)
            Assert.Equal("Description", error.Field)
            Assert.Equal(2, error.Position)
            Assert.Equal(bytes.Length, error.Length)
            Assert.Equal<byte>(bytes, error.Raw)
            Assert.Equal("Invalid Shift-JIS byte sequence.", error.Message)

    [<Fact>]
    let ``An alias cannot consume bytes outside its field or enter an ASCII field`` () =
        let input = Convert.FromHexString "58FA4A59"
        Assert.True(RecordBytes.text "BT" "SplitAlias" 2 1 input |> Result.isError)
        Assert.True(RecordBytes.ascii "BT" "Identifier" 2 2 input |> Result.isError)
        Assert.Equal("Ⅰ", RecordBytes.text "BT" "Description" 2 2 input |> value)
        Assert.Equal("", RecordBytes.text "BT" "Empty" 2 0 input |> value)

    [<Theory>]
    [<InlineData(true)>]
    [<InlineData(false)>]
    let ``Legacy and expanded BT descriptions retain aliases and own the complete original record`` legacy =
        let format, length, breeding, nameOffset, descriptionOffset =
            if legacy then
                IdentifierFormat.Legacy, 6887, "11201131", 49, 85
            else
                IdentifierFormat.Expanded, 6889, "0011201131", 51, 87

        let input = Array.create length 32uy
        Encoding.ASCII.GetBytes("BT220150702" + breeding).CopyTo(input, 0)
        Convert.FromHexString("8266FA4A").CopyTo(input, nameOffset)
        // Use ordinary full-width text next to aliases, plus both kinds of spaces.
        let description =
            Array.concat
                [ Convert.FromHexString "8266FA4A20816A"
                  Convert.FromHexString "FA4B8140FA4CFA4F" ]

        description.CopyTo(input, descriptionOffset)
        input[length - 2] <- 13uy
        input[length - 1] <- 10uy
        let original = Array.copy input
        let parsed = Records.parseBTWithFormat format input |> value
        Assert.Equal(breeding, parsed.BreedingId)
        Assert.Equal("ＧⅠ" + String(' ', 32), parsed.Name)
        Assert.Equal("ＧⅠ ）Ⅱ　ⅢⅥ" + String(' ', 6800 - description.Length), parsed.Description)
        Assert.Equal<byte>(original, parsed.Raw)
        Assert.Equal<byte>(original, input)
        input[descriptionOffset + 2] <- 32uy
        Assert.Equal<byte>(original, parsed.Raw)

    [<Fact>]
    let ``Concurrent alias decoding and failures do not share fallback state`` () =
        Parallel.For(
            0,
            256,
            fun i ->
                let hex, expected = aliases[i % aliases.Length]
                Assert.Equal(expected, RecordBytes.text "BT" "Description" 1 2 (Convert.FromHexString hex) |> value)

                Assert.True(
                    RecordBytes.text "BT" "Invalid" 1 3 (Convert.FromHexString "FA4A81")
                    |> Result.isError
                )
        )
        |> ignore

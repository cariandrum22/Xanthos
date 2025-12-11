module Xanthos.UnitTests.RecordTypesTests

open Xunit
open Xanthos.Core.Records.RecordTypes

// ============================================================================
// RecordTypes.parse - All Record Type Branches
// ============================================================================

[<Fact>]
let ``parse should identify TK`` () = Assert.Equal(RecordType.TK, parse "TK")

[<Fact>]
let ``parse should identify RA`` () = Assert.Equal(RecordType.RA, parse "RA")

[<Fact>]
let ``parse should identify SE`` () = Assert.Equal(RecordType.SE, parse "SE")

[<Fact>]
let ``parse should identify HR`` () = Assert.Equal(RecordType.HR, parse "HR")

[<Fact>]
let ``parse should identify O1`` () = Assert.Equal(RecordType.O1, parse "O1")

[<Fact>]
let ``parse should identify O2`` () = Assert.Equal(RecordType.O2, parse "O2")

[<Fact>]
let ``parse should identify O3`` () = Assert.Equal(RecordType.O3, parse "O3")

[<Fact>]
let ``parse should identify O4`` () = Assert.Equal(RecordType.O4, parse "O4")

[<Fact>]
let ``parse should identify O5`` () = Assert.Equal(RecordType.O5, parse "O5")

[<Fact>]
let ``parse should identify O6`` () = Assert.Equal(RecordType.O6, parse "O6")

[<Fact>]
let ``parse should identify H1`` () = Assert.Equal(RecordType.H1, parse "H1")

[<Fact>]
let ``parse should identify H5`` () = Assert.Equal(RecordType.H5, parse "H5")

[<Fact>]
let ``parse should identify H6`` () = Assert.Equal(RecordType.H6, parse "H6")

[<Fact>]
let ``parse should identify WF`` () = Assert.Equal(RecordType.WF, parse "WF")

[<Fact>]
let ``parse should identify JC`` () = Assert.Equal(RecordType.JC, parse "JC")

[<Fact>]
let ``parse should identify TC`` () = Assert.Equal(RecordType.TC, parse "TC")

[<Fact>]
let ``parse should identify CC`` () = Assert.Equal(RecordType.CC, parse "CC")

[<Fact>]
let ``parse should identify WE`` () = Assert.Equal(RecordType.WE, parse "WE")

[<Fact>]
let ``parse should identify AV`` () = Assert.Equal(RecordType.AV, parse "AV")

[<Fact>]
let ``parse should identify UM`` () = Assert.Equal(RecordType.UM, parse "UM")

[<Fact>]
let ``parse should identify KS`` () = Assert.Equal(RecordType.KS, parse "KS")

[<Fact>]
let ``parse should identify CH`` () = Assert.Equal(RecordType.CH, parse "CH")

[<Fact>]
let ``parse should identify BR`` () = Assert.Equal(RecordType.BR, parse "BR")

[<Fact>]
let ``parse should identify BN`` () = Assert.Equal(RecordType.BN, parse "BN")

[<Fact>]
let ``parse should identify RC`` () = Assert.Equal(RecordType.RC, parse "RC")

[<Fact>]
let ``parse should identify HN`` () = Assert.Equal(RecordType.HN, parse "HN")

[<Fact>]
let ``parse should identify SK`` () = Assert.Equal(RecordType.SK, parse "SK")

[<Fact>]
let ``parse should identify CK`` () = Assert.Equal(RecordType.CK, parse "CK")

[<Fact>]
let ``parse should identify HC`` () = Assert.Equal(RecordType.HC, parse "HC")

[<Fact>]
let ``parse should identify HS`` () = Assert.Equal(RecordType.HS, parse "HS")

[<Fact>]
let ``parse should identify HY`` () = Assert.Equal(RecordType.HY, parse "HY")

[<Fact>]
let ``parse should identify YS`` () = Assert.Equal(RecordType.YS, parse "YS")

[<Fact>]
let ``parse should identify BT`` () = Assert.Equal(RecordType.BT, parse "BT")

[<Fact>]
let ``parse should identify CS`` () = Assert.Equal(RecordType.CS, parse "CS")

[<Fact>]
let ``parse should identify DM`` () = Assert.Equal(RecordType.DM, parse "DM")

[<Fact>]
let ``parse should identify TM`` () = Assert.Equal(RecordType.TM, parse "TM")

[<Fact>]
let ``parse should identify WC`` () = Assert.Equal(RecordType.WC, parse "WC")

[<Fact>]
let ``parse should identify WH`` () = Assert.Equal(RecordType.WH, parse "WH")

[<Fact>]
let ``parse should identify JG`` () = Assert.Equal(RecordType.JG, parse "JG")

[<Theory>]
[<InlineData("tk")>] // lowercase
[<InlineData("Tk")>] // mixed case
[<InlineData("tK")>]
[<InlineData(" TK ")>] // with whitespace
[<InlineData("  tk  ")>]
let ``parse should be case-insensitive and trim whitespace`` (input: string) =
    let result = parse input
    Assert.Equal(RecordType.TK, result)

[<Theory>]
[<InlineData("XX")>]
[<InlineData("ZZ")>]
[<InlineData("99")>]
[<InlineData("AB")>]
[<InlineData("")>]
[<InlineData("   ")>]
[<InlineData("INVALID")>]
let ``parse should return Unknown for unrecognized type identifiers`` (input: string) =
    match parse input with
    | Unknown s -> Assert.Equal(input.ToUpperInvariant().Trim(), s)
    | _ -> failwith $"Expected Unknown but got known type for '{input}'"

// ============================================================================
// RecordTypes.toString - All Record Type Branches
// ============================================================================

[<Fact>]
let ``toString should return TK for TK type`` () =
    Assert.Equal("TK", toString RecordType.TK)

[<Fact>]
let ``toString should return RA for RA type`` () =
    Assert.Equal("RA", toString RecordType.RA)

[<Fact>]
let ``toString should return SE for SE type`` () =
    Assert.Equal("SE", toString RecordType.SE)

[<Fact>]
let ``toString should return HR for HR type`` () =
    Assert.Equal("HR", toString RecordType.HR)

[<Fact>]
let ``toString should return O1 for O1 type`` () =
    Assert.Equal("O1", toString RecordType.O1)

[<Fact>]
let ``toString should return O2 for O2 type`` () =
    Assert.Equal("O2", toString RecordType.O2)

[<Fact>]
let ``toString should return O3 for O3 type`` () =
    Assert.Equal("O3", toString RecordType.O3)

[<Fact>]
let ``toString should return O4 for O4 type`` () =
    Assert.Equal("O4", toString RecordType.O4)

[<Fact>]
let ``toString should return O5 for O5 type`` () =
    Assert.Equal("O5", toString RecordType.O5)

[<Fact>]
let ``toString should return O6 for O6 type`` () =
    Assert.Equal("O6", toString RecordType.O6)

[<Fact>]
let ``toString should return H1 for H1 type`` () =
    Assert.Equal("H1", toString RecordType.H1)

[<Fact>]
let ``toString should return H5 for H5 type`` () =
    Assert.Equal("H5", toString RecordType.H5)

[<Fact>]
let ``toString should return H6 for H6 type`` () =
    Assert.Equal("H6", toString RecordType.H6)

[<Fact>]
let ``toString should return WF for WF type`` () =
    Assert.Equal("WF", toString RecordType.WF)

[<Fact>]
let ``toString should return JC for JC type`` () =
    Assert.Equal("JC", toString RecordType.JC)

[<Fact>]
let ``toString should return TC for TC type`` () =
    Assert.Equal("TC", toString RecordType.TC)

[<Fact>]
let ``toString should return CC for CC type`` () =
    Assert.Equal("CC", toString RecordType.CC)

[<Fact>]
let ``toString should return WE for WE type`` () =
    Assert.Equal("WE", toString RecordType.WE)

[<Fact>]
let ``toString should return AV for AV type`` () =
    Assert.Equal("AV", toString RecordType.AV)

[<Fact>]
let ``toString should return UM for UM type`` () =
    Assert.Equal("UM", toString RecordType.UM)

[<Fact>]
let ``toString should return KS for KS type`` () =
    Assert.Equal("KS", toString RecordType.KS)

[<Fact>]
let ``toString should return CH for CH type`` () =
    Assert.Equal("CH", toString RecordType.CH)

[<Fact>]
let ``toString should return BR for BR type`` () =
    Assert.Equal("BR", toString RecordType.BR)

[<Fact>]
let ``toString should return BN for BN type`` () =
    Assert.Equal("BN", toString RecordType.BN)

[<Fact>]
let ``toString should return RC for RC type`` () =
    Assert.Equal("RC", toString RecordType.RC)

[<Fact>]
let ``toString should return HN for HN type`` () =
    Assert.Equal("HN", toString RecordType.HN)

[<Fact>]
let ``toString should return SK for SK type`` () =
    Assert.Equal("SK", toString RecordType.SK)

[<Fact>]
let ``toString should return CK for CK type`` () =
    Assert.Equal("CK", toString RecordType.CK)

[<Fact>]
let ``toString should return HC for HC type`` () =
    Assert.Equal("HC", toString RecordType.HC)

[<Fact>]
let ``toString should return HS for HS type`` () =
    Assert.Equal("HS", toString RecordType.HS)

[<Fact>]
let ``toString should return HY for HY type`` () =
    Assert.Equal("HY", toString RecordType.HY)

[<Fact>]
let ``toString should return YS for YS type`` () =
    Assert.Equal("YS", toString RecordType.YS)

[<Fact>]
let ``toString should return BT for BT type`` () =
    Assert.Equal("BT", toString RecordType.BT)

[<Fact>]
let ``toString should return CS for CS type`` () =
    Assert.Equal("CS", toString RecordType.CS)

[<Fact>]
let ``toString should return DM for DM type`` () =
    Assert.Equal("DM", toString RecordType.DM)

[<Fact>]
let ``toString should return TM for TM type`` () =
    Assert.Equal("TM", toString RecordType.TM)

[<Fact>]
let ``toString should return WC for WC type`` () =
    Assert.Equal("WC", toString RecordType.WC)

[<Fact>]
let ``toString should return WH for WH type`` () =
    Assert.Equal("WH", toString RecordType.WH)

[<Fact>]
let ``toString should return JG for JG type`` () =
    Assert.Equal("JG", toString RecordType.JG)

[<Fact>]
let ``toString should return original string for Unknown type`` () =
    let unknownType = Unknown "XY"
    let result = toString unknownType
    Assert.Equal("XY", result)

[<Fact>]
let ``parse and toString should be reversible for all known types`` () =
    let allKnownTypes =
        [ "TK"
          "RA"
          "SE"
          "HR"
          "O1"
          "O2"
          "O3"
          "O4"
          "O5"
          "O6"
          "H1"
          "H5"
          "H6"
          "UM"
          "KS"
          "CH"
          "BR"
          "BN"
          "HN"
          "SK"
          "RC"
          "CK"
          "HC"
          "HS"
          "HY"
          "YS"
          "BT"
          "CS"
          "DM"
          "TM"
          "WF"
          "WC"
          "WH"
          "WE"
          "AV"
          "JC"
          "TC"
          "CC"
          "JG" ]

    for typeId in allKnownTypes do
        let parsed = parse typeId
        let back = toString parsed
        Assert.Equal(typeId, back)

// ============================================================================
// RecordTypes.isRecognized - All Record Type Branches
// ============================================================================

[<Fact>]
let ``isRecognized should return true for TK`` () = Assert.True(isRecognized RecordType.TK)

[<Fact>]
let ``isRecognized should return true for RA`` () = Assert.True(isRecognized RecordType.RA)

[<Fact>]
let ``isRecognized should return true for SE`` () = Assert.True(isRecognized RecordType.SE)

[<Fact>]
let ``isRecognized should return true for HR`` () = Assert.True(isRecognized RecordType.HR)

[<Fact>]
let ``isRecognized should return true for O1`` () = Assert.True(isRecognized RecordType.O1)

[<Fact>]
let ``isRecognized should return true for O2`` () = Assert.True(isRecognized RecordType.O2)

[<Fact>]
let ``isRecognized should return true for O3`` () = Assert.True(isRecognized RecordType.O3)

[<Fact>]
let ``isRecognized should return true for O4`` () = Assert.True(isRecognized RecordType.O4)

[<Fact>]
let ``isRecognized should return true for O5`` () = Assert.True(isRecognized RecordType.O5)

[<Fact>]
let ``isRecognized should return true for O6`` () = Assert.True(isRecognized RecordType.O6)

[<Fact>]
let ``isRecognized should return true for H1`` () = Assert.True(isRecognized RecordType.H1)

[<Fact>]
let ``isRecognized should return true for H5`` () = Assert.True(isRecognized RecordType.H5)

[<Fact>]
let ``isRecognized should return true for H6`` () = Assert.True(isRecognized RecordType.H6)

[<Fact>]
let ``isRecognized should return true for WF`` () = Assert.True(isRecognized RecordType.WF)

[<Fact>]
let ``isRecognized should return true for JC`` () = Assert.True(isRecognized RecordType.JC)

[<Fact>]
let ``isRecognized should return true for TC`` () = Assert.True(isRecognized RecordType.TC)

[<Fact>]
let ``isRecognized should return true for CC`` () = Assert.True(isRecognized RecordType.CC)

[<Fact>]
let ``isRecognized should return true for WE`` () = Assert.True(isRecognized RecordType.WE)

[<Fact>]
let ``isRecognized should return true for AV`` () = Assert.True(isRecognized RecordType.AV)

[<Fact>]
let ``isRecognized should return true for UM`` () = Assert.True(isRecognized RecordType.UM)

[<Fact>]
let ``isRecognized should return true for KS`` () = Assert.True(isRecognized RecordType.KS)

[<Fact>]
let ``isRecognized should return true for CH`` () = Assert.True(isRecognized RecordType.CH)

[<Fact>]
let ``isRecognized should return true for BR`` () = Assert.True(isRecognized RecordType.BR)

[<Fact>]
let ``isRecognized should return true for BN`` () = Assert.True(isRecognized RecordType.BN)

[<Fact>]
let ``isRecognized should return true for RC`` () = Assert.True(isRecognized RecordType.RC)

[<Fact>]
let ``isRecognized should return true for HN`` () = Assert.True(isRecognized RecordType.HN)

[<Fact>]
let ``isRecognized should return true for SK`` () = Assert.True(isRecognized RecordType.SK)

[<Fact>]
let ``isRecognized should return true for CK`` () = Assert.True(isRecognized RecordType.CK)

[<Fact>]
let ``isRecognized should return true for HC`` () = Assert.True(isRecognized RecordType.HC)

[<Fact>]
let ``isRecognized should return true for HS`` () = Assert.True(isRecognized RecordType.HS)

[<Fact>]
let ``isRecognized should return true for HY`` () = Assert.True(isRecognized RecordType.HY)

[<Fact>]
let ``isRecognized should return true for YS`` () = Assert.True(isRecognized RecordType.YS)

[<Fact>]
let ``isRecognized should return true for BT`` () = Assert.True(isRecognized RecordType.BT)

[<Fact>]
let ``isRecognized should return true for CS`` () = Assert.True(isRecognized RecordType.CS)

[<Fact>]
let ``isRecognized should return true for DM`` () = Assert.True(isRecognized RecordType.DM)

[<Fact>]
let ``isRecognized should return true for TM`` () = Assert.True(isRecognized RecordType.TM)

[<Fact>]
let ``isRecognized should return true for WC`` () = Assert.True(isRecognized RecordType.WC)

[<Fact>]
let ``isRecognized should return true for WH`` () = Assert.True(isRecognized RecordType.WH)

[<Fact>]
let ``isRecognized should return true for JG`` () = Assert.True(isRecognized RecordType.JG)

[<Fact>]
let ``isRecognized should return false for Unknown type`` () =
    let unknownType = Unknown "XX"
    let result = isRecognized unknownType
    Assert.False(result)

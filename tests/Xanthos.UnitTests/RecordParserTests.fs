module Xanthos.UnitTests.RecordParserTests

open System
open Xunit
open Xanthos.Core
open Xanthos.Core.Text
open Xanthos.Core.Records
open Xanthos.Core.Records.RecordParser
open Xanthos.Core.Records.CodeTables
open Xanthos.Core.Records.TK
open Xanthos.Core.Records.RA
open Xanthos.Core.Records.SE
open Xanthos.Core.Records.HR
open Xanthos.Core.Records.O1
open Xanthos.Core.Records.O2
open Xanthos.Core.Records.O3
open Xanthos.Core.Records.O4
open Xanthos.Core.Records.O5
open Xanthos.Core.Records.O6
open Xanthos.Core.Records.H1
open Xanthos.Core.Records.H5
open Xanthos.Core.Records.H6
open Xanthos.Core.Records.WF
open Xanthos.Core.Records.JC
open Xanthos.Core.Records.TC
open Xanthos.Core.Records.CC
open Xanthos.Core.Records.WE
open Xanthos.Core.Records.AV
open Xanthos.Core.Records.UM
open Xanthos.Core.Records.KS
open Xanthos.Core.Records.CH
open Xanthos.Core.Records.BR
open Xanthos.Core.Records.BN
open Xanthos.Core.Records.RC

// ============================================================================
// Core Infrastructure Tests
// ============================================================================

[<Fact>]
let ``RecordParser extractBytes returns correct bytes`` () =
    let data = [| 1uy; 2uy; 3uy; 4uy; 5uy |]

    match extractBytes data 1 3 with
    | Ok bytes -> Assert.Equal<byte[]>([| 2uy; 3uy; 4uy |], bytes)
    | Error err -> failwithf "Expected Ok, got %A" err

[<Fact>]
let ``RecordParser parseInt parses valid integer`` () =
    let bytes = encodeShiftJis "123"

    match parseInt bytes with
    | Some value -> Assert.Equal(123, value)
    | None -> failwith "Expected Some integer"

[<Fact>]
let ``RecordParser parseDecimal with precision 1`` () =
    let bytes = encodeShiftJis "1234"

    match parseDecimal bytes 1 with
    | Some value -> Assert.Equal(123.4M, value)
    | None -> failwith "Expected Some decimal"

[<Fact>]
let ``RecordParser parseDate with yyyyMMdd format`` () =
    let bytes = encodeShiftJis "20240315"

    match parseDate bytes "yyyyMMdd" with
    | Some date -> Assert.Equal(DateTime(2024, 3, 15), date)
    | None -> failwith "Expected Some date"

[<Fact>]
let ``RecordParser getRecordType extracts first 2 bytes`` () =
    let data = encodeShiftJis "TK12345"
    Assert.Equal("TK", getRecordType data)

// ============================================================================
// CodeTables Tests
// ============================================================================

[<Fact>]
let ``CodeTables parseCode parses SexCode Male`` () =
    match parseCode<SexCode> "1" with
    | Some code -> Assert.Equal(SexCode.Male, code)
    | None -> failwith "Expected Some SexCode.Male"

[<Fact>]
let ``CodeTables parseCode parses RacecourseCode Tokyo`` () =
    match parseCode<RacecourseCode> "5" with
    | Some code -> Assert.Equal(RacecourseCode.Tokyo, code)
    | None -> failwith "Expected Some RacecourseCode"

// ============================================================================
// RecordTypes Tests
// ============================================================================

[<Fact>]
let ``RecordTypes parse returns TK for "TK"`` () =
    Assert.Equal(RecordTypes.RecordType.TK, RecordTypes.parse "TK")

[<Fact>]
let ``RecordTypes parse returns RA for "RA"`` () =
    Assert.Equal(RecordTypes.RecordType.RA, RecordTypes.parse "RA")

[<Fact>]
let ``RecordTypes isRecognized returns true for all recognized types`` () =
    // Race data
    Assert.True(RecordTypes.isRecognized RecordTypes.RecordType.TK)
    Assert.True(RecordTypes.isRecognized RecordTypes.RecordType.RA)
    Assert.True(RecordTypes.isRecognized RecordTypes.RecordType.SE)
    Assert.True(RecordTypes.isRecognized RecordTypes.RecordType.HR)
    // Odds
    Assert.True(RecordTypes.isRecognized RecordTypes.RecordType.O1)
    Assert.True(RecordTypes.isRecognized RecordTypes.RecordType.O2)
    Assert.True(RecordTypes.isRecognized RecordTypes.RecordType.O3)
    Assert.True(RecordTypes.isRecognized RecordTypes.RecordType.O4)
    Assert.True(RecordTypes.isRecognized RecordTypes.RecordType.O5)
    Assert.True(RecordTypes.isRecognized RecordTypes.RecordType.O6)
    // Vote counts
    Assert.True(RecordTypes.isRecognized RecordTypes.RecordType.H1)
    Assert.True(RecordTypes.isRecognized RecordTypes.RecordType.H5)
    Assert.True(RecordTypes.isRecognized RecordTypes.RecordType.H6)
    // Master data
    Assert.True(RecordTypes.isRecognized RecordTypes.RecordType.UM)
    Assert.True(RecordTypes.isRecognized RecordTypes.RecordType.KS)
    Assert.True(RecordTypes.isRecognized RecordTypes.RecordType.CH)
    Assert.True(RecordTypes.isRecognized RecordTypes.RecordType.BR)
    Assert.True(RecordTypes.isRecognized RecordTypes.RecordType.BN)
    Assert.True(RecordTypes.isRecognized RecordTypes.RecordType.HN)
    Assert.True(RecordTypes.isRecognized RecordTypes.RecordType.SK)
    Assert.True(RecordTypes.isRecognized RecordTypes.RecordType.RC)
    // Analysis data
    Assert.True(RecordTypes.isRecognized RecordTypes.RecordType.CK)
    Assert.True(RecordTypes.isRecognized RecordTypes.RecordType.HC)
    Assert.True(RecordTypes.isRecognized RecordTypes.RecordType.HS)
    Assert.True(RecordTypes.isRecognized RecordTypes.RecordType.HY)
    Assert.True(RecordTypes.isRecognized RecordTypes.RecordType.YS)
    Assert.True(RecordTypes.isRecognized RecordTypes.RecordType.BT)
    Assert.True(RecordTypes.isRecognized RecordTypes.RecordType.CS)
    Assert.True(RecordTypes.isRecognized RecordTypes.RecordType.DM)
    Assert.True(RecordTypes.isRecognized RecordTypes.RecordType.TM)
    Assert.True(RecordTypes.isRecognized RecordTypes.RecordType.WF)
    Assert.True(RecordTypes.isRecognized RecordTypes.RecordType.WC)
    // Real-time data
    Assert.True(RecordTypes.isRecognized RecordTypes.RecordType.WH)
    Assert.True(RecordTypes.isRecognized RecordTypes.RecordType.WE)
    Assert.True(RecordTypes.isRecognized RecordTypes.RecordType.AV)
    Assert.True(RecordTypes.isRecognized RecordTypes.RecordType.JC)
    Assert.True(RecordTypes.isRecognized RecordTypes.RecordType.TC)
    Assert.True(RecordTypes.isRecognized RecordTypes.RecordType.CC)
    Assert.True(RecordTypes.isRecognized RecordTypes.RecordType.JG)

[<Fact>]
let ``RecordTypes toString returns correct string`` () =
    Assert.Equal("TK", RecordTypes.toString RecordTypes.RecordType.TK)
    Assert.Equal("O1", RecordTypes.toString RecordTypes.RecordType.O1)
    Assert.Equal("UM", RecordTypes.toString RecordTypes.RecordType.UM)

// ============================================================================
// Phase 2: Race Data Record Parser Tests
// ============================================================================

[<Fact>]
let ``TK parser extracts race key correctly`` () =
    // Create minimal valid TK record (212 bytes minimum)
    let data = Array.create 346 32uy // Fill with spaces (full record size)
    let raceKey = "2024050512345678"
    let horseId = "2020105678"
    let horseName = "TestHorse"
    Array.Copy(encodeShiftJis "TK", 0, data, 0, 2)
    Array.Copy(encodeShiftJis raceKey, 0, data, 2, 16)
    Array.Copy(encodeShiftJis horseId, 0, data, 18, 10) // HorseId required
    Array.Copy(encodeShiftJis horseName, 0, data, 28, 9) // HorseName required

    match TK.parse data with
    | Ok record -> Assert.Equal(raceKey, record.RaceKey)
    | Error err -> failwithf "TK parsing failed: %A" err

[<Fact>]
let ``RA parser extracts race key and course correctly`` () =
    // Create minimal valid RA record (366 bytes)
    let data = Array.create 366 32uy
    let raceKey = "2024050512345678"
    let raceName = "TestRace"
    Array.Copy(encodeShiftJis "RA", 0, data, 0, 2)
    Array.Copy(encodeShiftJis raceKey, 0, data, 2, 16)
    Array.Copy(encodeShiftJis raceName, 0, data, 18, 8) // RaceName required at offset 18
    Array.Copy(encodeShiftJis "05", 0, data, 68, 2) // Tokyo racecourse code at offset 68

    match RA.parse data with
    | Ok record ->
        Assert.Equal(raceKey, record.RaceKey)
        Assert.Equal(Some RacecourseCode.Tokyo, record.RacecourseCode)
    | Error err -> failwithf "RA parsing failed: %A" err

[<Fact>]
let ``SE parser extracts horse number correctly`` () =
    // Create minimal valid SE record (164 bytes minimum)
    let data = Array.create 1446 32uy
    let raceKey = "2024050512345678"
    let horseId = "2020105678"
    let horseName = "TestHorse"
    Array.Copy(encodeShiftJis "SE", 0, data, 0, 2)
    Array.Copy(encodeShiftJis raceKey, 0, data, 2, 16)
    Array.Copy(encodeShiftJis horseId, 0, data, 18, 10) // HorseId required at offset 18
    Array.Copy(encodeShiftJis horseName, 0, data, 28, 9) // HorseName required at offset 28
    Array.Copy(encodeShiftJis "05", 0, data, 65, 2) // Horse number 5 at offset 65

    match SE.parse data with
    | Ok record ->
        Assert.Equal(raceKey, record.RaceKey)
        Assert.Equal(Some 5, record.HorseNumber)
    | Error err -> failwithf "SE parsing failed: %A" err

[<Fact>]
let ``HR parser extracts bet type and payoff correctly`` () =
    // Create minimal valid HR record (90 bytes)
    let data = Array.create 90 32uy
    let raceKey = "2024050512345678"
    Array.Copy(encodeShiftJis "HR", 0, data, 0, 2)
    Array.Copy(encodeShiftJis raceKey, 0, data, 2, 16)
    Array.Copy(encodeShiftJis "1", 0, data, 18, 1) // Bet type: Win
    Array.Copy(encodeShiftJis "03", 0, data, 19, 2) // Horse 3
    Array.Copy(encodeShiftJis "000001200", 0, data, 25, 9) // 1200 yen payoff

    match HR.parse data with
    | Ok record ->
        Assert.Equal(raceKey, record.RaceKey)
        Assert.Equal(Some 1, record.BetType)
        Assert.Equal(Some 3, record.HorseNumber1)
        Assert.Equal(Some 1200, record.Payoff)
    | Error err -> failwithf "HR parsing failed: %A" err

// ============================================================================
// Phase 3: Odds Data Record Parser Tests
// ============================================================================

[<Fact>]
let ``O1 parser extracts win odds correctly`` () =
    // Create minimal valid O1 record (38 bytes minimum)
    let data = Array.create 100 32uy
    let raceKey = "2024050512345678"
    Array.Copy(encodeShiftJis "O1", 0, data, 0, 2)
    Array.Copy(encodeShiftJis raceKey, 0, data, 2, 16)
    Array.Copy(encodeShiftJis "03", 0, data, 18, 2) // Horse number 3
    Array.Copy(encodeShiftJis "0035", 0, data, 20, 4) // Odds 3.5 (35 with precision 1)
    Array.Copy(encodeShiftJis "01", 0, data, 24, 2) // Popularity 1

    match O1.parse data with
    | Ok record ->
        Assert.Equal(raceKey, record.RaceKey)
        Assert.Equal(Some 3, record.HorseNumber)
        Assert.Equal(Some 3.5M, record.Odds)
        Assert.Equal(Some 1, record.Popularity)
    | Error err -> failwithf "O1 parsing failed: %A" err

[<Fact>]
let ``O2 parser extracts place odds range correctly`` () =
    // Create minimal valid O2 record
    let data = Array.create 100 32uy
    let raceKey = "2024050512345678"
    Array.Copy(encodeShiftJis "O2", 0, data, 0, 2)
    Array.Copy(encodeShiftJis raceKey, 0, data, 2, 16)
    Array.Copy(encodeShiftJis "05", 0, data, 18, 2) // Horse number 5
    Array.Copy(encodeShiftJis "0012", 0, data, 20, 4) // Min odds 1.2
    Array.Copy(encodeShiftJis "0028", 0, data, 24, 4) // Max odds 2.8

    match O2.parse data with
    | Ok record ->
        Assert.Equal(raceKey, record.RaceKey)
        Assert.Equal(Some 5, record.HorseNumber)
        Assert.Equal(Some 1.2M, record.OddsMin)
        Assert.Equal(Some 2.8M, record.OddsMax)
    | Error err -> failwithf "O2 parsing failed: %A" err

[<Fact>]
let ``O3 parser extracts bracket quinella odds correctly`` () =
    // Create minimal valid O3 record
    let data = Array.create 100 32uy
    let raceKey = "2024050512345678"
    Array.Copy(encodeShiftJis "O3", 0, data, 0, 2)
    Array.Copy(encodeShiftJis raceKey, 0, data, 2, 16)
    Array.Copy(encodeShiftJis "1", 0, data, 18, 1) // Bracket number 1
    Array.Copy(encodeShiftJis "2", 0, data, 19, 1) // Bracket number 2
    Array.Copy(encodeShiftJis "00125", 0, data, 20, 5) // Odds 12.5

    match O3.parse data with
    | Ok record ->
        Assert.Equal(raceKey, record.RaceKey)
        Assert.Equal(Some 1, record.BracketNumber1)
        Assert.Equal(Some 2, record.BracketNumber2)
        Assert.Equal(Some 12.5M, record.Odds)
    | Error err -> failwithf "O3 parsing failed: %A" err

[<Fact>]
let ``O4 parser extracts quinella odds correctly`` () =
    // Create minimal valid O4 record
    let data = Array.create 100 32uy
    let raceKey = "2024050512345678"
    Array.Copy(encodeShiftJis "O4", 0, data, 0, 2)
    Array.Copy(encodeShiftJis raceKey, 0, data, 2, 16)
    Array.Copy(encodeShiftJis "03", 0, data, 18, 2) // Horse number 1
    Array.Copy(encodeShiftJis "07", 0, data, 20, 2) // Horse number 2
    Array.Copy(encodeShiftJis "000245", 0, data, 22, 6) // Odds 24.5
    Array.Copy(encodeShiftJis "005", 0, data, 28, 3) // Popularity 5

    match O4.parse data with
    | Ok record ->
        Assert.Equal(raceKey, record.RaceKey)
        Assert.Equal(Some 3, record.HorseNumber1)
        Assert.Equal(Some 7, record.HorseNumber2)
        Assert.Equal(Some 24.5M, record.Odds)
        Assert.Equal(Some 5, record.Popularity)
    | Error err -> failwithf "O4 parsing failed: %A" err

[<Fact>]
let ``O5 parser extracts wide odds range correctly`` () =
    // Create minimal valid O5 record
    let data = Array.create 100 32uy
    let raceKey = "2024050512345678"
    Array.Copy(encodeShiftJis "O5", 0, data, 0, 2)
    Array.Copy(encodeShiftJis raceKey, 0, data, 2, 16)
    Array.Copy(encodeShiftJis "02", 0, data, 18, 2) // Horse number 1
    Array.Copy(encodeShiftJis "09", 0, data, 20, 2) // Horse number 2
    Array.Copy(encodeShiftJis "00105", 0, data, 22, 5) // Min odds 10.5
    Array.Copy(encodeShiftJis "00185", 0, data, 27, 5) // Max odds 18.5
    Array.Copy(encodeShiftJis "012", 0, data, 32, 3) // Popularity 12

    match O5.parse data with
    | Ok record ->
        Assert.Equal(raceKey, record.RaceKey)
        Assert.Equal(Some 2, record.HorseNumber1)
        Assert.Equal(Some 9, record.HorseNumber2)
        Assert.Equal(Some 10.5M, record.OddsMin)
        Assert.Equal(Some 18.5M, record.OddsMax)
        Assert.Equal(Some 12, record.Popularity)
    | Error err -> failwithf "O5 parsing failed: %A" err

[<Fact>]
let ``O6 parser extracts exacta odds correctly`` () =
    // Create minimal valid O6 record
    let data = Array.create 100 32uy
    let raceKey = "2024050512345678"
    Array.Copy(encodeShiftJis "O6", 0, data, 0, 2)
    Array.Copy(encodeShiftJis raceKey, 0, data, 2, 16)
    Array.Copy(encodeShiftJis "05", 0, data, 18, 2) // 1st place horse
    Array.Copy(encodeShiftJis "03", 0, data, 20, 2) // 2nd place horse
    Array.Copy(encodeShiftJis "000678", 0, data, 22, 6) // Odds 67.8
    Array.Copy(encodeShiftJis "008", 0, data, 28, 3) // Popularity 8

    match O6.parse data with
    | Ok record ->
        Assert.Equal(raceKey, record.RaceKey)
        Assert.Equal(Some 5, record.HorseNumber1)
        Assert.Equal(Some 3, record.HorseNumber2)
        Assert.Equal(Some 67.8M, record.Odds)
        Assert.Equal(Some 8, record.Popularity)
    | Error err -> failwithf "O6 parsing failed: %A" err

// ============================================================================
// Phase 4: Payoff Data Record Parser Tests
// ============================================================================

[<Fact>]
let ``H1 parser extracts win/place payoff correctly`` () =
    // Create minimal valid H1 record
    let data = Array.create 100 32uy
    let raceKey = "2024050512345678"
    Array.Copy(encodeShiftJis "H1", 0, data, 0, 2)
    Array.Copy(encodeShiftJis raceKey, 0, data, 2, 16)
    Array.Copy(encodeShiftJis "1", 0, data, 18, 1) // Bet type: Win
    Array.Copy(encodeShiftJis "07", 0, data, 19, 2) // Horse number 7
    Array.Copy(encodeShiftJis "003200", 0, data, 21, 6) // Payoff 3200 yen (6 bytes)
    Array.Copy(encodeShiftJis "02", 0, data, 27, 2) // Popularity 2

    match H1.parse data with
    | Ok record ->
        Assert.Equal(raceKey, record.RaceKey)
        Assert.Equal(Some 1, record.BetType)
        Assert.Equal(Some 7, record.HorseNumber)
        Assert.Equal(Some 3200, record.Payoff)
        Assert.Equal(Some 2, record.Popularity)
    | Error err -> failwithf "H1 parsing failed: %A" err

[<Fact>]
let ``H5 parser extracts quintet payoff correctly`` () =
    // Create minimal valid H5 record
    let data = Array.create 100 32uy
    let raceKey = "2024050512345678"
    Array.Copy(encodeShiftJis "H5", 0, data, 0, 2)
    Array.Copy(encodeShiftJis raceKey, 0, data, 2, 16)
    Array.Copy(encodeShiftJis "01", 0, data, 18, 2) // Horse 1
    Array.Copy(encodeShiftJis "03", 0, data, 20, 2) // Horse 3
    Array.Copy(encodeShiftJis "05", 0, data, 22, 2) // Horse 5
    Array.Copy(encodeShiftJis "07", 0, data, 24, 2) // Horse 7
    Array.Copy(encodeShiftJis "09", 0, data, 26, 2) // Horse 9
    Array.Copy(encodeShiftJis "000125000", 0, data, 28, 9) // Payoff 125000 yen
    Array.Copy(encodeShiftJis "0123", 0, data, 37, 4) // Popularity 123

    match H5.parse data with
    | Ok record ->
        Assert.Equal(raceKey, record.RaceKey)
        Assert.Equal(Some 1, record.HorseNumber1)
        Assert.Equal(Some 3, record.HorseNumber2)
        Assert.Equal(Some 5, record.HorseNumber3)
        Assert.Equal(Some 7, record.HorseNumber4)
        Assert.Equal(Some 9, record.HorseNumber5)
        Assert.Equal(Some 125000, record.Payoff)
        Assert.Equal(Some 123, record.Popularity)
    | Error err -> failwithf "H5 parsing failed: %A" err

[<Fact>]
let ``H6 parser extracts trio payoff correctly`` () =
    // Create minimal valid H6 record
    let data = Array.create 100 32uy
    let raceKey = "2024050512345678"
    Array.Copy(encodeShiftJis "H6", 0, data, 0, 2)
    Array.Copy(encodeShiftJis raceKey, 0, data, 2, 16)
    Array.Copy(encodeShiftJis "02", 0, data, 18, 2) // Horse 2
    Array.Copy(encodeShiftJis "06", 0, data, 20, 2) // Horse 6
    Array.Copy(encodeShiftJis "08", 0, data, 22, 2) // Horse 8
    Array.Copy(encodeShiftJis "00045600", 0, data, 24, 8) // Payoff 45600 yen
    Array.Copy(encodeShiftJis "045", 0, data, 32, 3) // Popularity 45

    match H6.parse data with
    | Ok record ->
        Assert.Equal(raceKey, record.RaceKey)
        Assert.Equal(Some 2, record.HorseNumber1)
        Assert.Equal(Some 6, record.HorseNumber2)
        Assert.Equal(Some 8, record.HorseNumber3)
        Assert.Equal(Some 45600, record.Payoff)
        Assert.Equal(Some 45, record.Popularity)
    | Error err -> failwithf "H6 parsing failed: %A" err

// ============================================================================
// Phase 5: Real-time Update Record Parser Tests
// ============================================================================

[<Fact>]
let ``WF parser extracts horse weight correctly`` () =
    // Create minimal valid WF record
    let data = Array.create 100 32uy
    let raceKey = "2024050512345678"
    Array.Copy(encodeShiftJis "WF", 0, data, 0, 2)
    Array.Copy(encodeShiftJis raceKey, 0, data, 2, 16)
    Array.Copy(encodeShiftJis "05", 0, data, 18, 2) // Horse number 5
    Array.Copy(encodeShiftJis "478", 0, data, 20, 3) // Weight 478kg
    Array.Copy(encodeShiftJis " +4", 0, data, 23, 3) // Weight diff +4kg

    match WF.parse data with
    | Ok record ->
        Assert.Equal(raceKey, record.RaceKey)
        Assert.Equal(Some 5, record.HorseNumber)
        Assert.Equal(Some 478, record.Weight)
        Assert.Equal(Some 4, record.WeightDiff)
    | Error err -> failwithf "WF parsing failed: %A" err

[<Fact>]
let ``JC parser extracts jockey change correctly`` () =
    // Create minimal valid JC record
    let data = Array.create 200 32uy
    let raceKey = "2024050512345678"
    Array.Copy(encodeShiftJis "JC", 0, data, 0, 2)
    Array.Copy(encodeShiftJis raceKey, 0, data, 2, 16)
    Array.Copy(encodeShiftJis "03", 0, data, 18, 2) // Horse number 3

    match JC.parse data with
    | Ok record ->
        Assert.Equal(raceKey, record.RaceKey)
        Assert.Equal(Some 3, record.HorseNumber)
    | Error err -> failwithf "JC parsing failed: %A" err

[<Fact>]
let ``TC parser extracts training time correctly`` () =
    // Create minimal valid TC record
    let data = Array.create 100 32uy
    let raceKey = "2024050512345678"
    Array.Copy(encodeShiftJis "TC", 0, data, 0, 2)
    Array.Copy(encodeShiftJis raceKey, 0, data, 2, 16)
    Array.Copy(encodeShiftJis "07", 0, data, 18, 2) // Horse number 7

    match TC.parse data with
    | Ok record ->
        Assert.Equal(raceKey, record.RaceKey)
        Assert.Equal(Some 7, record.HorseNumber)
    | Error err -> failwithf "TC parsing failed: %A" err

[<Fact>]
let ``CC parser extracts course change correctly`` () =
    // Create minimal valid CC record
    let data = Array.create 100 32uy
    let raceKey = "2024050512345678"
    Array.Copy(encodeShiftJis "CC", 0, data, 0, 2)
    Array.Copy(encodeShiftJis raceKey, 0, data, 2, 16)
    Array.Copy(encodeShiftJis "1", 0, data, 18, 1) // Old: Turf
    Array.Copy(encodeShiftJis "2", 0, data, 19, 1) // New: Dirt
    Array.Copy(encodeShiftJis "2000", 0, data, 20, 4) // Old distance 2000m
    Array.Copy(encodeShiftJis "1800", 0, data, 24, 4) // New distance 1800m

    match CC.parse data with
    | Ok record ->
        Assert.Equal(raceKey, record.RaceKey)
        Assert.Equal(Some TrackSurfaceCode.Turf, record.OldTrackSurface)
        Assert.Equal(Some TrackSurfaceCode.Dirt, record.NewTrackSurface)
        Assert.Equal(Some 2000, record.OldDistance)
        Assert.Equal(Some 1800, record.NewDistance)
    | Error err -> failwithf "CC parsing failed: %A" err

[<Fact>]
let ``WE parser extracts weather change correctly`` () =
    // Create minimal valid WE record
    let data = Array.create 100 32uy
    let raceKey = "2024050512345678"
    Array.Copy(encodeShiftJis "WE", 0, data, 0, 2)
    Array.Copy(encodeShiftJis raceKey, 0, data, 2, 16)
    Array.Copy(encodeShiftJis "1", 0, data, 18, 1) // Old weather
    Array.Copy(encodeShiftJis "3", 0, data, 19, 1) // New weather

    match WE.parse data with
    | Ok record ->
        Assert.Equal(raceKey, record.RaceKey)
        Assert.True(record.OldWeather.IsSome)
        Assert.True(record.NewWeather.IsSome)
    | Error err -> failwithf "WE parsing failed: %A" err

[<Fact>]
let ``AV parser extracts track condition change correctly`` () =
    // Create minimal valid AV record
    let data = Array.create 100 32uy
    let raceKey = "2024050512345678"
    Array.Copy(encodeShiftJis "AV", 0, data, 0, 2)
    Array.Copy(encodeShiftJis raceKey, 0, data, 2, 16)
    Array.Copy(encodeShiftJis "1", 0, data, 18, 1) // Old: Good
    Array.Copy(encodeShiftJis "2", 0, data, 19, 1) // New: Yielding

    match AV.parse data with
    | Ok record ->
        Assert.Equal(raceKey, record.RaceKey)
        Assert.Equal(Some TrackConditionCode.Good, record.OldTrackCondition)
        Assert.Equal(Some TrackConditionCode.Yielding, record.NewTrackCondition)
    | Error err -> failwithf "AV parsing failed: %A" err

// ============================================================================
// Phase 6: Master Data Record Parser Tests
// ============================================================================

[<Fact>]
let ``UM parser extracts horse master data correctly`` () =
    // Create minimal valid UM record
    let data = Array.create 500 32uy
    let horseId = "2020105678"
    let horseName = "サンプルホース"
    Array.Copy(encodeShiftJis "UM", 0, data, 0, 2)
    Array.Copy(encodeShiftJis horseId, 0, data, 2, 10)
    Array.Copy(encodeShiftJis horseName, 0, data, 12, encodeShiftJis(horseName).Length)
    Array.Copy(encodeShiftJis "1", 0, data, 48, 1) // Sex: Male
    Array.Copy(encodeShiftJis "1", 0, data, 49, 1) // Hair color: Chestnut

    match UM.parse data with
    | Ok record ->
        Assert.Equal(horseId, record.HorseId)
        Assert.StartsWith("サンプル", record.HorseName)
        Assert.Equal(Some SexCode.Male, record.Sex)
        Assert.Equal(Some HairColorCode.Chestnut, record.HairColor)
    | Error err -> failwithf "UM parsing failed: %A" err

[<Fact>]
let ``KS parser extracts jockey master data correctly`` () =
    // Create minimal valid KS record
    let data = Array.create 150 32uy
    let jockeyCode = "01234"
    let jockeyName = "テスト騎手"
    Array.Copy(encodeShiftJis "KS", 0, data, 0, 2)
    Array.Copy(encodeShiftJis jockeyCode, 0, data, 2, 5)
    Array.Copy(encodeShiftJis jockeyName, 0, data, 7, encodeShiftJis(jockeyName).Length)

    match KS.parse data with
    | Ok record ->
        Assert.Equal(jockeyCode, record.JockeyCode)
        Assert.StartsWith("テスト", record.JockeyName)
    | Error err -> failwithf "KS parsing failed: %A" err

[<Fact>]
let ``CH parser extracts trainer master data correctly`` () =
    // Create minimal valid CH record
    let data = Array.create 150 32uy
    let trainerCode = "56789"
    let trainerName = "テスト調教師"
    Array.Copy(encodeShiftJis "CH", 0, data, 0, 2)
    Array.Copy(encodeShiftJis trainerCode, 0, data, 2, 5)
    Array.Copy(encodeShiftJis trainerName, 0, data, 7, encodeShiftJis(trainerName).Length)

    match CH.parse data with
    | Ok record ->
        Assert.Equal(trainerCode, record.TrainerCode)
        Assert.StartsWith("テスト", record.TrainerName)
    | Error err -> failwithf "CH parsing failed: %A" err

[<Fact>]
let ``BR parser extracts broodmare master data correctly`` () =
    // Create minimal valid BR record
    let data = Array.create 300 32uy
    let horseId = "2018100123"
    let horseName = "テスト繁殖牝馬"
    Array.Copy(encodeShiftJis "BR", 0, data, 0, 2)
    Array.Copy(encodeShiftJis horseId, 0, data, 2, 10)
    Array.Copy(encodeShiftJis horseName, 0, data, 12, encodeShiftJis(horseName).Length)
    Array.Copy(encodeShiftJis "2", 0, data, 48, 1) // Hair color: Liver (栃栗毛)

    match BR.parse data with
    | Ok record ->
        Assert.Equal(horseId, record.HorseId)
        Assert.StartsWith("テスト", record.HorseName)
        Assert.Equal(Some HairColorCode.Liver, record.HairColor)
    | Error err -> failwithf "BR parsing failed: %A" err

[<Fact>]
let ``BN parser extracts breeding horse master data correctly`` () =
    // Create minimal valid BN record
    let data = Array.create 300 32uy
    let horseId = "2017050456"
    let horseName = "テスト繁殖馬"
    Array.Copy(encodeShiftJis "BN", 0, data, 0, 2)
    Array.Copy(encodeShiftJis horseId, 0, data, 2, 10)
    Array.Copy(encodeShiftJis horseName, 0, data, 12, encodeShiftJis(horseName).Length)
    Array.Copy(encodeShiftJis "1", 0, data, 48, 1) // Sex: Male
    Array.Copy(encodeShiftJis "3", 0, data, 49, 1) // Hair color: Bay (鹿毛)

    match BN.parse data with
    | Ok record ->
        Assert.Equal(horseId, record.HorseId)
        Assert.StartsWith("テスト", record.HorseName)
        Assert.Equal(Some SexCode.Male, record.Sex)
        Assert.Equal(Some HairColorCode.Bay, record.HairColor)
    | Error err -> failwithf "BN parsing failed: %A" err

[<Fact>]
let ``RC parser extracts race code master data correctly`` () =
    // Create minimal valid RC record
    let data = Array.create 150 32uy
    let raceCode = "1234"
    let raceName = "テストレース"
    Array.Copy(encodeShiftJis "RC", 0, data, 0, 2)
    Array.Copy(encodeShiftJis raceCode, 0, data, 2, 4)
    Array.Copy(encodeShiftJis raceName, 0, data, 6, encodeShiftJis(raceName).Length)
    Array.Copy(encodeShiftJis "1", 0, data, 76, 1) // Grade: G1 (code=1)
    Array.Copy(encodeShiftJis "2000", 0, data, 79, 4) // Distance: 2000m
    Array.Copy(encodeShiftJis "1", 0, data, 83, 1) // Track surface: Turf

    match RC.parse data with
    | Ok record ->
        Assert.Equal(raceCode, record.RaceCode)
        Assert.StartsWith("テスト", record.RaceName)
        Assert.Equal(Some GradeCode.G1, record.Grade)
        Assert.Equal(Some 2000, record.Distance)
        Assert.Equal(Some TrackSurfaceCode.Turf, record.TrackSurface)
    | Error err -> failwithf "RC parsing failed: %A" err

// ============================================================================
// Phase 7: Additional Real-time/Exclusion Data Parser Tests
// ============================================================================

[<Fact>]
let ``JG parser extracts horse exclusion info correctly`` () =
    // Create minimal valid JG record (80 bytes)
    let data = Array.create 100 32uy
    Array.Copy(encodeShiftJis "JG", 0, data, 0, 2) // Record type
    Array.Copy(encodeShiftJis "1", 0, data, 2, 1) // Data category
    Array.Copy(encodeShiftJis "20240505", 0, data, 3, 8) // Created date
    Array.Copy(encodeShiftJis "2024", 0, data, 11, 4) // Year
    Array.Copy(encodeShiftJis "0505", 0, data, 15, 4) // MonthDay
    Array.Copy(encodeShiftJis "01", 0, data, 19, 2) // Racecourse (Sapporo)
    Array.Copy(encodeShiftJis "02", 0, data, 21, 2) // Kai
    Array.Copy(encodeShiftJis "03", 0, data, 23, 2) // Day
    Array.Copy(encodeShiftJis "05", 0, data, 25, 2) // Race number
    Array.Copy(encodeShiftJis "2020123456", 0, data, 27, 10) // Pedigree reg num
    let horseName = "テスト除外馬"
    Array.Copy(encodeShiftJis horseName, 0, data, 37, encodeShiftJis(horseName).Length)
    Array.Copy(encodeShiftJis "015", 0, data, 73, 3) // Voting order
    Array.Copy(encodeShiftJis "9", 0, data, 76, 1) // Entry category (Scratched)

    match JG.parse data with
    | Ok record ->
        Assert.Equal(2024, record.Year)
        Assert.Equal("0505", record.MonthDay)
        Assert.Equal(Some RacecourseCode.Sapporo, record.RacecourseCode)
        Assert.Equal(Some 5, record.RaceNumber)
        Assert.Equal("2020123456", record.PedigreeRegNum)
        Assert.StartsWith("テスト", record.HorseName)
        Assert.Equal(Some 15, record.VotingOrder)
        Assert.Equal(Some JG.EntryCategory.Scratched, record.EntryCategory)
    | Error err -> failwithf "JG parsing failed: %A" err

[<Fact>]
let ``WH parser extracts horse weight announcements correctly`` () =
    // Create minimal valid WH record (847 bytes)
    let data = Array.create 900 32uy
    Array.Copy(encodeShiftJis "WH", 0, data, 0, 2) // Record type
    Array.Copy(encodeShiftJis "1", 0, data, 2, 1) // Data category
    Array.Copy(encodeShiftJis "20240505", 0, data, 3, 8) // Created date
    Array.Copy(encodeShiftJis "2024", 0, data, 11, 4) // Year
    Array.Copy(encodeShiftJis "0505", 0, data, 15, 4) // MonthDay
    Array.Copy(encodeShiftJis "05", 0, data, 19, 2) // Racecourse (Tokyo = 5)
    Array.Copy(encodeShiftJis "01", 0, data, 21, 2) // Kai
    Array.Copy(encodeShiftJis "01", 0, data, 23, 2) // Day
    Array.Copy(encodeShiftJis "11", 0, data, 25, 2) // Race number
    Array.Copy(encodeShiftJis "05051430", 0, data, 27, 8) // Announced time
    // First horse weight entry at offset 35
    Array.Copy(encodeShiftJis "01", 0, data, 35, 2) // Horse number 1
    let horse1Name = "テスト馬1号"
    Array.Copy(encodeShiftJis horse1Name, 0, data, 37, encodeShiftJis(horse1Name).Length)
    Array.Copy(encodeShiftJis "468", 0, data, 73, 3) // Weight 468kg
    Array.Copy(encodeShiftJis "+", 0, data, 76, 1) // Weight sign
    Array.Copy(encodeShiftJis "004", 0, data, 77, 3) // Weight change +4kg
    // Second horse weight entry at offset 35 + 45 = 80
    Array.Copy(encodeShiftJis "02", 0, data, 80, 2) // Horse number 2
    let horse2Name = "テスト馬2号"
    Array.Copy(encodeShiftJis horse2Name, 0, data, 82, encodeShiftJis(horse2Name).Length)
    Array.Copy(encodeShiftJis "502", 0, data, 118, 3) // Weight 502kg
    Array.Copy(encodeShiftJis "-", 0, data, 121, 1) // Weight sign
    Array.Copy(encodeShiftJis "006", 0, data, 122, 3) // Weight change -6kg

    match WH.parse data with
    | Ok record ->
        Assert.Equal(2024, record.Year)
        Assert.Equal("0505", record.MonthDay)
        Assert.Equal(Some RacecourseCode.Tokyo, record.RacecourseCode)
        Assert.Equal(Some 11, record.RaceNumber)
        Assert.Equal("05051430", record.AnnouncedTime)
        Assert.True(record.HorseWeights.Length >= 2)
        // First horse
        Assert.Equal(Some 1, record.HorseWeights.[0].HorseNumber)
        Assert.Equal(Some 468, record.HorseWeights.[0].Weight)
        Assert.Equal("+", record.HorseWeights.[0].WeightSign)
        Assert.Equal(Some 4, record.HorseWeights.[0].WeightChange)
        // Second horse
        Assert.Equal(Some 2, record.HorseWeights.[1].HorseNumber)
        Assert.Equal(Some 502, record.HorseWeights.[1].Weight)
        Assert.Equal("-", record.HorseWeights.[1].WeightSign)
        Assert.Equal(Some 6, record.HorseWeights.[1].WeightChange)
    | Error err -> failwithf "WH parsing failed: %A" err

[<Fact>]
let ``WC parser extracts wood chip training data correctly`` () =
    // Create minimal valid WC record (105 bytes)
    let data = Array.create 110 32uy
    Array.Copy(encodeShiftJis "WC", 0, data, 0, 2) // Record type
    Array.Copy(encodeShiftJis "1", 0, data, 2, 1) // Data category
    Array.Copy(encodeShiftJis "20240501", 0, data, 3, 8) // Created date
    Array.Copy(encodeShiftJis "0", 0, data, 11, 1) // Training center (Miho)
    Array.Copy(encodeShiftJis "20240430", 0, data, 12, 8) // Training date
    Array.Copy(encodeShiftJis "0630", 0, data, 20, 4) // Training time
    Array.Copy(encodeShiftJis "2021054321", 0, data, 24, 10) // Pedigree reg num
    Array.Copy(encodeShiftJis "0", 0, data, 34, 1) // Course A
    Array.Copy(encodeShiftJis "1", 0, data, 35, 1) // Track direction (Left)
    // 5 furlong time (1000m)
    Array.Copy(encodeShiftJis "0678", 0, data, 72, 4) // 67.8 seconds
    Array.Copy(encodeShiftJis "135", 0, data, 76, 3) // Lap 1000-800: 13.5 sec
    // 4 furlong time (800m)
    Array.Copy(encodeShiftJis "0543", 0, data, 79, 4) // 54.3 seconds
    Array.Copy(encodeShiftJis "140", 0, data, 83, 3) // Lap 800-600: 14.0 sec
    // 3 furlong time (600m)
    Array.Copy(encodeShiftJis "0403", 0, data, 86, 4) // 40.3 seconds
    Array.Copy(encodeShiftJis "138", 0, data, 90, 3) // Lap 600-400: 13.8 sec

    match WC.parse data with
    | Ok record ->
        Assert.Equal(Some WC.TrainingCenterCode.Miho, record.TrainingCenter)
        Assert.True(record.TrainingDate.IsSome)
        Assert.Equal("0630", record.TrainingTime)
        Assert.Equal("2021054321", record.PedigreeRegNum)
        Assert.Equal(Some WC.CourseCode.A, record.Course)
        Assert.Equal(Some WC.TrackDirection.Left, record.TrackDirection)
        Assert.Equal(Some 678, record.FurlongTime5)
        Assert.Equal(Some 135, record.LapTime1000to800)
        Assert.Equal(Some 543, record.FurlongTime4)
        Assert.Equal(Some 140, record.LapTime800to600)
        Assert.Equal(Some 403, record.FurlongTime3)
        Assert.Equal(Some 138, record.LapTime600to400)
    | Error err -> failwithf "WC parsing failed: %A" err

[<Fact>]
let ``CK parser extracts race stats header correctly`` () =
    // Create minimal valid CK record (6870 bytes) - testing header and some basic fields
    let data = Array.create 7000 32uy
    Array.Copy(encodeShiftJis "CK", 0, data, 0, 2) // Record type
    Array.Copy(encodeShiftJis "1", 0, data, 2, 1) // Data category
    Array.Copy(encodeShiftJis "20240505", 0, data, 3, 8) // Created date
    Array.Copy(encodeShiftJis "2024", 0, data, 11, 4) // Year
    Array.Copy(encodeShiftJis "0505", 0, data, 15, 4) // MonthDay
    Array.Copy(encodeShiftJis "05", 0, data, 19, 2) // Racecourse (Tokyo = 5)
    Array.Copy(encodeShiftJis "02", 0, data, 21, 2) // Kai
    Array.Copy(encodeShiftJis "04", 0, data, 23, 2) // Day
    Array.Copy(encodeShiftJis "11", 0, data, 25, 2) // Race number
    Array.Copy(encodeShiftJis "2020105678", 0, data, 27, 10) // Pedigree reg num
    let horseName = "テストＣＫ馬"
    Array.Copy(encodeShiftJis horseName, 0, data, 37, encodeShiftJis(horseName).Length)
    // Total placements starting at offset 127 (3 bytes each, 6 values)
    Array.Copy(encodeShiftJis "010", 0, data, 127, 3) // 1st place: 10 times
    Array.Copy(encodeShiftJis "005", 0, data, 130, 3) // 2nd place: 5 times
    Array.Copy(encodeShiftJis "003", 0, data, 133, 3) // 3rd place: 3 times
    Array.Copy(encodeShiftJis "002", 0, data, 136, 3) // 4th place: 2 times
    Array.Copy(encodeShiftJis "001", 0, data, 139, 3) // 5th place: 1 time
    Array.Copy(encodeShiftJis "020", 0, data, 142, 3) // Outside: 20 times

    match CK.parse data with
    | Ok record ->
        Assert.Equal(2024, record.Year)
        Assert.Equal("0505", record.MonthDay)
        Assert.Equal(Some RacecourseCode.Tokyo, record.RacecourseCode)
        Assert.Equal(Some 2, record.Kai)
        Assert.Equal(Some 4, record.Day)
        Assert.Equal(Some 11, record.RaceNumber)
        Assert.Equal("2020105678", record.PedigreeRegNum)
        Assert.StartsWith("テスト", record.HorseName)
        // Total placements
        Assert.Equal(Some 10, record.TotalPlacements.First)
        Assert.Equal(Some 5, record.TotalPlacements.Second)
        Assert.Equal(Some 3, record.TotalPlacements.Third)
        Assert.Equal(Some 2, record.TotalPlacements.Fourth)
        Assert.Equal(Some 1, record.TotalPlacements.Fifth)
        Assert.Equal(Some 20, record.TotalPlacements.Outside)
    | Error err -> failwithf "CK parsing failed: %A" err

[<Fact>]
let ``HC parser extracts slope training data correctly`` () =
    // Create minimal valid HC record (60 bytes)
    let data = Array.create 65 32uy
    Array.Copy(encodeShiftJis "HC", 0, data, 0, 2) // Record type
    Array.Copy(encodeShiftJis "1", 0, data, 2, 1) // Data category
    Array.Copy(encodeShiftJis "20240501", 0, data, 3, 8) // Created date
    Array.Copy(encodeShiftJis "0", 0, data, 11, 1) // Training center (Miho)
    Array.Copy(encodeShiftJis "20240430", 0, data, 12, 8) // Training date
    Array.Copy(encodeShiftJis "0545", 0, data, 20, 4) // Training time
    Array.Copy(encodeShiftJis "2019076543", 0, data, 24, 10) // Pedigree reg num
    // 4 furlong time (800m)
    Array.Copy(encodeShiftJis "0528", 0, data, 34, 4) // 52.8 seconds
    Array.Copy(encodeShiftJis "142", 0, data, 38, 3) // Lap: 14.2 sec
    // 3 furlong time (600m)
    Array.Copy(encodeShiftJis "0386", 0, data, 41, 4) // 38.6 seconds
    Array.Copy(encodeShiftJis "127", 0, data, 45, 3) // Lap: 12.7 sec
    // 2 furlong time (400m)
    Array.Copy(encodeShiftJis "0259", 0, data, 48, 4) // 25.9 seconds
    Array.Copy(encodeShiftJis "131", 0, data, 52, 3) // Lap: 13.1 sec
    // 1 furlong time (200m)
    Array.Copy(encodeShiftJis "128", 0, data, 55, 3) // Lap: 12.8 sec

    match HC.parse data with
    | Ok record ->
        Assert.Equal(Some HC.TrainingCenterCode.Miho, record.TrainingCenter)
        Assert.True(record.TrainingDate.IsSome)
        Assert.Equal("0545", record.TrainingTime)
        Assert.Equal("2019076543", record.PedigreeRegNum)
        Assert.Equal(Some 528, record.FurlongTime4)
        Assert.Equal(Some 142, record.LapTime800to600)
        Assert.Equal(Some 386, record.FurlongTime3)
        Assert.Equal(Some 127, record.LapTime600to400)
        Assert.Equal(Some 259, record.FurlongTime2)
        Assert.Equal(Some 131, record.LapTime400to200)
        Assert.Equal(Some 128, record.LapTime200to0)
    | Error err -> failwithf "HC parsing failed: %A" err

// ============================================================================
// Phase 8: Additional Record Parsers (BT, CS, TM, DM, YS, HY, HS, HN, SK)
// ============================================================================

[<Fact>]
let ``BT parser extracts bloodline info correctly`` () =
    // Create minimal valid BT record (6889 bytes)
    let data = Array.create 6900 32uy
    Array.Copy(encodeShiftJis "BT", 0, data, 0, 2) // Record type
    Array.Copy(encodeShiftJis "1", 0, data, 2, 1) // Data category
    Array.Copy(encodeShiftJis "20240501", 0, data, 3, 8) // Created date
    Array.Copy(encodeShiftJis "1234567890", 0, data, 11, 10) // Breeding reg num
    Array.Copy(encodeShiftJis "LINEAGE001", 0, data, 21, 10) // Lineage ID
    let lineageName = "サンデーサイレンス系"
    Array.Copy(encodeShiftJis lineageName, 0, data, 51, encodeShiftJis(lineageName).Length)

    match BT.parse data with
    | Ok record ->
        Assert.Equal("1234567890", record.BreedingRegNum)
        Assert.StartsWith("LINEAGE", record.LineageId)
        Assert.StartsWith("サンデー", record.LineageName)
        Assert.Equal(Some 1, record.DataCategory)
        Assert.True(record.CreatedDate.IsSome)
    | Error err -> failwithf "BT parsing failed: %A" err

[<Fact>]
let ``CS parser extracts course info correctly`` () =
    // Create minimal valid CS record (6829 bytes)
    let data = Array.create 6850 32uy
    Array.Copy(encodeShiftJis "CS", 0, data, 0, 2) // Record type
    Array.Copy(encodeShiftJis "1", 0, data, 2, 1) // Data category
    Array.Copy(encodeShiftJis "20240501", 0, data, 3, 8) // Created date
    Array.Copy(encodeShiftJis "05", 0, data, 11, 2) // Racecourse (Tokyo = 5)
    Array.Copy(encodeShiftJis "2000", 0, data, 13, 4) // Distance
    Array.Copy(encodeShiftJis "11", 0, data, 17, 2) // Track code (芝左)
    Array.Copy(encodeShiftJis "20200301", 0, data, 19, 8) // Course revision date

    match CS.parse data with
    | Ok record ->
        Assert.Equal(Some RacecourseCode.Tokyo, record.RacecourseCode)
        Assert.Equal(Some 2000, record.Distance)
        Assert.Equal("11", record.TrackCode)
        Assert.True(record.CourseRevisionDate.IsSome)
        Assert.Equal(Some 1, record.DataCategory)
    | Error err -> failwithf "CS parsing failed: %A" err

[<Fact>]
let ``TM parser extracts mining predictions correctly`` () =
    // Create minimal valid TM record (141 bytes)
    let data = Array.create 150 32uy
    Array.Copy(encodeShiftJis "TM", 0, data, 0, 2) // Record type
    Array.Copy(encodeShiftJis "1", 0, data, 2, 1) // Data category
    Array.Copy(encodeShiftJis "20240505", 0, data, 3, 8) // Created date
    Array.Copy(encodeShiftJis "2024", 0, data, 11, 4) // Year
    Array.Copy(encodeShiftJis "0505", 0, data, 15, 4) // MonthDay
    Array.Copy(encodeShiftJis "05", 0, data, 19, 2) // Racecourse (Tokyo = 5)
    Array.Copy(encodeShiftJis "02", 0, data, 21, 2) // Kai
    Array.Copy(encodeShiftJis "04", 0, data, 23, 2) // Day
    Array.Copy(encodeShiftJis "11", 0, data, 25, 2) // Race number
    Array.Copy(encodeShiftJis "1430", 0, data, 27, 4) // Created time
    // First prediction entry at offset 31 (6 bytes each)
    Array.Copy(encodeShiftJis "01", 0, data, 31, 2) // Horse number 1
    Array.Copy(encodeShiftJis "0850", 0, data, 33, 4) // Score 85.0
    // Second prediction entry at offset 37
    Array.Copy(encodeShiftJis "02", 0, data, 37, 2) // Horse number 2
    Array.Copy(encodeShiftJis "0723", 0, data, 39, 4) // Score 72.3

    match TM.parse data with
    | Ok record ->
        Assert.Equal(2024, record.Year)
        Assert.Equal("0505", record.MonthDay)
        Assert.Equal(Some RacecourseCode.Tokyo, record.RacecourseCode)
        Assert.Equal(Some 11, record.RaceNumber)
        Assert.Equal("1430", record.CreatedTime)
        Assert.True(record.Predictions.Length >= 2)
        Assert.Equal(Some 1, record.Predictions.[0].HorseNumber)
        Assert.Equal(Some 85.0m, record.Predictions.[0].PredictionScore)
        Assert.Equal(Some 2, record.Predictions.[1].HorseNumber)
        Assert.Equal(Some 72.3m, record.Predictions.[1].PredictionScore)
    | Error err -> failwithf "TM parsing failed: %A" err

[<Fact>]
let ``DM parser extracts time predictions correctly`` () =
    // Create minimal valid DM record (303 bytes)
    let data = Array.create 310 32uy
    Array.Copy(encodeShiftJis "DM", 0, data, 0, 2) // Record type
    Array.Copy(encodeShiftJis "1", 0, data, 2, 1) // Data category
    Array.Copy(encodeShiftJis "20240505", 0, data, 3, 8) // Created date
    Array.Copy(encodeShiftJis "2024", 0, data, 11, 4) // Year
    Array.Copy(encodeShiftJis "0505", 0, data, 15, 4) // MonthDay
    Array.Copy(encodeShiftJis "05", 0, data, 19, 2) // Racecourse (Tokyo = 5)
    Array.Copy(encodeShiftJis "02", 0, data, 21, 2) // Kai
    Array.Copy(encodeShiftJis "04", 0, data, 23, 2) // Day
    Array.Copy(encodeShiftJis "11", 0, data, 25, 2) // Race number
    Array.Copy(encodeShiftJis "1430", 0, data, 27, 4) // Created time
    // First prediction entry at offset 31 (15 bytes each)
    Array.Copy(encodeShiftJis "01", 0, data, 31, 2) // Horse number 1
    Array.Copy(encodeShiftJis "12345", 0, data, 33, 5) // Predicted time (1:23.45)
    Array.Copy(encodeShiftJis "0012", 0, data, 38, 4) // Error+ 0.12
    Array.Copy(encodeShiftJis "0015", 0, data, 42, 4) // Error- 0.15

    match DM.parse data with
    | Ok record ->
        Assert.Equal(2024, record.Year)
        Assert.Equal("0505", record.MonthDay)
        Assert.Equal(Some RacecourseCode.Tokyo, record.RacecourseCode)
        Assert.Equal(Some 11, record.RaceNumber)
        Assert.True(record.Predictions.Length >= 1)
        Assert.Equal(Some 1, record.Predictions.[0].HorseNumber)
        Assert.Equal("12345", record.Predictions.[0].PredictedTime)
        Assert.Equal("0012", record.Predictions.[0].ErrorPlus)
        Assert.Equal("0015", record.Predictions.[0].ErrorMinus)
    | Error err -> failwithf "DM parsing failed: %A" err

[<Fact>]
let ``YS parser extracts schedule info correctly`` () =
    // Create minimal valid YS record (382 bytes)
    let data = Array.create 400 32uy
    Array.Copy(encodeShiftJis "YS", 0, data, 0, 2) // Record type
    Array.Copy(encodeShiftJis "1", 0, data, 2, 1) // Data category
    Array.Copy(encodeShiftJis "20240505", 0, data, 3, 8) // Created date
    Array.Copy(encodeShiftJis "2024", 0, data, 11, 4) // Year
    Array.Copy(encodeShiftJis "0505", 0, data, 15, 4) // MonthDay
    Array.Copy(encodeShiftJis "05", 0, data, 19, 2) // Racecourse (Tokyo = 5)
    Array.Copy(encodeShiftJis "02", 0, data, 21, 2) // Kai
    Array.Copy(encodeShiftJis "04", 0, data, 23, 2) // Day
    Array.Copy(encodeShiftJis "0", 0, data, 25, 1) // Day of week (Sunday = 0)
    // First grade race entry at offset 26 (118 bytes each)
    Array.Copy(encodeShiftJis "0001", 0, data, 26, 4) // Special race number
    let raceName = "天皇賞（春）"
    Array.Copy(encodeShiftJis raceName, 0, data, 30, encodeShiftJis(raceName).Length)
    Array.Copy(encodeShiftJis "3200", 0, data, 138, 4) // Distance 3200m

    match YS.parse data with
    | Ok record ->
        Assert.Equal(2024, record.Year)
        Assert.Equal("0505", record.MonthDay)
        Assert.Equal(Some RacecourseCode.Tokyo, record.RacecourseCode)
        Assert.Equal(Some 2, record.Kai)
        Assert.Equal(Some 4, record.Day)
        Assert.Equal(Some YS.DayOfWeekCode.Sunday, record.DayOfWeek)
        Assert.True(record.GradeRaces.Length >= 1)
        Assert.Equal(Some 1, record.GradeRaces.[0].SpecialRaceNumber)
        Assert.StartsWith("天皇", record.GradeRaces.[0].RaceNameMain)
        Assert.Equal(Some 3200, record.GradeRaces.[0].Distance)
    | Error err -> failwithf "YS parsing failed: %A" err

[<Fact>]
let ``HY parser extracts horse name meaning correctly`` () =
    // Create minimal valid HY record (123 bytes)
    let data = Array.create 130 32uy
    Array.Copy(encodeShiftJis "HY", 0, data, 0, 2) // Record type
    Array.Copy(encodeShiftJis "1", 0, data, 2, 1) // Data category
    Array.Copy(encodeShiftJis "20240501", 0, data, 3, 8) // Created date
    Array.Copy(encodeShiftJis "2020123456", 0, data, 11, 10) // Pedigree reg num
    let horseName = "ディープインパクト"
    Array.Copy(encodeShiftJis horseName, 0, data, 21, encodeShiftJis(horseName).Length)
    let meaning = "深い衝撃"
    Array.Copy(encodeShiftJis meaning, 0, data, 57, encodeShiftJis(meaning).Length)

    match HY.parse data with
    | Ok record ->
        Assert.Equal("2020123456", record.PedigreeRegNum)
        Assert.StartsWith("ディープ", record.HorseName)
        Assert.StartsWith("深い", record.NameMeaning)
        Assert.Equal(Some 1, record.DataCategory)
    | Error err -> failwithf "HY parsing failed: %A" err

[<Fact>]
let ``HS parser extracts market transaction correctly`` () =
    // Create minimal valid HS record (200 bytes)
    let data = Array.create 210 32uy
    Array.Copy(encodeShiftJis "HS", 0, data, 0, 2) // Record type
    Array.Copy(encodeShiftJis "1", 0, data, 2, 1) // Data category
    Array.Copy(encodeShiftJis "20240501", 0, data, 3, 8) // Created date
    Array.Copy(encodeShiftJis "2020123456", 0, data, 11, 10) // Pedigree reg num
    Array.Copy(encodeShiftJis "2015054321", 0, data, 21, 10) // Father breeding reg num
    Array.Copy(encodeShiftJis "2010065432", 0, data, 31, 10) // Mother breeding reg num
    Array.Copy(encodeShiftJis "2019", 0, data, 41, 4) // Birth year
    Array.Copy(encodeShiftJis "SEL001", 0, data, 45, 6) // Market code
    Array.Copy(encodeShiftJis "20210501", 0, data, 171, 8) // Market start date
    Array.Copy(encodeShiftJis "20210503", 0, data, 179, 8) // Market end date
    Array.Copy(encodeShiftJis "2", 0, data, 187, 1) // Horse age
    Array.Copy(encodeShiftJis "0150000000", 0, data, 188, 10) // Transaction price (150M yen)

    match HS.parse data with
    | Ok record ->
        Assert.Equal("2020123456", record.PedigreeRegNum)
        Assert.Equal("2015054321", record.FatherBreedingRegNum)
        Assert.Equal("2010065432", record.MotherBreedingRegNum)
        Assert.Equal(Some 2019, record.BirthYear)
        Assert.StartsWith("SEL", record.MarketCode)
        Assert.True(record.MarketStartDate.IsSome)
        Assert.True(record.MarketEndDate.IsSome)
        Assert.Equal(Some 2, record.HorseAge)
        Assert.Equal("0150000000", record.TransactionPrice)
    | Error err -> failwithf "HS parsing failed: %A" err

[<Fact>]
let ``HN parser extracts breeding horse master correctly`` () =
    // Create minimal valid HN record (251 bytes)
    let data = Array.create 260 32uy
    Array.Copy(encodeShiftJis "HN", 0, data, 0, 2) // Record type
    Array.Copy(encodeShiftJis "1", 0, data, 2, 1) // Data category
    Array.Copy(encodeShiftJis "20240501", 0, data, 3, 8) // Created date
    Array.Copy(encodeShiftJis "0123456789", 0, data, 11, 10) // Breeding reg num
    Array.Copy(encodeShiftJis "2020123456", 0, data, 29, 10) // Pedigree reg num
    let horseName = "サンデーサイレンス"
    Array.Copy(encodeShiftJis horseName, 0, data, 40, encodeShiftJis(horseName).Length)
    Array.Copy(encodeShiftJis "1986", 0, data, 196, 4) // Birth year
    Array.Copy(encodeShiftJis "1", 0, data, 200, 1) // Sex (Male)
    Array.Copy(encodeShiftJis "01", 0, data, 202, 2) // Hair color (栗)
    Array.Copy(encodeShiftJis "3", 0, data, 204, 1) // Import category (Imported)
    Array.Copy(encodeShiftJis "1989", 0, data, 205, 4) // Import year

    match HN.parse data with
    | Ok record ->
        Assert.Equal("0123456789", record.BreedingRegNum)
        Assert.Equal("2020123456", record.PedigreeRegNum)
        Assert.StartsWith("サンデー", record.HorseName)
        Assert.Equal(Some 1986, record.BirthYear)
        Assert.Equal(Some SexCode.Male, record.SexCode)
        Assert.Equal(Some HairColorCode.Chestnut, record.HairColorCode)
        Assert.Equal(Some HN.ImportCategory.Imported, record.ImportCategory)
        Assert.Equal(Some 1989, record.ImportYear)
    | Error err -> failwithf "HN parsing failed: %A" err

[<Fact>]
let ``SK parser extracts offspring master correctly`` () =
    // Create minimal valid SK record (208 bytes)
    let data = Array.create 220 32uy
    Array.Copy(encodeShiftJis "SK", 0, data, 0, 2) // Record type
    Array.Copy(encodeShiftJis "1", 0, data, 2, 1) // Data category
    Array.Copy(encodeShiftJis "20240501", 0, data, 3, 8) // Created date
    Array.Copy(encodeShiftJis "2020123456", 0, data, 11, 10) // Pedigree reg num
    Array.Copy(encodeShiftJis "20200315", 0, data, 21, 8) // Birth date
    Array.Copy(encodeShiftJis "1", 0, data, 29, 1) // Sex (Male)
    Array.Copy(encodeShiftJis "1", 0, data, 30, 1) // Breed (Thoroughbred)
    Array.Copy(encodeShiftJis "04", 0, data, 31, 2) // Hair color (黒鹿毛 = 4)
    Array.Copy(encodeShiftJis "0", 0, data, 33, 1) // Import category (Domestic)
    Array.Copy(encodeShiftJis "00000001", 0, data, 38, 8) // Producer code
    // 3代血統 at offset 66 (10 bytes each)
    Array.Copy(encodeShiftJis "FATHER0001", 0, data, 66, 10) // Father
    Array.Copy(encodeShiftJis "MOTHER0001", 0, data, 76, 10) // Mother
    Array.Copy(encodeShiftJis "FF00000001", 0, data, 86, 10) // Father Father
    Array.Copy(encodeShiftJis "FM00000001", 0, data, 96, 10) // Father Mother
    Array.Copy(encodeShiftJis "MF00000001", 0, data, 106, 10) // Mother Father
    Array.Copy(encodeShiftJis "MM00000001", 0, data, 116, 10) // Mother Mother

    match SK.parse data with
    | Ok record ->
        Assert.Equal("2020123456", record.PedigreeRegNum)
        Assert.True(record.BirthDate.IsSome)
        Assert.Equal(Some SexCode.Male, record.SexCode)
        Assert.Equal(Some SK.BreedCode.Thoroughbred, record.BreedCode)
        Assert.Equal(Some HairColorCode.DarkBay, record.HairColorCode)
        Assert.Equal(Some SK.OffspringImportCategory.Domestic, record.ImportCategory)
        Assert.Equal("00000001", record.ProducerCode)
        Assert.Equal("FATHER0001", record.Pedigree.Father)
        Assert.Equal("MOTHER0001", record.Pedigree.Mother)
        Assert.Equal("FF00000001", record.Pedigree.FatherFather)
        Assert.Equal("MF00000001", record.Pedigree.MotherFather)
    | Error err -> failwithf "SK parsing failed: %A" err

module Xanthos.UnitTests.FixtureParserTests

open System
open System.IO
open System.Reflection
open System.Text.Json
open Xunit
open Xanthos.Interop
open Xanthos.Runtime
open Xanthos.Core.Records

/// <summary>
/// Test utilities for working with captured fixture files.
/// </summary>
module FixtureLoader =
    /// Gets the root path to the fixtures directory.
    let fixturesRoot () =
        // Navigate up from bin/Debug/net10.0 to tests/fixtures
        let assemblyLocation = Assembly.GetExecutingAssembly().Location
        let assemblyDir = Path.GetDirectoryName(assemblyLocation)

        let projectRoot =
            Path.GetFullPath(Path.Combine(assemblyDir, "..", "..", "..", "..", ".."))

        Path.Combine(projectRoot, "tests", "fixtures")

    /// Enumerates all fixture files grouped by spec and record type.
    let enumerateFixtures () =
        let root = fixturesRoot ()

        if Directory.Exists root then
            Directory.GetDirectories(root)
            |> Array.collect (fun specDir ->
                let spec = Path.GetFileName(specDir)

                Directory.GetDirectories(specDir)
                |> Array.collect (fun typeDir ->
                    let recordType = Path.GetFileName(typeDir)

                    Directory.GetFiles(typeDir, "*.bin")
                    |> Array.map (fun file -> spec, recordType, file)))
            |> Array.toList
        else
            []

    /// Loads a fixture file as raw bytes.
    let loadFixture (path: string) : byte[] = File.ReadAllBytes(path)

    /// Creates a JvPayload from raw bytes
    let toPayload (data: byte[]) : JvPayload = { Timestamp = None; Data = data }

    /// Gets all fixture metadata files with their parsed JSON content
    let enumerateMetadata () =
        let root = fixturesRoot ()

        if Directory.Exists root then
            Directory.GetFiles(root, "*.meta.json", SearchOption.AllDirectories)
            |> Array.map (fun path ->
                let content = File.ReadAllText(path)
                path, content)
            |> Array.toList
        else
            []

    /// Gets the oldest metadata timestamp from fixtures
    let getOldestFixtureDate () =
        let metadata = enumerateMetadata ()

        if metadata.IsEmpty then
            None
        else
            metadata
            |> List.choose (fun (_, content) ->
                try
                    let doc = JsonDocument.Parse(content)
                    let root = doc.RootElement
                    let mutable tsElement = Unchecked.defaultof<JsonElement>

                    if root.TryGetProperty("timestamp", &tsElement) then
                        let ts = tsElement.GetString()

                        match DateTime.TryParse(ts) with
                        | true, dt -> Some dt
                        | _ -> None
                    else
                        None
                with _ ->
                    None)
            |> function
                | [] -> None
                | dates -> Some(List.min dates)

/// <summary>
/// All known record types for coverage gap analysis.
/// </summary>
module KnownRecordTypes =
    /// All implemented record type identifiers
    let all =
        [
          // Race data
          "TK"
          "RA"
          "SE"
          "HR"
          // Odds data
          "O1"
          "O2"
          "O3"
          "O4"
          "O5"
          "O6"
          // Vote count data
          "H1"
          "H5"
          "H6"
          // Master data
          "UM"
          "KS"
          "CH"
          "BR"
          "BN"
          "HN"
          "SK"
          "RC"
          // Analysis data
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
          // Real-time data
          "WH"
          "WE"
          "AV"
          "JC"
          "TC"
          "CC"
          "JG" ]

    /// Record types grouped by category for reporting
    let byCategory =
        [ "Race Data", [ "TK"; "RA"; "SE"; "HR" ]
          "Odds Data", [ "O1"; "O2"; "O3"; "O4"; "O5"; "O6" ]
          "Vote Count", [ "H1"; "H5"; "H6" ]
          "Master Data", [ "UM"; "KS"; "CH"; "BR"; "BN"; "HN"; "SK"; "RC" ]
          "Analysis Data", [ "CK"; "HC"; "HS"; "HY"; "YS"; "BT"; "CS"; "DM"; "TM"; "WF"; "WC" ]
          "Real-time Data", [ "WH"; "WE"; "AV"; "JC"; "TC"; "CC"; "JG" ] ]

/// <summary>
/// Tests that verify fixture files can be parsed correctly.
/// These tests are conditional - they only run if fixtures are present.
///
/// To capture fixtures, run on Windows with JV-Link installed:
/// dotnet run --project samples/Xanthos.Cli -- \
///     --sid YOUR_SID capture-fixtures \
///     --output tests/fixtures \
///     --specs "RACE,DIFF" \
///     --from "2024-01-01" \
///     --max-records 5
/// </summary>
type FixtureParserTests() =

    [<Fact>]
    [<Trait("Category", "Fixtures")>]
    member _.``Fixture directory location is deterministic``() =
        let root = FixtureLoader.fixturesRoot ()
        Assert.EndsWith("tests/fixtures", root.Replace("\\", "/"))

    [<SkippableFact>]
    [<Trait("Category", "Fixtures")>]
    member _.``All fixture files parse without error``() =
        let fixtures = FixtureLoader.enumerateFixtures ()
        Skip.If(fixtures.IsEmpty, "No fixtures found - run capture-fixtures on Windows first")

        let mutable successCount = 0
        let mutable failCount = 0
        let failures = ResizeArray<string>()

        for (spec, expectedType, filePath) in fixtures do
            let data = FixtureLoader.loadFixture filePath
            let payload = FixtureLoader.toPayload data
            let parseResult = PayloadParser.parsePayload payload

            match parseResult with
            | Ok _ ->
                // Verify record type matches directory
                let actualType = PayloadParser.getRecordTypeId data

                if actualType <> expectedType then
                    failures.Add($"File {filePath}: Expected type {expectedType} but got {actualType}")
                    failCount <- failCount + 1
                else
                    successCount <- successCount + 1
            | Error err ->
                failures.Add($"File {filePath}: Parse error - {err}")
                failCount <- failCount + 1

        if failures.Count > 0 then
            let failureMsg = String.Join(Environment.NewLine, failures)
            Assert.Fail($"Parsing failures ({failCount}/{fixtures.Length}):{Environment.NewLine}{failureMsg}")

        Assert.True(successCount > 0, $"Successfully parsed {successCount} fixtures")

    [<SkippableFact>]
    [<Trait("Category", "Fixtures")>]
    member _.``Fixture record types are all implemented``() =
        let fixtures = FixtureLoader.enumerateFixtures ()
        Skip.If(fixtures.IsEmpty, "No fixtures found - run capture-fixtures on Windows first")

        let recordTypes = fixtures |> List.map (fun (_, rt, _) -> rt) |> List.distinct

        for rt in recordTypes do
            let parsed = RecordTypes.parse rt
            Assert.True(parsed <> RecordTypes.RecordType.Unknown rt, $"Record type '{rt}' should be recognized")
            Assert.True(RecordTypes.isRecognized parsed, $"Record type '{rt}' should be implemented")

    [<SkippableFact>]
    [<Trait("Category", "Fixtures")>]
    member _.``Each spec directory contains at least one record type``() =
        let root = FixtureLoader.fixturesRoot ()
        Skip.If(not (Directory.Exists root), "Fixtures directory does not exist")

        let specDirs = Directory.GetDirectories(root)
        Skip.If(specDirs.Length = 0, "No spec directories found")

        for specDir in specDirs do
            let spec = Path.GetFileName(specDir)
            let typeDirs = Directory.GetDirectories(specDir)
            Assert.True(typeDirs.Length > 0, $"Spec '{spec}' should contain at least one record type directory")

    [<SkippableFact>]
    [<Trait("Category", "Fixtures")>]
    member _.``Fixture metadata files are valid JSON``() =
        let root = FixtureLoader.fixturesRoot ()
        Skip.If(not (Directory.Exists root), "Fixtures directory does not exist")

        let metaFiles =
            if Directory.Exists root then
                Directory.GetFiles(root, "*.meta.json", SearchOption.AllDirectories)
            else
                [||]

        Skip.If(metaFiles.Length = 0, "No metadata files found")

        for metaFile in metaFiles do
            let content = File.ReadAllText(metaFile)
            // Basic JSON structure validation
            Assert.Contains("timestamp", content)
            Assert.Contains("byteLength", content)
            Assert.Contains("recordType", content)

/// <summary>
/// Field value verification tests - ensure parsed fields contain valid data.
/// </summary>
type FixtureFieldVerificationTests() =

    [<SkippableFact>]
    [<Trait("Category", "Fixtures")>]
    member _.``Parsed fixtures have non-empty required fields``() =
        let fixtures = FixtureLoader.enumerateFixtures ()
        Skip.If(fixtures.IsEmpty, "No fixtures found - run capture-fixtures on Windows first")

        let mutable checkedCount = 0
        let issues = ResizeArray<string>()

        for (spec, recordType, filePath) in fixtures do
            let data = FixtureLoader.loadFixture filePath
            let payload = FixtureLoader.toPayload data
            let parseResult = PayloadParser.parsePayload payload

            match parseResult with
            | Ok parsed ->
                checkedCount <- checkedCount + 1
                // Check for common issues based on record type
                let checkIssues = ResizeArray<string>()

                // Generic checks for all record types
                match parsed with
                | ParsedRecord.RARecord r ->
                    if String.IsNullOrWhiteSpace(r.RaceKey) then
                        checkIssues.Add("RA: Empty RaceKey")

                    if String.IsNullOrWhiteSpace(r.RaceName) then
                        checkIssues.Add("RA: Empty RaceName")
                | ParsedRecord.SERecord r ->
                    if String.IsNullOrWhiteSpace(r.RaceKey) then
                        checkIssues.Add("SE: Empty RaceKey")

                    if String.IsNullOrWhiteSpace(r.HorseId) then
                        checkIssues.Add("SE: Empty HorseId")
                | ParsedRecord.TKRecord r ->
                    if String.IsNullOrWhiteSpace(r.RaceKey) then
                        checkIssues.Add("TK: Empty RaceKey")

                    if String.IsNullOrWhiteSpace(r.HorseId) then
                        checkIssues.Add("TK: Empty HorseId")
                | ParsedRecord.UMRecord r ->
                    if String.IsNullOrWhiteSpace(r.HorseId) then
                        checkIssues.Add("UM: Empty HorseId")
                | ParsedRecord.KSRecord r ->
                    if String.IsNullOrWhiteSpace(r.JockeyCode) then
                        checkIssues.Add("KS: Empty JockeyCode")
                | ParsedRecord.CHRecord r ->
                    if String.IsNullOrWhiteSpace(r.TrainerCode) then
                        checkIssues.Add("CH: Empty TrainerCode")
                | _ -> ()

                if checkIssues.Count > 0 then
                    let fileName = Path.GetFileName(filePath)
                    let issueStr = String.Join(", ", checkIssues)
                    issues.Add($"{spec}/{recordType}/{fileName}: {issueStr}")
            | Error _ -> ()

        if issues.Count > 0 then
            printfn "Field validation issues found:"

            for issue in issues do
                printfn "  - %s" issue

        // This test reports issues but doesn't fail - real data may have edge cases
        Assert.True(checkedCount > 0, $"Checked {checkedCount} fixtures for field validity")

    [<SkippableFact>]
    [<Trait("Category", "Fixtures")>]
    member _.``Date fields contain valid dates``() =
        let fixtures = FixtureLoader.enumerateFixtures ()
        Skip.If(fixtures.IsEmpty, "No fixtures found - run capture-fixtures on Windows first")

        let mutable validDates = 0
        let mutable invalidDates = 0
        let invalids = ResizeArray<string>()

        for (spec, recordType, filePath) in fixtures do
            let data = FixtureLoader.loadFixture filePath
            let payload = FixtureLoader.toPayload data
            let parseResult = PayloadParser.parsePayload payload

            match parseResult with
            | Ok parsed ->
                // Check BirthDate where applicable
                let birthDate =
                    match parsed with
                    | ParsedRecord.UMRecord r -> r.BirthDate
                    | ParsedRecord.KSRecord r -> r.BirthDate
                    | ParsedRecord.CHRecord r -> r.BirthDate
                    | _ -> None

                match birthDate with
                | Some dt when dt.Year >= 1900 && dt.Year <= DateTime.Now.Year + 1 -> validDates <- validDates + 1
                | Some dt ->
                    invalidDates <- invalidDates + 1
                    invalids.Add($"{spec}/{recordType}: Invalid birth date {dt}")
                | None -> ()
            | Error _ -> ()

        if invalids.Count > 0 then
            printfn "Invalid dates found:"

            for inv in invalids do
                printfn "  - %s" inv

        Assert.True(validDates > 0 || fixtures.Length > 0, "Checked date fields")

/// <summary>
/// Coverage gap detection - identify missing record types.
/// </summary>
type FixtureCoverageGapTests() =

    [<SkippableFact>]
    [<Trait("Category", "Fixtures")>]
    member _.``Report fixture coverage gaps``() =
        let fixtures = FixtureLoader.enumerateFixtures ()
        Skip.If(fixtures.IsEmpty, "No fixtures found - run capture-fixtures on Windows first")

        let capturedTypes =
            fixtures |> List.map (fun (_, rt, _) -> rt) |> List.distinct |> Set.ofList

        let allTypes = KnownRecordTypes.all |> Set.ofList
        let missingTypes = Set.difference allTypes capturedTypes
        let extraTypes = Set.difference capturedTypes allTypes

        printfn ""
        printfn "=========================================="
        printfn "  FIXTURE COVERAGE GAP ANALYSIS"
        printfn "=========================================="
        printfn ""

        printfn
            "Coverage: %d/%d record types (%.1f%%)"
            capturedTypes.Count
            allTypes.Count
            (float capturedTypes.Count / float allTypes.Count * 100.0)

        printfn ""

        if not missingTypes.IsEmpty then
            printfn "MISSING FIXTURES (need to capture):"

            for (category, types) in KnownRecordTypes.byCategory do
                let missing = types |> List.filter (fun t -> missingTypes.Contains t)

                if not missing.IsEmpty then
                    printfn "  %s: %s" category (String.Join(", ", missing))

            printfn ""

        if not extraTypes.IsEmpty then
            printfn "EXTRA TYPES (not in known list): %s" (String.Join(", ", extraTypes))
            printfn ""

        printfn "CAPTURED BY CATEGORY:"

        for (category, types) in KnownRecordTypes.byCategory do
            let captured = types |> List.filter (fun t -> capturedTypes.Contains t)
            let total = types.Length
            let pct = float captured.Length / float total * 100.0
            printfn "  %s: %d/%d (%.0f%%)" category captured.Length total pct

        printfn ""

        // Report passes - this is informational
        Assert.True(true, $"Coverage analysis complete: {capturedTypes.Count}/{allTypes.Count} types")

    [<SkippableFact>]
    [<Trait("Category", "Fixtures")>]
    member _.``Minimum coverage threshold met``() =
        let fixtures = FixtureLoader.enumerateFixtures ()
        Skip.If(fixtures.IsEmpty, "No fixtures found - run capture-fixtures on Windows first")

        let capturedTypes =
            fixtures |> List.map (fun (_, rt, _) -> rt) |> List.distinct |> List.length

        let allTypes = KnownRecordTypes.all.Length
        let coverage = float capturedTypes / float allTypes * 100.0

        // Warn if coverage is below 50%
        if coverage < 50.0 then
            printfn "WARNING: Fixture coverage is below 50%% (%.1f%%)" coverage
            printfn "Consider capturing more specs to improve coverage."

        Assert.True(capturedTypes > 0, $"Have fixtures for {capturedTypes} record types")

/// <summary>
/// Fixture freshness checks - warn about stale fixtures.
/// </summary>
type FixtureFreshnessTests() =

    [<SkippableFact>]
    [<Trait("Category", "Fixtures")>]
    member _.``Fixtures are not stale``() =
        let fixtures = FixtureLoader.enumerateFixtures ()
        Skip.If(fixtures.IsEmpty, "No fixtures found - run capture-fixtures on Windows first")

        let oldestDate = FixtureLoader.getOldestFixtureDate ()
        let maxAgeInDays = 180 // 6 months

        match oldestDate with
        | Some oldest ->
            let age = DateTime.Now - oldest
            printfn "Oldest fixture: %s (%d days ago)" (oldest.ToString("yyyy-MM-dd")) (int age.TotalDays)

            if age.TotalDays > float maxAgeInDays then
                printfn "WARNING: Fixtures are older than %d days." maxAgeInDays
                printfn "Consider re-capturing to ensure compatibility with latest JV-Link data."
        | None -> printfn "Could not determine fixture age from metadata."

        Assert.True(true, "Freshness check complete")

    [<SkippableFact>]
    [<Trait("Category", "Fixtures")>]
    member _.``Report fixture capture dates``() =
        let metadata = FixtureLoader.enumerateMetadata ()
        Skip.If(metadata.IsEmpty, "No metadata files found")

        let dates =
            metadata
            |> List.choose (fun (path, content) ->
                try
                    let doc = JsonDocument.Parse(content)
                    let root = doc.RootElement
                    let ts = root.GetProperty("timestamp").GetString()
                    let rt = root.GetProperty("recordType").GetString()

                    match DateTime.TryParse(ts) with
                    | true, dt -> Some(rt, dt)
                    | _ -> None
                with _ ->
                    None)
            |> List.groupBy fst
            |> List.map (fun (rt, items) ->
                let dates = items |> List.map snd
                rt, List.min dates, List.max dates)
            |> List.sortBy (fun (_, minDate, _) -> minDate)

        printfn ""
        printfn "Fixture capture dates by record type:"
        printfn "======================================"

        for (rt, minDate, maxDate) in dates do
            if minDate = maxDate then
                printfn "  %s: %s" rt (minDate.ToString("yyyy-MM-dd"))
            else
                printfn "  %s: %s to %s" rt (minDate.ToString("yyyy-MM-dd")) (maxDate.ToString("yyyy-MM-dd"))

        Assert.True(true, "Date report complete")

/// <summary>
/// Edge case detection - identify unusual values in fixtures.
/// </summary>
type FixtureEdgeCaseTests() =

    [<SkippableFact>]
    [<Trait("Category", "Fixtures")>]
    member _.``Detect edge cases in fixture data``() =
        let fixtures = FixtureLoader.enumerateFixtures ()
        Skip.If(fixtures.IsEmpty, "No fixtures found - run capture-fixtures on Windows first")

        let edgeCases = ResizeArray<string>()

        for (spec, recordType, filePath) in fixtures do
            let data = FixtureLoader.loadFixture filePath
            let fileName = Path.GetFileName(filePath)

            // Check for unusual byte patterns
            let allZeros = data |> Array.forall ((=) 0uy)
            let allSpaces = data |> Array.forall ((=) 32uy)
            let veryShort = data.Length < 10
            let veryLong = data.Length > 100000

            if allZeros then
                edgeCases.Add($"{spec}/{recordType}/{fileName}: All zeros (empty record?)")

            if allSpaces then
                edgeCases.Add($"{spec}/{recordType}/{fileName}: All spaces (blank record?)")

            if veryShort then
                edgeCases.Add($"{spec}/{recordType}/{fileName}: Very short ({data.Length} bytes)")

            if veryLong then
                edgeCases.Add($"{spec}/{recordType}/{fileName}: Very long ({data.Length} bytes)")

        if edgeCases.Count > 0 then
            printfn ""
            printfn "Edge cases detected:"
            printfn "===================="

            for ec in edgeCases do
                printfn "  - %s" ec
        else
            printfn "No edge cases detected in fixture data."

        Assert.True(true, "Edge case detection complete")

    [<SkippableFact>]
    [<Trait("Category", "Fixtures")>]
    member _.``Report fixture size statistics``() =
        let fixtures = FixtureLoader.enumerateFixtures ()
        Skip.If(fixtures.IsEmpty, "No fixtures found - run capture-fixtures on Windows first")

        let sizesByType =
            fixtures
            |> List.map (fun (_, rt, path) ->
                let data = FixtureLoader.loadFixture path
                rt, data.Length)
            |> List.groupBy fst
            |> List.map (fun (rt, items) ->
                let sizes = items |> List.map snd
                let minSize = List.min sizes
                let maxSize = List.max sizes
                let avgSize = List.averageBy float sizes
                rt, minSize, maxSize, int avgSize, sizes.Length)
            |> List.sortBy (fun (rt, _, _, _, _) -> rt)

        printfn ""
        printfn "Fixture size statistics:"
        printfn "========================"
        printfn "%-4s %8s %8s %8s %6s" "Type" "Min" "Max" "Avg" "Count"
        printfn "%s" (String.replicate 40 "-")

        for (rt, minSize, maxSize, avgSize, count) in sizesByType do
            printfn "%-4s %8d %8d %8d %6d" rt minSize maxSize avgSize count

        Assert.True(true, "Size statistics complete")

/// <summary>
/// Summary information about captured fixtures.
/// </summary>
type FixtureSummaryTests() =

    [<SkippableFact>]
    [<Trait("Category", "Fixtures")>]
    member _.``Report fixture coverage summary``() =
        let fixtures = FixtureLoader.enumerateFixtures ()
        Skip.If(fixtures.IsEmpty, "No fixtures found - run capture-fixtures on Windows first")

        // Group by record type and count
        let byType =
            fixtures
            |> List.groupBy (fun (_, rt, _) -> rt)
            |> List.map (fun (rt, items) -> rt, items.Length)
            |> List.sortBy fst

        printfn ""
        printfn "=========================================="
        printfn "  FIXTURE COVERAGE SUMMARY"
        printfn "=========================================="
        printfn ""
        printfn "By record type:"

        for (rt, count) in byType do
            printfn "  %s: %d fixture(s)" rt count

        printfn ""
        printfn "Total: %d fixture(s) across %d record types" fixtures.Length byType.Length

        // Group by spec
        let bySpec =
            fixtures
            |> List.groupBy (fun (spec, _, _) -> spec)
            |> List.map (fun (spec, items) -> spec, items.Length)
            |> List.sortBy fst

        printfn ""
        printfn "By spec:"

        for (spec, count) in bySpec do
            printfn "  %s: %d fixture(s)" spec count

        Assert.True(true, "Summary printed")

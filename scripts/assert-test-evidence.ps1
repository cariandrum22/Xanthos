param(
    [Parameter(Mandatory)][string]$ResultsDirectory,
    [Parameter(Mandatory)][string]$RunId,
    [Parameter(Mandatory)][string]$Profile,
    [Parameter(Mandatory)][string]$Project,
    [Parameter(Mandatory)][string]$ExpectedOs,
    [Parameter(Mandatory)][string]$ExpectedTfm,
    [string]$PlanPath = 'tests/test-plan.json'
)
$ErrorActionPreference = 'Stop'
$directory = (Resolve-Path -LiteralPath $ResultsDirectory).Path
$context = Get-Content -LiteralPath (Join-Path $directory 'invocation.json') -Raw | ConvertFrom-Json
foreach ($pair in @(@('runId', $RunId), @('profile', $Profile), @('project', $Project), @('os', $ExpectedOs), @('tfm', $ExpectedTfm))) {
    if ($context.($pair[0]) -cne $pair[1]) { throw "Evidence context mismatch: $($pair[0])" }
}
if ($context.exitCode -ne 0 -or $null -eq $context.exitCode) { throw 'Test process did not complete successfully.' }
$plan = Get-Content -LiteralPath $PlanPath -Raw | ConvertFrom-Json
$cases = @($plan.cases | Where-Object { $_.project -ceq $Project -and $Profile -cin $_.profiles -and $_.tfm -ceq $ExpectedTfm -and $ExpectedOs -cin $_.os })
if ($cases.Count -eq 0) { throw 'Profile has zero expected tests.' }
$byName = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
$ids = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($case in $cases) {
    if (-not $ids.Add($case.id)) { throw "Duplicate plan ID: $($case.id)" }
    if ($byName.ContainsKey($case.displayName)) { throw 'Ambiguous planned result identity.' }
    $byName.Add($case.displayName, $case)
}
$trxPath = Join-Path $directory 'results.trx'
if (-not $context.trxSha256 -or (Get-FileHash -LiteralPath $trxPath).Hash.ToLowerInvariant() -cne $context.trxSha256) { throw 'TRX differs from the completed invocation hash.' }
[xml]$trx = Get-Content -LiteralPath $trxPath -Raw
if ($trx.TestRun.ResultSummary.outcome -cne 'Completed') { throw 'TRX run is incomplete or aborted.' }
if ([DateTimeOffset]::Parse($trx.TestRun.Times.start) -lt [DateTimeOffset]::Parse($context.startedAt)) { throw 'TRX predates this invocation.' }
$definitions = @{}
foreach ($definition in $trx.TestRun.TestDefinitions.UnitTest) {
    $definitions[$definition.id] = "$($definition.TestMethod.className).$($definition.TestMethod.name)"
}
$seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$passed = 0; $skipped = 0
foreach ($result in @($trx.TestRun.Results.UnitTestResult | Where-Object { $null -ne $_ })) {
    if (-not $byName.ContainsKey($result.testName)) { throw "Unexpected or unmapped test: $($result.testName)" }
    $case = $byName[$result.testName]
    if (-not $seen.Add($case.id)) { throw "Duplicate result: $($case.id)" }
    if ($definitions[$result.testId] -cne $case.fqn) { throw "FQN mismatch: $($case.id)" }
    switch -CaseSensitive ($result.outcome) {
        'Passed' { $passed++ }
        'NotExecuted' {
            if ($Profile -cne 'OptionalFixtures' -or $case.id -cnotin $plan.optionalSkipAllowlist) { throw "Required test skipped: $($case.id)" }
            $skipped++
        }
        default { throw "Unsuccessful test: $($case.id) outcome=$($result.outcome)" }
    }
}
if (-not $seen.SetEquals($ids)) {
    $missing = @($ids | Where-Object { -not $seen.Contains($_) })
    throw "Missing required tests: $($missing -join ',')"
}
if ([int]$trx.TestRun.ResultSummary.Counters.total -ne $seen.Count -or [int]$trx.TestRun.ResultSummary.Counters.failed -ne 0) {
    throw 'TRX counters disagree with individual results.'
}
$manifest = [ordered]@{
    status = $(if ($Profile -eq 'OptionalFixtures' -and $skipped -gt 0) { 'not-run' } else { 'pass' })
    runId = $RunId; profile = $Profile; project = $Project; os = $ExpectedOs; tfm = $ExpectedTfm; commit = $context.commit
    expected = $cases.Count; passed = $passed; skipped = $skipped; caseIds = @($seen | Sort-Object)
    trxSha256 = (Get-FileHash -LiteralPath $trxPath).Hash.ToLowerInvariant()
    planSha256 = (Get-FileHash -LiteralPath $PlanPath).Hash.ToLowerInvariant()
}
$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $directory 'test-evidence.json') -Encoding utf8
Write-Output "$($manifest.status.ToUpperInvariant()): $Project $Profile passed=$passed skipped=$skipped"

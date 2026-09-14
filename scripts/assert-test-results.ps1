param(
    [Parameter(Mandatory)][string]$TrxPath,
    [Parameter(Mandatory)][ValidateRange(1, 2147483647)][int]$ExpectedCount
)
$ErrorActionPreference = 'Stop'
[xml]$trx = Get-Content -LiteralPath $TrxPath -Raw
$counters = $trx.TestRun.ResultSummary.Counters
if ($null -eq $counters) { throw 'TRX result counters are missing.' }
$results = @($trx.TestRun.Results.UnitTestResult)
if ([int]$counters.total -ne $ExpectedCount -or [int]$counters.passed -ne $ExpectedCount -or
    [int]$counters.failed -ne 0 -or [int]$counters.notExecuted -ne 0 -or
    $results.Count -ne $ExpectedCount -or @($results | Where-Object outcome -ne 'Passed').Count -ne 0 -or
    $trx.TestRun.ResultSummary.outcome -ne 'Completed') {
    throw "Required test gate failed: expected=$ExpectedCount total=$($counters.total) passed=$($counters.passed) failed=$($counters.failed) notExecuted=$($counters.notExecuted)."
}
Write-Output "PASS: $ExpectedCount required tests, no failures/skips/missing results."

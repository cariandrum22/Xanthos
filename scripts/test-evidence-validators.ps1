param([Parameter(Mandatory)][string]$RunId)
$ErrorActionPreference = 'Stop'
if ($RunId -notmatch '^[A-Za-z0-9_-]+$') { throw 'Unsafe run ID.' }
$root = Join-Path '.artifacts/test-quality' $RunId
if (Test-Path -LiteralPath $root) { throw 'Use a new result directory.' }
New-Item -ItemType Directory -Path $root -Force | Out-Null
$root = (Resolve-Path $root).Path
$planPath = Join-Path $root 'synthetic-plan.json'
$cases = @()
foreach ($name in @('UnitTests', 'PropertyTests', 'Cli.E2E', 'FunctionalScenarioTests', 'WindowsTests')) {
    $windows = $name -eq 'WindowsTests'
    $profiles = if ($windows) { @('WindowsManaged') } elseif ($name -eq 'Cli.E2E') { @('Fast') } else { @('Fast', 'Coverage') }
    $cases += @{ id = "synthetic-$name"; project = "tests/Xanthos.$name/Xanthos.$name.fsproj"; fqn = "Synthetic.$name.Case"; displayName = "Synthetic.$name.Case"; profiles = $profiles; os = $(if ($windows) { @('windows') } else { @('linux', 'macos', 'windows') }); tfm = $(if ($windows) { 'net10.0-windows' } else { 'net10.0' }) }
}
@{ cases = $cases; optionalSkipAllowlist = @() } | ConvertTo-Json -Depth 8 | Set-Content $planPath -Encoding utf8
function Write-SyntheticRun($directory, $case, $os, $profile) {
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    # Deliberately artificial evidence: only tests the gate, never SDK behavior.
    $trx = @"
<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010" name="synthetic-$os-$profile"><Times start="2026-09-13T00:00:01Z"/><TestDefinitions><UnitTest id="1"><TestMethod className="Synthetic.$($case.project.Split('/')[1].Substring(8))" name="Case"/></UnitTest></TestDefinitions><Results><UnitTestResult testId="1" testName="$($case.displayName)" outcome="Passed"/></Results><ResultSummary outcome="Completed"><Counters total="1" failed="0"/></ResultSummary></TestRun>
"@
    $trxPath = Join-Path $directory 'results.trx'
    $trx | Set-Content $trxPath -Encoding utf8
    $coveragePath = Join-Path $directory 'coverage.cobertura.xml'
    '<coverage><packages><package name="Xanthos"><classes><class filename="Synthetic.fs"><lines><line number="1" hits="1" branch="false"/></lines></class></classes></package></packages></coverage>' | Set-Content $coveragePath -Encoding utf8
    @{ runId = $RunId; project = $case.project; profile = $profile; os = $os; tfm = $case.tfm; commit = 'synthetic-commit'; startedAt = '2026-09-13T00:00:00Z'; exitCode = 0; filter = 'synthetic'; trxSha256 = (Get-FileHash $trxPath).Hash.ToLowerInvariant(); coverageSha256 = @((Get-FileHash $coveragePath).Hash.ToLowerInvariant()) } | ConvertTo-Json | Set-Content (Join-Path $directory 'invocation.json') -Encoding utf8
}
$source = Join-Path $root 'source'
Write-SyntheticRun $source $cases[0] windows Fast
$results = @()
foreach ($fault in @('valid', 'missing-case', 'zero', 'skip', 'fail', 'duplicate', 'wrong-run', 'wrong-os', 'wrong-tfm', 'aborted', 'stale', 'process-failed', 'unlisted-optional-skip', 'replaced-trx')) {
    $directory = Join-Path $root $fault
    Copy-Item -LiteralPath $source -Destination $directory -Recurse
    $contextPath = Join-Path $directory 'invocation.json'
    $trxPath = Join-Path $directory 'results.trx'
    $context = Get-Content $contextPath -Raw | ConvertFrom-Json
    [xml]$trx = Get-Content $trxPath -Raw
    switch ($fault) {
        'missing-case' { [void]$trx.TestRun.Results.RemoveChild($trx.TestRun.Results.UnitTestResult) }
        'zero' { [void]$trx.TestRun.Results.RemoveChild($trx.TestRun.Results.UnitTestResult); $trx.TestRun.ResultSummary.Counters.total = '0' }
        'skip' { $trx.TestRun.Results.UnitTestResult.outcome = 'NotExecuted' }
        'fail' { $trx.TestRun.Results.UnitTestResult.outcome = 'Failed' }
        'duplicate' { [void]$trx.TestRun.Results.AppendChild($trx.TestRun.Results.UnitTestResult.CloneNode($true)) }
        'wrong-run' { $context.runId = 'previous-run' }
        'wrong-os' { $context.os = 'linux' }
        'wrong-tfm' { $context.tfm = 'net9.0' }
        'aborted' { $trx.TestRun.ResultSummary.outcome = 'Aborted' }
        'stale' { $trx.TestRun.Times.start = '2026-09-12T00:00:01Z' }
        'process-failed' { $context.exitCode = 1 }
        'unlisted-optional-skip' { $trx.TestRun.Results.UnitTestResult.outcome = 'NotExecuted' }
        'replaced-trx' { $trx.TestRun.name = 'another-machine' }
    }
    $trx.Save($trxPath)
    if ($fault -ne 'replaced-trx') { $context.trxSha256 = (Get-FileHash $trxPath).Hash.ToLowerInvariant() }
    $context | ConvertTo-Json | Set-Content $contextPath -Encoding utf8
    $selectedPlan = $planPath
    $profile = 'Fast'
    if ($fault -eq 'unlisted-optional-skip') {
        $profile = 'OptionalFixtures'; $context.profile = $profile
        $context | ConvertTo-Json | Set-Content $contextPath -Encoding utf8
        $badPlan = Get-Content $planPath -Raw | ConvertFrom-Json
        $badPlan.cases[0].profiles = @($profile)
        $selectedPlan = Join-Path $directory 'plan.json'
        $badPlan | ConvertTo-Json -Depth 8 | Set-Content $selectedPlan -Encoding utf8
    }
    & (Join-Path $PSHOME 'pwsh') -NoProfile -File "$PSScriptRoot/assert-test-evidence.ps1" -ResultsDirectory $directory -RunId $RunId -Profile $profile -Project $cases[0].project -ExpectedOs windows -ExpectedTfm net10.0 -PlanPath $selectedPlan *> (Join-Path $directory 'validator.log')
    $code = $LASTEXITCODE
    if (($fault -eq 'valid' -and $code -ne 0) -or ($fault -ne 'valid' -and $code -eq 0)) { throw "Unexpected validator outcome: $fault ($code)" }
    $results += @{ fault = $fault; exitCode = $code; synthetic = $true }
}
$results | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $root 'gate-negative-cases.json') -Encoding utf8
Write-Output "PASS: $($results.Count) synthetic evidence controls."

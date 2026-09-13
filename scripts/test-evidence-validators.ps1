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
$artifacts = Join-Path $root 'artifacts'
foreach ($os in @('linux', 'macos', 'windows')) {
    foreach ($case in $cases | Where-Object { $os -in $_.os }) {
        foreach ($profile in $case.profiles) {
            $name = $case.project.Split('/')[1].Substring(8)
            Write-SyntheticRun (Join-Path $artifacts "quality-$os/$($case.tfm)/$profile/$name") $case $os $profile
        }
    }
}
$before = @(Get-ChildItem $artifacts -Recurse -File -Filter results.trx | ForEach-Object { @{ path = $_.FullName; hash = (Get-FileHash $_.FullName).Hash } })
& "$PSScriptRoot/assert-ci-artifacts.ps1" -ArtifactsDirectory $artifacts -RunId $RunId -Commit synthetic-commit -JobResult success -PlanPath $planPath *> (Join-Path $root 'artifacts-valid.log')
foreach ($file in $before) { if ((Get-FileHash $file.path).Hash -cne $file.hash) { throw 'Artifact hash changed during collection.' } }
$results += @{ fault = 'three-os-same-filenames'; exitCode = 0; preservedFiles = $before.Count; synthetic = $true }
foreach ($fault in @('missing-os', 'overwritten-trx', 'job-failure', 'job-cancelled', 'wrong-commit')) {
    $directory = Join-Path $root "artifacts-$fault"
    Copy-Item -LiteralPath $artifacts -Destination $directory -Recurse
    $job = 'success'; $commit = 'synthetic-commit'
    switch ($fault) {
        'missing-os' { Rename-Item -LiteralPath (Join-Path $directory 'quality-macos') -NewName 'missing-macos' }
        'overwritten-trx' { Copy-Item -LiteralPath (Join-Path $directory 'quality-linux/net10.0/Fast/UnitTests/results.trx') -Destination (Join-Path $directory 'quality-windows/net10.0/Fast/UnitTests/results.trx') }
        'job-failure' { $job = 'failure' }
        'job-cancelled' { $job = 'cancelled' }
        'wrong-commit' { $commit = 'another-commit' }
    }
    & (Join-Path $PSHOME 'pwsh') -NoProfile -File "$PSScriptRoot/assert-ci-artifacts.ps1" -ArtifactsDirectory $directory -RunId $RunId -Commit $commit -JobResult $job -PlanPath $planPath *> (Join-Path $directory 'validator.log')
    $code = $LASTEXITCODE
    if ($code -eq 0) { throw "Artifact fault accepted: $fault" }
    $results += @{ fault = $fault; exitCode = $code; synthetic = $true }
}
$results | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $root 'gate-negative-cases.json') -Encoding utf8
Write-Output "PASS: $($results.Count) synthetic evidence controls."

# Structural coverage failures must reach their intended rule, not only the hash guard.
$coverageSource = Join-Path $root 'coverage-source'
Write-SyntheticRun $coverageSource $cases[0] windows Coverage
& "$PSScriptRoot/test-coverage-validator.ps1" -SourceDirectory $coverageSource -OutputDirectory (Join-Path $root 'coverage-controls') -PlanPath $planPath

$environmentPath = Join-Path $root 'windows-environment.json'
foreach ($fault in @('valid', 'missing', 'sdk-present', 'dll-present', 'wrong-commit', 'wrong-architecture')) {
    $state = @{ runId = $RunId; commit = 'synthetic-commit'; os = 'windows'; architecture = 'X64'; pointerSize = 8; sdkAbsent = $true; sdkActivatedByPreflight = $false; status = 'pass'; registrations = @(); nativeFiles = @(); services = @() }
    switch ($fault) {
        'sdk-present' { $state.sdkAbsent = $false }
        'dll-present' { $state.nativeFiles = @('synthetic.dll') }
        'wrong-commit' { $state.commit = 'another-commit' }
        'wrong-architecture' { $state.architecture = 'X86' }
    }
    $state | ConvertTo-Json | Set-Content $environmentPath -Encoding utf8
    $selected = if ($fault -eq 'missing') { Join-Path $root 'absent.json' } else { $environmentPath }
    & (Join-Path $PSHOME 'pwsh') -NoProfile -File "$PSScriptRoot/assert-sdk-absence.ps1" -Path $selected -RunId $RunId -Commit synthetic-commit *> (Join-Path $root "sdk-$fault.log")
    if (($fault -eq 'valid') -ne ($LASTEXITCODE -eq 0)) { throw "Unexpected SDK absence validator outcome: $fault" }
}
Write-Output 'PASS: six SDK absence evidence controls.'

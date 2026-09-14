param([Parameter(Mandatory)][string]$SourceDirectory, [Parameter(Mandatory)][string]$OutputDirectory, [string]$PlanPath = 'tests/test-plan.json')
$ErrorActionPreference = 'Stop'
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Use a fresh fault-control directory.' }
$source = (Resolve-Path -LiteralPath $SourceDirectory).Path
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$output = (Resolve-Path -LiteralPath $OutputDirectory).Path
$report = Get-ChildItem $source -Recurse -Filter coverage.cobertura.xml | Select-Object -First 1
if (-not $report) { throw 'Source must contain a coverage report.' }
$original = Get-Content (Join-Path $source 'invocation.json') -Raw | ConvertFrom-Json
if ($original.profile -cne 'Coverage') { throw 'Fault controls require a completed Coverage invocation.' }
& "$PSScriptRoot/assert-coverage.ps1" -ResultsDirectory $source -RunId $original.runId -Project $original.project -PlanPath $PlanPath
$controls = [ordered]@{
    'missing' = 'Missing collector report.'
    'empty' = 'Empty or stale coverage report.'
    'broken-xml' = 'Malformed coverage XML.'
    'no-production-module' = 'Coverage must contain exactly one Xanthos production module.'
    'stale-run' = 'Coverage run/project/exit status mismatch.'
    'hash-tamper' = 'Coverage differs from the completed invocation hash.'
    'diagnostic-subset' = 'Diagnostic coverage is not full-suite acceptance evidence.'
    'missing-case' = 'Missing required tests:'
}
$evidence = @()
foreach ($fault in $controls.Keys) {
    $directory = Join-Path $output $fault
    New-Item -ItemType Directory -Path $directory | Out-Null
    $context = Get-Content (Join-Path $source 'invocation.json') -Raw | ConvertFrom-Json
    $context | Add-Member -NotePropertyName diagnosticFilter -NotePropertyValue $false -Force
    $trxPath = Join-Path $directory 'results.trx'
    Copy-Item -LiteralPath (Join-Path $source 'results.trx') -Destination $trxPath
    $path = Join-Path $directory 'coverage.cobertura.xml'
    if ($fault -ne 'missing') { Copy-Item -LiteralPath $report.FullName -Destination $path }
    switch ($fault) {
        'empty' { [IO.File]::WriteAllBytes($path, [byte[]]@()) }
        'broken-xml' { Set-Content $path '<coverage>' }
        'no-production-module' {
            [xml]$xml = Get-Content $path -Raw
            foreach ($package in $xml.coverage.packages.package) { $package.name = 'OnlyTestAssembly' }
            $xml.Save($path)
        }
        'hash-tamper' { Add-Content $path '<!-- tampered -->' }
        'diagnostic-subset' { $context.diagnosticFilter = $true }
        'missing-case' {
            [xml]$trx = Get-Content $trxPath -Raw
            $results = @($trx.TestRun.Results.UnitTestResult)
            if ($results.Count -lt 1) { throw 'Missing-case control requires source cases.' }
            $null = $trx.TestRun.Results.RemoveChild($results[0])
            $trx.Save($trxPath)
            $context.trxSha256 = (Get-FileHash $trxPath).Hash.ToLowerInvariant()
            $context.diagnosticFilter = $false
            $context.filter = 'FullyQualifiedName!=OptionalFixture'
        }
    }
    # Update structural controls' hashes so they reach the intended semantic rule.
    # The independent tamper control must retain the original hash.
    if ($fault -notin @('missing', 'hash-tamper')) {
        $context.coverageSha256 = @((Get-FileHash $path).Hash.ToLowerInvariant())
    }
    $context | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $directory 'invocation.json') -Encoding utf8
    $expectedRun = if ($fault -eq 'stale-run') { 'different-run' } else { $context.runId }
    $log = Join-Path $directory 'validator.log'
    & (Get-Process -Id $PID).Path -NoProfile -File "$PSScriptRoot/assert-coverage.ps1" -ResultsDirectory $directory -RunId $expectedRun -Project $context.project -PlanPath $PlanPath *> $log
    $code = $LASTEXITCODE
    if ($code -eq 0 -or -not (Select-String -LiteralPath $log -SimpleMatch $controls[$fault] -Quiet)) {
        throw "Fault control did not reject for expected reason: $fault; see $log"
    }
    $evidence += @{ fault = $fault; exitCode = $code; expectedReason = $controls[$fault]; log = "$fault/validator.log" }
}
$evidence | ConvertTo-Json | Set-Content (Join-Path $output 'results.json') -Encoding utf8
Write-Output "PASS: all $($controls.Count) invalid coverage controls rejected for their expected reasons."

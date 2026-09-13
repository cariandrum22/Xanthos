param([Parameter(Mandatory)][string]$SourceDirectory, [Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Use a fresh fault-control directory.' }
$source = (Resolve-Path -LiteralPath $SourceDirectory).Path
$context = Get-Content (Join-Path $source 'invocation.json') -Raw | ConvertFrom-Json
$report = Get-ChildItem $source -Recurse -Filter coverage.cobertura.xml | Select-Object -First 1
$evidence = @()
foreach ($fault in @('missing', 'empty', 'broken-xml', 'no-production-module', 'stale-run')) {
    $directory = Join-Path $OutputDirectory $fault
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $source 'results.trx') -Destination $directory
    $context | ConvertTo-Json | Set-Content (Join-Path $directory 'invocation.json') -Encoding utf8
    $path = Join-Path $directory 'coverage.cobertura.xml'
    switch ($fault) {
        'empty' { [IO.File]::WriteAllBytes($path, [byte[]]@()) }
        'broken-xml' { Set-Content $path '<coverage>' }
        'no-production-module' {
            [xml]$xml = Get-Content $report.FullName -Raw
            foreach ($package in $xml.coverage.packages.package) { $package.name = 'OnlyTestAssembly' }
            $xml.Save([IO.Path]::GetFullPath($path))
        }
        'stale-run' { Copy-Item -LiteralPath $report.FullName -Destination $path }
    }
    $expectedRun = if ($fault -eq 'stale-run') { 'different-run' } else { $context.runId }
    & (Get-Process -Id $PID).Path -NoProfile -File "$PSScriptRoot/assert-coverage.ps1" -ResultsDirectory $directory -RunId $expectedRun -Project $context.project *> (Join-Path $directory 'validator.log')
    $code = $LASTEXITCODE
    if ($code -eq 0) { throw "Fault control accepted: $fault" }
    $evidence += @{ fault = $fault; exitCode = $code; log = "$fault/validator.log" }
}
$evidence | ConvertTo-Json | Set-Content (Join-Path $OutputDirectory 'results.json') -Encoding utf8
Write-Output 'PASS: all five invalid coverage controls rejected.'

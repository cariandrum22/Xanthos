<#
.SYNOPSIS
Publishes the x64 CLI and verifies required COM scenarios without stub fallback.
.DESCRIPTION
Uses the existing x64 JV-Link key registration. Interactive/media/live-event
checks are separate from the required connection and property checks.
#>
param(
    [string]$OutputDirectory = '.artifacts/com-verification',
    [switch]$SkipPublish,
    [string]$CliPath,
    [Parameter(Mandatory)][ValidatePattern('^\d{14}$')][string]$FromTime
)
$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent $PSScriptRoot
$previousCliPath = $env:XANTHOS_E2E_CLI_PATH
$previousFromTime = $env:XANTHOS_COM_FROM_TIME
Push-Location $repository
try {
    if ([IntPtr]::Size -ne 8) { throw 'Run this script from x64 PowerShell.' }
    $directory = [IO.Path]::GetFullPath($OutputDirectory)
    [IO.Directory]::CreateDirectory($directory) | Out-Null
    if (-not $SkipPublish) {
        $publishDirectory = Join-Path $directory 'cli-x64'
        & dotnet publish samples/Xanthos.Cli -c Release -f net10.0-windows -r win-x64 --self-contained false -o $publishDirectory
        if ($LASTEXITCODE -ne 0) { throw 'x64 CLI publish failed.' }
        $CliPath = Join-Path $publishDirectory 'Xanthos.Cli.exe'
    }
    if (-not $CliPath -or -not [IO.Path]::IsPathFullyQualified($CliPath) -or -not (Test-Path -LiteralPath $CliPath)) {
        throw 'Supply an existing absolute -CliPath when using -SkipPublish.'
    }
    $env:XANTHOS_E2E_CLI_PATH = $CliPath
    $env:XANTHOS_COM_FROM_TIME = $FromTime
    & dotnet test tests/Xanthos.ComTests -c Release --logger 'trx;LogFileName=com-x64.trx' --results-directory $directory
    if ($LASTEXITCODE -ne 0) { throw 'Required x64 COM tests failed.' }
    & (Join-Path $PSScriptRoot 'assert-test-results.ps1') -TrxPath (Join-Path $directory 'com-x64.trx') -ExpectedCount 15
} finally {
    $env:XANTHOS_E2E_CLI_PATH = $previousCliPath
    $env:XANTHOS_COM_FROM_TIME = $previousFromTime
    Pop-Location
}

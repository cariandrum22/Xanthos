<#
.SYNOPSIS
    Runs COM verification workflow against real JV-Link.

.DESCRIPTION
    Builds the project, runs tests with XANTHOS_E2E_MODE=COM, and exercises
    the CLI commands against a live JV-Link COM instance. Requires JV-Link
    to be installed and a valid service key.

.PARAMETER Sid
    JRA-VAN service ID (default: UNKNOWN).

.PARAMETER ServiceKey
    JRA-VAN service key. Required. Can also be set via XANTHOS_E2E_SERVICE_KEY.

.PARAMETER SavePath
    Path where JV-Link saves downloaded files. Uses registry default if not specified.

.PARAMETER Dataspec
    Data specification to test (default: RACE).

.PARAMETER FromTime
    Start time for data retrieval in YYYYMMDDHHmmss format (default: 20240101000000).

.PARAMETER RealtimeKey
    Key for realtime data test. If not provided, realtime test is skipped.

.PARAMETER WatchDurationSeconds
    Duration in seconds for watch-events test (default: 60).

.PARAMETER SkipBuild
    Skip the build step.

.PARAMETER SkipTests
    Skip running dotnet test.

.PARAMETER SkipCli
    Skip CLI command verification.

.EXAMPLE
    .\run-com-verification.ps1 -ServiceKey "YOUR_KEY"
    # Runs full verification with default settings

.EXAMPLE
    .\run-com-verification.ps1 -ServiceKey "YOUR_KEY" -SkipBuild -SkipTests
    # Only runs CLI verification
#>
[CmdletBinding()]
param(
    [string]$Sid = "UNKNOWN",
    [Parameter(Mandatory = $false)]
    [string]$ServiceKey = $env:XANTHOS_E2E_SERVICE_KEY,
    [string]$SavePath = $env:XANTHOS_E2E_SAVE_PATH,
    [string]$Dataspec = "RACE",
    [string]$FromTime = "20240101000000",
    [string]$RealtimeKey,
    [int]$WatchDurationSeconds = 60,
    [switch]$SkipBuild,
    [switch]$SkipTests,
    [switch]$SkipCli
)

$ErrorActionPreference = "Stop"

if (-not $ServiceKey) {
    throw "Service key is required. Specify -ServiceKey or set XANTHOS_E2E_SERVICE_KEY."
}

if (-not $SavePath) {
    Write-Warning "No save path supplied; JV-Link will use its current registry value."
}

function Invoke-Dotnet {
    param(
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [string]$Step = "dotnet"
    )

    Write-Host "==> $Step" -ForegroundColor Cyan
    dotnet @Arguments

    if ($LASTEXITCODE -ne 0) {
        throw "Command '$Step' failed with exit code $LASTEXITCODE."
    }
}

function Invoke-Cli {
    param(
        [Parameter(Mandatory = $true)][string]$Command,
        [string[]]$ExtraArgs
    )

    $globalArgs = @("--sid", $Sid, "--service-key", $ServiceKey)

    if ($SavePath) {
        $globalArgs += @("--save-path", $SavePath)
    }

    $args = @("--project", "samples/Xanthos.Cli", "--")
    $args += $globalArgs
    $args += $Command

    if ($ExtraArgs) {
        $args += $ExtraArgs
    }

    Invoke-Dotnet -Arguments $args -Step "xanthos-cli $Command"
}

Write-Host "Configuring environment for COM verification..." -ForegroundColor Cyan
$env:XANTHOS_E2E_MODE = "COM"
$env:XANTHOS_E2E_SID = $Sid
$env:XANTHOS_E2E_SERVICE_KEY = $ServiceKey

if ($SavePath) {
    $env:XANTHOS_E2E_SAVE_PATH = $SavePath
}

$env:XANTHOS_E2E_SPEC = $Dataspec
$env:XANTHOS_E2E_FROM = $FromTime

if (-not $SkipBuild) {
    Invoke-Dotnet -Arguments @("build", "src/Xanthos/Xanthos.fsproj") -Step "dotnet build"
}

if (-not $SkipTests) {
    Invoke-Dotnet -Arguments @("test") -Step "dotnet test (COM)"
}

if (-not $SkipCli) {
    Invoke-Cli -Command "version"
    Invoke-Cli -Command "status"
    Invoke-Cli -Command "download" -ExtraArgs @("--spec", $Dataspec, "--from", $FromTime)

    if ($WatchDurationSeconds -gt 0) {
        Invoke-Cli -Command "watch-events" -ExtraArgs @("--duration", $WatchDurationSeconds.ToString())
    }

    if ($RealtimeKey) {
        Invoke-Cli -Command "realtime" -ExtraArgs @("--spec", $Dataspec, "--key", $RealtimeKey)
    }
    else {
        Write-Warning "Realtime key not provided; skipping realtime command."
    }
}

Write-Host "COM verification workflow completed successfully." -ForegroundColor Green

<#
.SYNOPSIS
    Captures JV-Link test fixtures for offline parser testing.

.DESCRIPTION
    Builds the Xanthos CLI in Release mode and captures real JV-Link data
    as test fixtures. Requires JV-Link COM to be installed on Windows.

    All parameters have sensible defaults - just run .\capture-fixtures.ps1 to start.

.PARAMETER Sid
    JRA-VAN service ID (default: UNKNOWN, or set XANTHOS_JVLINK_SID env var).

.PARAMETER Specs
    Comma-separated list of data specs to capture (default: "RACE,DIFF").

.PARAMETER From
    Start date for data retrieval in YYYYMMDD format (default: 30 days ago).

.PARAMETER To
    End date for data retrieval in YYYYMMDD format (optional).
    Use this with -From to capture a specific date range.

.PARAMETER MaxRecords
    Maximum number of records per record type (default: 10).

.PARAMETER OutputDir
    Output directory for fixtures (default: tests/fixtures).

.PARAMETER SkipBuild
    Skip the build step if the exe already exists.

.PARAMETER UseJvGets
    Use JVGets instead of JVRead for testing alternative implementation.

.EXAMPLE
    .\capture-fixtures.ps1
    # Uses all defaults: SID=UNKNOWN, specs=RACE,DIFF, from=30 days ago

.EXAMPLE
    .\capture-fixtures.ps1 -Sid "YOUR_SID" -Specs "RACE,BLOD" -MaxRecords 20

.EXAMPLE
    .\capture-fixtures.ps1 -From "20230101" -SkipBuild

.EXAMPLE
    .\capture-fixtures.ps1 -From "20240101" -To "20240114" -Specs "RACE,BLOD,SLOP"
#>

param(
    [string]$Sid = "",

    [string]$Specs = "RACE,DIFF",

    [string]$From = (Get-Date).AddDays(-30).ToString("yyyyMMdd"),

    [string]$To = "",

    [int]$MaxRecords = 10,

    [string]$OutputDir = "",

    [switch]$SkipBuild,

    [switch]$UseJvGets
)

$ErrorActionPreference = "Stop"

# Resolve paths relative to repository root
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Split-Path -Parent $ScriptDir
$CliProject = Join-Path $RepoRoot "samples\Xanthos.Cli"
$ExePath = Join-Path $CliProject "bin\Release\net10.0-windows\Xanthos.Cli.exe"

if ([string]::IsNullOrEmpty($OutputDir)) {
    $OutputDir = Join-Path $RepoRoot "tests\fixtures"
}

Write-Host "=== Xanthos Fixture Capture ===" -ForegroundColor Cyan
Write-Host "Repository: $RepoRoot"
Write-Host "Output: $OutputDir"
if (-not [string]::IsNullOrEmpty($Sid)) {
    Write-Host "SID: (provided)"
} else {
    Write-Host "SID: UNKNOWN (default)"
}
Write-Host "Specs: $Specs"
Write-Host "From: $From"
if (-not [string]::IsNullOrEmpty($To)) {
    Write-Host "To: $To"
}
Write-Host "Max Records: $MaxRecords"
Write-Host ""

# Check JV-Link COM registration
$jvLinkKey = "Registry::HKEY_CLASSES_ROOT\JVDTLab.JVLink"
if (-not (Test-Path $jvLinkKey)) {
    Write-Host "ERROR: JV-Link COM not registered. Install JV-Link first." -ForegroundColor Red
    exit 1
}
Write-Host "JV-Link COM: Detected" -ForegroundColor Green

# Build if needed
if (-not $SkipBuild) {
    Write-Host ""
    Write-Host "Building CLI (Release, net10.0-windows, x86)..." -ForegroundColor Yellow
    Push-Location $RepoRoot
    try {
        dotnet build $CliProject -c Release -f net10.0-windows --verbosity quiet
        if ($LASTEXITCODE -ne 0) {
            Write-Host "ERROR: Build failed." -ForegroundColor Red
            exit 1
        }
        Write-Host "Build: Success" -ForegroundColor Green
    }
    finally {
        Pop-Location
    }
}

# Verify exe exists
if (-not (Test-Path $ExePath)) {
    Write-Host "ERROR: CLI exe not found at $ExePath" -ForegroundColor Red
    Write-Host "Run without -SkipBuild to build first." -ForegroundColor Yellow
    exit 1
}

# Create output directory
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
    Write-Host "Created output directory: $OutputDir"
}

# Run capture-fixtures
Write-Host ""
Write-Host "Capturing fixtures..." -ForegroundColor Yellow

# Build command arguments
$cliArgs = @()

# Only add --sid if explicitly provided
if (-not [string]::IsNullOrEmpty($Sid)) {
    $cliArgs += @("--sid", $Sid)
}

$cliArgs += @("capture-fixtures", "--output", $OutputDir, "--specs", $Specs, "--from", $From)
if (-not [string]::IsNullOrEmpty($To)) {
    $cliArgs += @("--to", $To)
}
$cliArgs += @("--max-records", $MaxRecords)
if ($UseJvGets) {
    $cliArgs += @("--use-jvgets")
}

$displayArgs = if (-not [string]::IsNullOrEmpty($Sid)) { $cliArgs -replace $Sid, "***" } else { $cliArgs }
Write-Host "Command: $ExePath $($displayArgs -join ' ')"
Write-Host ""

& $ExePath @cliArgs

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "=== Fixture Capture Complete ===" -ForegroundColor Green

    # Show summary
    $binFiles = Get-ChildItem -Path $OutputDir -Filter "*.bin" -Recurse -ErrorAction SilentlyContinue
    if ($binFiles) {
        Write-Host "Captured $($binFiles.Count) fixture file(s):" -ForegroundColor Cyan
        $binFiles | Group-Object { Split-Path (Split-Path $_.FullName -Parent) -Leaf } | ForEach-Object {
            Write-Host "  $($_.Name): $($_.Count) file(s)"
        }
    }
}
else {
    Write-Host ""
    Write-Host "=== Fixture Capture Failed ===" -ForegroundColor Red
    exit $LASTEXITCODE
}

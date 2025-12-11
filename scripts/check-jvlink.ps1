<#
.SYNOPSIS
    Checks if JV-Link COM is registered on the system.

.DESCRIPTION
    Verifies that the specified JV-Link ProgID is registered in the Windows
    registry and can be resolved to a COM type.

.PARAMETER ProgId
    The COM ProgID to check (default: JRAVANDataLab.JVLink).

.EXAMPLE
    .\check-jvlink.ps1
    # Checks the default JV-Link ProgID

.EXAMPLE
    .\check-jvlink.ps1 -ProgId "JVDTLab.JVLink"
    # Checks an alternative ProgID
#>
param(
    [string]$ProgId = "JRAVANDataLab.JVLink"
)

$ErrorActionPreference = "Stop"

Write-Host "=== JV-Link COM Check ===" -ForegroundColor Cyan
Write-Host "ProgID: $ProgId"
Write-Host ""

try {
    Write-Host "Checking COM registration..." -ForegroundColor Yellow
    $type = [type]::GetTypeFromProgID($ProgId, $false)
    if ($null -eq $type) {
        Write-Host "ERROR: ProgID '$ProgId' is not registered." -ForegroundColor Red
        exit 1
    }

    Write-Host "JV-Link COM: Detected" -ForegroundColor Green
    Write-Host "Type: $($type.FullName)"
    Write-Host ""
    Write-Host "=== Check Complete ===" -ForegroundColor Green
    exit 0
}
catch {
    Write-Host "ERROR: Failed to resolve ProgID '$ProgId'" -ForegroundColor Red
    Write-Host "Details: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

param(
    [Parameter(Mandatory)][string]$CollectorStatePath,
    [Parameter(Mandatory)][string]$EvidenceDirectory
)
$ErrorActionPreference = 'Stop'
if (Test-Path -LiteralPath $EvidenceDirectory) { throw 'Use a fresh settings evidence directory.' }
New-Item -ItemType Directory -Path $EvidenceDirectory -Force | Out-Null
$state = Get-Content -LiteralPath $CollectorStatePath -Raw | ConvertFrom-Json
$collector = Get-Process -Id $state.pid -ErrorAction SilentlyContinue
if ($state.status -eq 'running' -or $null -ne $collector) {
    @{ status = 'blocked'; reason = 'Notification collector still owns a COM session; settings were not read or changed.'; settingsChanged = $false } |
        ConvertTo-Json | Set-Content (Join-Path $EvidenceDirectory 'result.json') -Encoding utf8
    Write-Output 'BLOCKED: collector active; real settings DoD remains not-run.'
    exit 3
}
if ($state.disconnected -ne $true -or $state.subscriptionStopped -ne $true) { throw 'Collector cleanup is not confirmed; do not mutate SDK settings.' }
if (-not $IsWindows -or [IntPtr]::Size -ne 8) { throw 'Windows x64 PowerShell is required.' }
if ((Get-Process -Id $PID).SessionId -eq 0) { throw 'Run real SDK setting changes from the signed-in desktop; consent must remain visible to the user.' }
$mutex = [Threading.Mutex]::new($false, 'Local\Xanthos-SDK-SettingsVerification')
$owned = $false
try {
    $owned = $mutex.WaitOne(0)
    if (-not $owned) { throw 'Another settings verification owns the SDK.' }
    $env:XANTHOS_SETTINGS_EXCLUSIVE = 'verified'
    & dotnet fsi "$PSScriptRoot/test-settings-roundtrip.fsx" *> (Join-Path $EvidenceDirectory 'test.log')
    $code = $LASTEXITCODE
    @{ status = $(if ($code -eq 0) { 'pass' } else { 'fail' }); exitCode = $code; settingsValuesLogged = $false } |
        ConvertTo-Json | Set-Content (Join-Path $EvidenceDirectory 'result.json') -Encoding utf8
    if ($code -ne 0) { throw 'Real SDK settings verification failed. Inspect restoration evidence before any further SDK mutation.' }
}
finally {
    Remove-Item Env:XANTHOS_SETTINGS_EXCLUSIVE -ErrorAction SilentlyContinue
    if ($owned) { $mutex.ReleaseMutex() }
    $mutex.Dispose()
}

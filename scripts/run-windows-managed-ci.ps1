param(
    [Parameter(Mandatory)][string]$RunId
)
$ErrorActionPreference = 'Stop'
if (-not $IsWindows -or [IntPtr]::Size -ne 8) { throw 'Windows x64 PowerShell is required.' }
if ($RunId -notmatch '^[A-Za-z0-9_-]+$') { throw 'RunId must be a safe path component.' }
$root = (Resolve-Path "$PSScriptRoot/..").Path
Push-Location $root
try {
    $directory = ".artifacts/test-quality/$RunId/windows/Q08"
    if (Test-Path -LiteralPath $directory) { throw 'Use a fresh Q08 evidence directory.' }
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    $registrations = @()
    foreach ($view in @([Microsoft.Win32.RegistryView]::Registry32, [Microsoft.Win32.RegistryView]::Registry64)) {
        $base = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::ClassesRoot, $view)
        try {
            foreach ($path in @('JVDTLab.JVLink', 'CLSID\{2AB1774D-0C41-11D7-916F-0003479BEB3F}')) {
                $key = $base.OpenSubKey($path)
                if ($null -ne $key) { $registrations += "$view/$path"; $key.Dispose() }
            }
        }
        finally { $base.Dispose() }
    }
    $nativeFiles = @('System32/JVDTLAB/JVDTLab.dll','SysWOW64/JVDTLAB/JVDTLab.dll') |
        Where-Object { Test-Path -LiteralPath (Join-Path $env:WINDIR $_) }
    $services = @(Get-Service -Name JVLinkAgent,JVLink64Agent -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Name)
    $sdkAbsent = $registrations.Count -eq 0 -and @($nativeFiles).Count -eq 0 -and $services.Count -eq 0
    $state = [ordered]@{
        runId = $RunId
        commit = (git rev-parse HEAD)
        recordedAt = [DateTimeOffset]::UtcNow.ToString('o')
        architecture = [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString()
        pointerSize = [IntPtr]::Size
        sdkAbsent = $sdkAbsent
        registrations = $registrations
        nativeFiles = @($nativeFiles)
        services = $services
        sdkActivatedByPreflight = $false
        githubRunId = $env:GITHUB_RUN_ID
        githubRunAttempt = $env:GITHUB_RUN_ATTEMPT
        runnerOs = $env:RUNNER_OS
        status = 'preflight'
    }
    $state | ConvertTo-Json -Depth 5 | Set-Content "$directory/windows-environment.json" -Encoding utf8
    if (-not $sdkAbsent) { throw 'Q08 requires a runner without JV-Link. SDK-present local tests cannot replace this CI evidence.' }
    try {
        & "$PSScriptRoot/run-test-profile.ps1" -Profile WindowsManaged -RunId $RunId
        $state.status = 'pass'
    }
    catch { $state.status = 'fail'; throw }
    finally { $state | ConvertTo-Json -Depth 5 | Set-Content "$directory/windows-environment.json" -Encoding utf8 }
}
finally { Pop-Location }

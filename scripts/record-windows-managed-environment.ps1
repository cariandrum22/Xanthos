param([Parameter(Mandatory)][string]$Directory, [Parameter(Mandatory)][string]$RunId, [Parameter(Mandatory)][string]$Commit)
$ErrorActionPreference = 'Stop'
if (-not $IsWindows -or [IntPtr]::Size -ne 8) { throw 'Windows x64 PowerShell is required.' }
$directory = (Resolve-Path -LiteralPath $Directory).Path
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
    commit = $Commit
    recordedAt = [DateTimeOffset]::UtcNow.ToString('o')
    os = 'windows'
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
    status = $(if ($sdkAbsent) { 'pass' } else { 'fail' })
}
$state | ConvertTo-Json -Depth 5 | Set-Content "$directory/windows-environment.json" -Encoding utf8

& "$PSScriptRoot/assert-sdk-absence.ps1" -Path "$directory/windows-environment.json" -RunId $RunId -Commit $Commit

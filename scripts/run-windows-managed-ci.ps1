param([Parameter(Mandatory)][string]$RunId)
$ErrorActionPreference = 'Stop'
Push-Location (Resolve-Path "$PSScriptRoot/..").Path
try {
    & "$PSScriptRoot/run-test-profile.ps1" -Profile WindowsManaged -RunId $RunId -RequireSdkAbsent
}
finally { Pop-Location }

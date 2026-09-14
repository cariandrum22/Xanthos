param(
    [Parameter(Mandatory)][string]$Path,
    [Parameter(Mandatory)][string]$RunId,
    [Parameter(Mandatory)][string]$Commit
)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw 'Missing SDK absence evidence.' }
$state = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
if ($state.runId -cne $RunId -or $state.commit -cne $Commit) { throw 'SDK absence evidence context mismatch.' }
if ($state.architecture -cne 'X64' -or $state.pointerSize -ne 8 -or $state.os -cne 'windows') { throw 'Windows x64 SDK absence evidence is required.' }
if ($state.sdkAbsent -cne $true -or $state.sdkActivatedByPreflight -cne $false -or $state.status -cne 'pass') { throw 'JV-Link absence was not established.' }
foreach ($field in @('registrations', 'nativeFiles', 'services')) {
    if ($null -eq $state.$field -or @($state.$field).Count -ne 0) { throw "JV-Link absence was not established: $field" }
}

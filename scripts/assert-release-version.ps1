param(
    [Parameter(Mandatory)][string]$RequestedVersion
)
$ErrorActionPreference = 'Stop'
if ($RequestedVersion -cnotmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$') {
    throw 'A stable release version must be X.Y.Z without a v prefix or leading zeroes.'
}
$project = Join-Path $PSScriptRoot '../src/Xanthos/Xanthos.fsproj'
$output = & dotnet msbuild $project -getProperty:Version
if ($LASTEXITCODE -ne 0) { throw 'Could not read the source version from MSBuild.' }
$sourceVersion = ($output | Out-String).Trim()
if ($sourceVersion -cne $RequestedVersion) {
    throw "Release version '$RequestedVersion' does not match source version '$sourceVersion'."
}
Write-Output $sourceVersion

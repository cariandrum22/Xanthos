param(
    [Parameter(Mandatory)][string]$ArtifactsDirectory,
    [Parameter(Mandatory)][string]$RunId,
    [Parameter(Mandatory)][string]$Commit,
    [Parameter(Mandatory)][string]$JobResult,
    [string]$PlanPath = 'tests/test-plan.json'
)
$ErrorActionPreference = 'Stop'
if ($JobResult -cne 'success') { throw "Required matrix did not succeed: $JobResult" }
$manifest = @()
$fastIds = @{}
foreach ($os in @('linux', 'macos', 'windows')) {
    $root = Join-Path $ArtifactsDirectory "quality-$os"
    if (-not (Test-Path -LiteralPath $root -PathType Container)) { throw "Missing artifact: quality-$os" }
    $profiles = [ordered]@{
        Fast = @('UnitTests', 'PropertyTests', 'Cli.E2E', 'FunctionalScenarioTests')
        Coverage = @('UnitTests', 'PropertyTests', 'FunctionalScenarioTests')
    }
    if ($os -eq 'windows') { $profiles.WindowsManaged = @('WindowsTests') }
    $fastIds[$os] = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($profile in $profiles.Keys) {
        foreach ($name in $profiles[$profile]) {
            $tfm = if ($name -eq 'WindowsTests') { 'net10.0-windows' } else { 'net10.0' }
            $directory = Join-Path $root "$tfm/$profile/$name"
            $context = Get-Content -LiteralPath (Join-Path $directory 'invocation.json') -Raw | ConvertFrom-Json
            if ($context.commit -cne $Commit) { throw "Wrong commit: $os/$profile/$name" }
            $project = "tests/Xanthos.$name/Xanthos.$name.fsproj"
            & "$PSScriptRoot/assert-test-evidence.ps1" -ResultsDirectory $directory -RunId $RunId -Profile $profile -Project $project -ExpectedOs $os -ExpectedTfm $tfm -PlanPath $PlanPath
            if ($profile -eq 'Coverage') {
                & "$PSScriptRoot/assert-coverage.ps1" -ResultsDirectory $directory -RunId $RunId -Project $project -PlanPath $PlanPath
            }
            $evidence = Get-Content -LiteralPath (Join-Path $directory 'test-evidence.json') -Raw | ConvertFrom-Json
            if ($profile -eq 'Fast') { foreach ($id in $evidence.caseIds) { [void]$fastIds[$os].Add($id) } }
            foreach ($file in Get-ChildItem -LiteralPath $directory -Recurse -File | Where-Object { $_.Name -eq 'results.trx' -or $_.Name -eq 'coverage.cobertura.xml' }) {
                $manifest += @{ os = $os; profile = $profile; project = $project; path = [IO.Path]::GetRelativePath((Resolve-Path $ArtifactsDirectory).Path, $file.FullName); sha256 = (Get-FileHash -LiteralPath $file.FullName).Hash.ToLowerInvariant() }
            }
        }
    }
}
if (-not $fastIds.linux.SetEquals($fastIds.macos) -or -not $fastIds.linux.SetEquals($fastIds.windows)) { throw 'Fast case IDs differ across operating systems.' }
@{ status = 'pass'; runId = $RunId; commit = $Commit; fastCasesPerOs = $fastIds.linux.Count; files = $manifest } | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $ArtifactsDirectory 'artifact-manifest.json') -Encoding utf8
Write-Output "PASS: all three OS artifacts retained; Fast has $($fastIds.linux.Count) identical logical cases per OS."

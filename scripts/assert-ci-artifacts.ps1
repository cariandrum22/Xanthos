param(
    [Parameter(Mandatory)][string]$ArtifactsDirectory,
    [Parameter(Mandatory)][string]$RunId,
    [Parameter(Mandatory)][string]$Commit,
    [Parameter(Mandatory)][string]$JobResult,
    [ValidateRange(1, 2147483647)][int]$RunAttempt = 1,
    [string]$PlanPath = 'tests/test-plan.json'
)
$ErrorActionPreference = 'Stop'
if ($JobResult -cne 'success') { throw "Required matrix did not succeed: $JobResult" }
$manifest = @()
$fastIds = @{}
$attempts = @{}
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
            $attempt = 0
            if (-not [int]::TryParse([string]$context.githubRunAttempt, [ref]$attempt) -or $attempt -lt 1 -or $attempt -gt $RunAttempt) { throw "Invalid artifact attempt: $os/$profile/$name" }
            if ($attempts.ContainsKey($os) -and $attempts[$os] -ne $attempt) { throw "Mixed attempts within OS artifact: $os" }
            $attempts[$os] = $attempt
            if ($profile -eq 'WindowsManaged') {
                $environmentPath = Join-Path $directory 'windows-environment.json'
                & "$PSScriptRoot/assert-sdk-absence.ps1" -Path $environmentPath -RunId $RunId -Commit $Commit
                $environment = Get-Content $environmentPath -Raw | ConvertFrom-Json
                if ([string]$environment.githubRunAttempt -cne [string]$context.githubRunAttempt) { throw 'SDK absence evidence attempt mismatch.' }
            }
            $project = "tests/Xanthos.$name/Xanthos.$name.fsproj"
            & "$PSScriptRoot/assert-test-evidence.ps1" -ResultsDirectory $directory -RunId $RunId -Profile $profile -Project $project -ExpectedOs $os -ExpectedTfm $tfm -PlanPath $PlanPath
            if ($profile -eq 'Coverage') {
                & "$PSScriptRoot/assert-coverage.ps1" -ResultsDirectory $directory -RunId $RunId -Project $project -PlanPath $PlanPath
            }
            $evidence = Get-Content -LiteralPath (Join-Path $directory 'test-evidence.json') -Raw | ConvertFrom-Json
            if ($profile -eq 'Fast') { foreach ($id in $evidence.caseIds) { [void]$fastIds[$os].Add($id) } }
            foreach ($file in Get-ChildItem -LiteralPath $directory -Recurse -File | Where-Object { $_.Name -in @('results.trx', 'coverage.cobertura.xml', 'invocation.json', 'windows-environment.json') }) {
                $manifest += @{ os = $os; profile = $profile; project = $project; path = [IO.Path]::GetRelativePath((Resolve-Path $ArtifactsDirectory).Path, $file.FullName); sha256 = (Get-FileHash -LiteralPath $file.FullName).Hash.ToLowerInvariant() }
            }
        }
    }
}
if (-not $fastIds.linux.SetEquals($fastIds.macos) -or -not $fastIds.linux.SetEquals($fastIds.windows)) { throw 'Fast case IDs differ across operating systems.' }
@{ status = 'pass'; runId = $RunId; summaryAttempt = $RunAttempt; attemptsByOs = $attempts; commit = $Commit; fastCasesPerOs = $fastIds.linux.Count; files = $manifest } | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $ArtifactsDirectory 'artifact-manifest.json') -Encoding utf8
Write-Output "PASS: all three OS artifacts retained; Fast has $($fastIds.linux.Count) identical logical cases per OS."

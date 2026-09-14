param(
    [Parameter(Mandatory)][string]$ResultsDirectory,
    [Parameter(Mandatory)][string]$RunId,
    [Parameter(Mandatory)][string]$Project,
    [string]$PlanPath = 'tests/test-plan.json'
)
$ErrorActionPreference = 'Stop'
$directory = (Resolve-Path -LiteralPath $ResultsDirectory).Path
$context = Get-Content -LiteralPath (Join-Path $directory 'invocation.json') -Raw | ConvertFrom-Json
if ($context.runId -cne $RunId -or $context.project -cne $Project -or $context.exitCode -ne 0) {
    throw 'Coverage run/project/exit status mismatch.'
}
$started = [DateTimeOffset]::Parse($context.startedAt)
$reports = @(Get-ChildItem -LiteralPath $directory -Filter 'coverage.cobertura.xml' -Recurse -File)
if ($reports.Count -eq 0) { throw 'Missing collector report.' }
# VSTest can copy an attachment into TestRun/In as well as the collector GUID
# directory. Identical copies are one measurement; conflicting reports fail.
$hashes = @($reports | ForEach-Object { (Get-FileHash -LiteralPath $_.FullName).Hash } | Sort-Object -Unique)
if ($hashes.Count -ne 1) { throw 'Conflicting collector attachments in one run.' }
if ($context.profile -eq 'Coverage' -and ($context.coverageSha256.Count -ne 1 -or $hashes[0].ToLowerInvariant() -cne $context.coverageSha256[0])) { throw 'Coverage differs from the completed invocation hash.' }
$report = $reports | Sort-Object { $_.FullName.Length } | Select-Object -First 1
if ($report.Length -eq 0 -or $report.LastWriteTimeUtc -lt $started.UtcDateTime) { throw 'Empty or stale coverage report.' }
try { [xml]$xml = Get-Content -LiteralPath $report.FullName -Raw }
catch { throw 'Malformed coverage XML.' }
$package = @($xml.coverage.packages.package | Where-Object name -CEQ 'Xanthos')
if ($package.Count -ne 1) { throw 'Coverage must contain exactly one Xanthos production module.' }
$lines = @{}
foreach ($class in $package[0].classes.class) {
    $file = ([string]$class.filename).Replace('\', '/')
    foreach ($line in $class.lines.line) {
        $key = "${file}:$($line.number)"
        if (-not $lines.ContainsKey($key)) { $lines[$key] = 0L }
        $lines[$key] = [Math]::Max($lines[$key], [long]$line.hits)
    }
}
$covered = @($lines.Values | Where-Object { $_ -gt 0 }).Count
if ($lines.Count -eq 0 -or $covered -eq 0) { throw 'No executed Xanthos production lines.' }
# One authoritative TRX/plan contract. Revalidate rather than trust an old manifest.
if ($context.diagnosticFilter) { throw 'Diagnostic coverage is not full-suite acceptance evidence.' }
& "$PSScriptRoot/assert-test-evidence.ps1" -ResultsDirectory $directory -RunId $RunId -Profile $context.profile -Project $Project -ExpectedOs $context.os -ExpectedTfm $context.tfm -PlanPath $PlanPath
$evidence = Get-Content (Join-Path $directory 'test-evidence.json') -Raw | ConvertFrom-Json
$ids = @($evidence.caseIds)
# Module totals include branch identities from the collector. Do not sum method
# line entries (F# generated methods can share a source line).
$branchLines = @{}
foreach ($class in $package[0].classes.class) {
    foreach ($line in $class.lines.line) {
        if ($line.branch -eq 'true') {
            $key = "$($class.filename):$($line.number)"
            if ([string]$line.'condition-coverage' -notmatch '\((\d+)/(\d+)\)') { throw 'Malformed branch measurement.' }
            $value = @([int]$Matches[1], [int]$Matches[2])
            if ($branchLines.ContainsKey($key)) {
                if ($branchLines[$key][1] -ne $value[1]) { throw "Conflicting branch denominator: $key" }
                $value[0] = [Math]::Max($value[0], $branchLines[$key][0])
            }
            $branchLines[$key] = $value
        }
    }
}
$branchesCovered = 0; $branchesTotal = 0
foreach ($pair in $branchLines.Values) { $branchesCovered += $pair[0]; $branchesTotal += $pair[1] }
$manifest = [ordered]@{
    status = 'pass'; runId = $RunId; project = $Project; os = $context.os; tfm = $context.tfm
    commit = $context.commit; assembly = 'Xanthos'; caseIds = $ids
    report = [IO.Path]::GetRelativePath($directory, $report.FullName).Replace('\', '/')
    sha256 = (Get-FileHash -LiteralPath $report.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    lines = @{ covered = $covered; total = $lines.Count }
    branches = @{ covered = $branchesCovered; total = $branchesTotal; measured = ($branchesTotal -gt 0) }
    exclusions = @(); mergePolicy = 'Keep OS/TFM/project reports separate. Within a report count each file:line once. Do not sum percentages or claim branch union across reports.'
}
$manifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $directory 'coverage-manifest.json') -Encoding utf8
Write-Output "PASS coverage $Project lines=$covered/$($lines.Count) branches=$branchesCovered/$branchesTotal cases=$($ids.Count)"

param(
    [Parameter(Mandatory)][ValidateSet('Fast', 'Coverage', 'WindowsManaged', 'Com', 'Interactive', 'Live', 'Stress', 'OptionalFixtures')][string]$Profile,
    [string]$RunId = ([DateTime]::UtcNow.ToString('yyyyMMddTHHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)),
    [string[]]$Projects,
    [string]$Filter,
    [string]$FromTime,
    [string]$CollectorStatePath,
    [switch]$NoBuild,
    [switch]$RequireSdkAbsent
)
$ErrorActionPreference = 'Stop'
if ($RunId -notmatch '^[A-Za-z0-9_-]+$') { throw 'RunId must be a safe single path component.' }
if ($RequireSdkAbsent -and $Profile -ne 'WindowsManaged') { throw 'RequireSdkAbsent is only supported for WindowsManaged.' }
$platform = if ($IsWindows) { 'windows' } elseif ($IsMacOS) { 'macos' } else { 'linux' }
$baseDirectory = Join-Path '.artifacts/test-quality' "$RunId/$platform"
if ($Profile -eq 'Com') {
    if (-not $IsWindows -or $FromTime -notmatch '^\d{14}$') { throw 'Com requires Windows x64 and explicit -FromTime YYYYMMDDhhmmss.' }
    $directory = "$baseDirectory/net10.0-windows/Com/ComTests"
    if (Test-Path -LiteralPath $directory) { throw "Refusing to reuse results: $directory" }
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    $context = [ordered]@{ runId = $RunId; profile = $Profile; project = 'tests/Xanthos.ComTests/Xanthos.ComTests.fsproj'; os = $platform; tfm = 'net10.0-windows'; commit = (git rev-parse HEAD); startedAt = [DateTimeOffset]::UtcNow.ToString('o'); exitCode = $null }
    $context | ConvertTo-Json | Set-Content (Join-Path $directory 'invocation.json') -Encoding utf8
    try {
        & "$PSScriptRoot/run-com-verification.ps1" -OutputDirectory $directory -FromTime $FromTime
        $context.exitCode = 0
    }
    catch { $context.exitCode = 1; throw }
    finally {
        $trx = Join-Path $directory 'com-x64.trx'
        if (Test-Path -LiteralPath $trx) {
            Copy-Item -LiteralPath $trx -Destination (Join-Path $directory 'results.trx')
            $context.trxSha256 = (Get-FileHash -LiteralPath $trx).Hash.ToLowerInvariant()
        }
        $context | ConvertTo-Json | Set-Content (Join-Path $directory 'invocation.json') -Encoding utf8
    }
    & "$PSScriptRoot/assert-test-evidence.ps1" -ResultsDirectory $directory -RunId $RunId -Profile Com -Project $context.project -ExpectedOs windows -ExpectedTfm net10.0-windows
    return
}
if ($Profile -in @('Interactive', 'Live')) {
    if (-not $CollectorStatePath) { throw 'Supply the existing collector summary path; no collector is stopped or started implicitly.' }
    if ($Profile -eq 'Interactive') {
        & "$PSScriptRoot/test-settings-roundtrip.ps1" -CollectorStatePath $CollectorStatePath -EvidenceDirectory "$baseDirectory/net10.0-windows/Interactive"
    }
    else {
        $state = Get-Content -LiteralPath $CollectorStatePath -Raw | ConvertFrom-Json
        Write-Output "LIVE status=$($state.status) processed=$($state.processed) validated=$($state.validatedOrigins -join ',') missing=$($state.missingOrigins -join ',')"
        Write-Output 'Observation only: ongoing or missing notification kinds are not a passing result.'
    }
    return
}
if ($Profile -eq 'WindowsManaged' -and -not $IsWindows) { throw 'WindowsManaged must run on Windows; it cannot be skipped as a pass.' }
if (-not $Projects) {
    $Projects = switch ($Profile) {
        'Fast' { @('UnitTests', 'PropertyTests', 'Cli.E2E', 'FunctionalScenarioTests') }
        'Coverage' { @('UnitTests', 'PropertyTests', 'FunctionalScenarioTests') }
        'WindowsManaged' { @('WindowsTests') }
        'Stress' { @('UnitTests', 'PropertyTests', 'FunctionalScenarioTests') }
        'OptionalFixtures' { @('UnitTests') }
    }
}
$plan = Get-Content 'tests/test-plan.json' -Raw | ConvertFrom-Json
$previousReplay = $env:XANTHOS_PROPERTY_REPLAY
if ($Profile -eq 'Stress') {
    if (-not $env:XANTHOS_PROPERTY_REPLAY) {
        $env:XANTHOS_PROPERTY_REPLAY = "$(Get-Random -Minimum 1 -Maximum 2147483647),11400714819323198485"
    }
    if ($env:XANTHOS_PROPERTY_REPLAY -notmatch '^\d+,\d+$') { throw 'XANTHOS_PROPERTY_REPLAY must be seed,gamma.' }
    $parts = $env:XANTHOS_PROPERTY_REPLAY.Split(',')
    $seedValue = 0UL; $gammaValue = 0UL
    if (-not [uint64]::TryParse($parts[0], [ref]$seedValue) -or -not [uint64]::TryParse($parts[1], [ref]$gammaValue) -or $gammaValue % 2 -eq 0) {
        throw 'Property seed and gamma must be UInt64 values; gamma must be odd.'
    }
    Write-Output "Stress property replay: $env:XANTHOS_PROPERTY_REPLAY"
}
$previousProfile = $env:XANTHOS_TEST_PROFILE
$previousFailureDirectory = $env:XANTHOS_FAILURE_DIRECTORY
$env:XANTHOS_TEST_PROFILE = $Profile
try {
foreach ($name in $Projects) {
    if ($name -notin @('UnitTests', 'PropertyTests', 'Cli.E2E', 'FunctionalScenarioTests', 'WindowsTests')) { throw "Unapproved managed project: $name" }
    $project = "tests/Xanthos.$name/Xanthos.$name.fsproj"
    $tfm = if ($name -eq 'WindowsTests') { 'net10.0-windows' } else { 'net10.0' }
    $directory = Join-Path $baseDirectory "$tfm/$Profile/$name"
    if (Test-Path -LiteralPath $directory) { throw "Refusing to reuse a result directory: $directory" }
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    $env:XANTHOS_FAILURE_DIRECTORY = Join-Path (Resolve-Path -LiteralPath $directory).Path 'generated-failures'
    if ($RequireSdkAbsent) {
        & "$PSScriptRoot/record-windows-managed-environment.ps1" -Directory $directory -RunId $RunId -Commit (git rev-parse HEAD)
    }
    $selection = $Filter
    $optional = @($plan.cases | Where-Object { $_.project -ceq $project -and $_.requirement -ceq 'optional-fixture' })
    if (-not $selection) {
        if ($Profile -eq 'OptionalFixtures') { $selection = ($optional | ForEach-Object { "FullyQualifiedName=$($_.fqn)" }) -join '|' }
        elseif ($Profile -eq 'Stress') {
            $selection = if ($name -eq 'UnitTests') { 'FullyQualifiedName~Xanthos.UnitTests.PropertyTests|FullyQualifiedName~OfficialRecordProperties|FullyQualifiedName~RecordFieldBoundaryProperties|FullyQualifiedName~FieldCategoryProperties|FullyQualifiedName~FieldApplicabilityProperties|FullyQualifiedName~FormatAndDataSpecProperties' } elseif ($name -eq 'PropertyTests') { 'FullyQualifiedName~Xanthos.PropertyTests' } else { 'FullyQualifiedName~SessionModelTests' }
        }
        elseif ($optional.Count -gt 0) { $selection = ($optional | ForEach-Object { "FullyQualifiedName!=$($_.fqn)" }) -join '&' }
    }
    $context = [ordered]@{ runId = $RunId; profile = $Profile; project = $project; os = $platform; tfm = $tfm; commit = (git rev-parse HEAD); startedAt = [DateTimeOffset]::UtcNow.ToString('o'); filter = $selection; diagnosticFilter = [bool]$Filter; propertyReplay = $env:XANTHOS_PROPERTY_REPLAY; githubRunId = $env:GITHUB_RUN_ID; githubRunAttempt = $env:GITHUB_RUN_ATTEMPT; exitCode = $null }
    $arguments = @('test', $project, '-c', 'Release', '--logger', 'trx;LogFileName=results.trx', '--results-directory', $directory)
    if ($NoBuild) { $arguments += '--no-build' }
    if ($selection) { $arguments += @('--filter', $selection) }
    $timeout = if ($Profile -eq 'Stress') { '30m' } else { '10m' }
    $arguments += @('--blame-hang', '--blame-hang-timeout', $timeout, '--blame-hang-dump-type', 'mini')
    if ($Profile -eq 'Coverage') { $arguments += '--collect:XPlat Code Coverage' }
    $context | ConvertTo-Json | Set-Content (Join-Path $directory 'invocation.json') -Encoding utf8
    & dotnet @arguments *> (Join-Path $directory 'test.log')
    $context.exitCode = $LASTEXITCODE
    $trx = Join-Path $directory 'results.trx'
    $context.trxSha256 = if (Test-Path -LiteralPath $trx) { (Get-FileHash -LiteralPath $trx).Hash.ToLowerInvariant() } else { $null }
    $context.coverageSha256 = @(Get-ChildItem -LiteralPath $directory -Filter coverage.cobertura.xml -Recurse -File | ForEach-Object { (Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant() } | Sort-Object -Unique)
    $context | ConvertTo-Json | Set-Content (Join-Path $directory 'invocation.json') -Encoding utf8
    if ($context.exitCode -ne 0) { throw "Test failure: $project; see $directory/test.log" }
    if (-not $Filter) {
        & "$PSScriptRoot/assert-test-evidence.ps1" -ResultsDirectory $directory -RunId $RunId -Profile $Profile -Project $project -ExpectedOs $platform -ExpectedTfm $tfm
    }
    if ($Profile -eq 'Coverage') {
        & "$PSScriptRoot/assert-coverage.ps1" -ResultsDirectory $directory -RunId $RunId -Project $project
    }
}
}
finally {
    $env:XANTHOS_PROPERTY_REPLAY = $previousReplay
    $env:XANTHOS_TEST_PROFILE = $previousProfile
    $env:XANTHOS_FAILURE_DIRECTORY = $previousFailureDirectory
}

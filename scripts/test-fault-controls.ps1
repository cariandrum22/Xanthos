param(
    [Parameter(Mandatory)][string]$RunId,
    [ValidateSet('AlwaysError', 'IgnoredSetter', 'ConstantGetter', 'RecordOffset', 'NumericScale', 'RawOwnership', 'NotificationKey', 'RedundantClose', 'ReadReturnCode', 'MissingCategory', 'ReadErrorOutputs', 'ReadDataOwnership', 'FailedCloseOwnership')][string[]]$Faults = @('AlwaysError', 'IgnoredSetter', 'ConstantGetter')
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path "$PSScriptRoot/..").Path
if ($RunId -notmatch '^[A-Za-z0-9_-]+$') { throw 'Unsafe run ID.' }
$output = Join-Path $root ".artifacts/test-quality/$RunId"
if (Test-Path -LiteralPath $output) { throw 'Use a fresh fault-control run.' }
$files = @(git -C $root ls-files --cached --others --exclude-standard)
$snapshot = Join-Path $output 'source-snapshot'
New-Item -ItemType Directory -Path $snapshot -Force | Out-Null
$sourceHashes = @()
foreach ($relative in $files) {
    $source = Join-Path $root $relative
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { continue }
    $destination = Join-Path $snapshot $relative
    New-Item -ItemType Directory -Path (Split-Path $destination) -Force | Out-Null
    Copy-Item -LiteralPath $source -Destination $destination
    $sourceHashes += @{ path = $relative; sha256 = (Get-FileHash -LiteralPath $destination).Hash }
}
$sourceHashes | ConvertTo-Json | Set-Content (Join-Path $output 'source-hashes.json') -Encoding utf8
$results = @()
foreach ($fault in $Faults) {
    $copy = Join-Path $output "$fault/worktree"
    New-Item -ItemType Directory -Path $copy -Force | Out-Null
    foreach ($relative in $files) {
        $source = Join-Path $snapshot $relative
        if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { continue }
        $destination = Join-Path $copy $relative
        New-Item -ItemType Directory -Path (Split-Path $destination) -Force | Out-Null
        Copy-Item -LiteralPath $source -Destination $destination
    }
    $project = 'tests/Xanthos.FunctionalScenarioTests/Xanthos.FunctionalScenarioTests.fsproj'
    $filter = 'FullyQualifiedName~Q04 same session'
    $target = Join-Path $copy 'src/Xanthos/SdkOperations.fs'
    $code = Get-Content -LiteralPath $target -Raw
    switch ($fault) {
        'ReadErrorOutputs' {
            $filter = 'FullyQualifiedName~Q07 model traces'
            $needle = '|> Map.ofList })'
            $replacement = '|> fun _ -> Map.empty })'
        }
        'ReadDataOwnership' {
            $filter = 'FullyQualifiedName~Q07 model traces'
            $needle = 'bytes.[0 .. value - 1]'
            $replacement = 'bytes'
        }
        'FailedCloseOwnership' {
            $filter = 'FullyQualifiedName~Q07 model traces'
            $target = Join-Path $copy 'src/Xanthos/Interop/NativeSessionLifetime.fs'
            $code = Get-Content -LiteralPath $target -Raw
            $needle = '| "JVClose", 0 -> dataOpen <- false'
            $replacement = '| "JVClose", _ -> dataOpen <- false'
        }
        'ReadReturnCode' {
            $filter = 'FullyQualifiedName~Q07 model traces'
            $needle = 'ReturnCode = value }'
            $replacement = 'ReturnCode = 0 }'
        }
        'MissingCategory' {
            $project = 'tests/Xanthos.UnitTests/Xanthos.UnitTests.fsproj'
            $filter = 'FullyQualifiedName~FieldCategoryProperties'
            $target = Join-Path $copy 'tests/Xanthos.UnitTests/FieldCategoryProperties.fs'
            $code = Get-Content -LiteralPath $target -Raw
            $needle = '            hit category'
            $replacement = '            ignore category'
        }
        'NotificationKey' {
            $filter = 'FullyQualifiedName~S11'
            $target = Join-Path $copy 'src/Xanthos/EventKeys.fs'
            $code = Get-Content -LiteralPath $target -Raw
            $needle = 'Request = { Dataspec = dataspec; Key = key }'
            $replacement = 'Request = { Dataspec = dataspec; Key = key + "0" }'
        }
        'RedundantClose' {
            $filter = 'FullyQualifiedName~Q07 native cleanup'
            $target = Join-Path $copy 'src/Xanthos/Interop/NativeSessionLifetime.fs'
            $code = Get-Content -LiteralPath $target -Raw
            $needle = '        if dataOpen then'
            $replacement = '        if true then'
        }
        'RecordOffset' {
            $project = 'tests/Xanthos.UnitTests/Xanthos.UnitTests.fsproj'
            $filter = 'FullyQualifiedName~OfficialRecordProperties'
            $target = Join-Path $copy 'src/Xanthos/RecordBytes.fs'
            $code = Get-Content -LiteralPath $target -Raw
            $needle = 'date id "CreatedDate" 4 data'
            $replacement = 'date id "CreatedDate" 5 data'
        }
        'NumericScale' {
            $project = 'tests/Xanthos.UnitTests/Xanthos.UnitTests.fsproj'
            $filter = 'FullyQualifiedName~RecordFieldBoundaryProperties'
            $target = Join-Path $copy 'src/Xanthos/RecordBytes.fs'
            $code = Get-Content -LiteralPath $target -Raw
            $needle = 'Ok(Some(value / scale))'
            $replacement = 'Ok(Some(value / (scale * 10M)))'
        }
        'RawOwnership' {
            $project = 'tests/Xanthos.UnitTests/Xanthos.UnitTests.fsproj'
            $filter = 'FullyQualifiedName~OfficialRecordProperties'
            $target = Join-Path $copy 'src/Xanthos/Data/HorseWeight.fs'
            $code = Get-Content -LiteralPath $target -Raw
            $needle = 'Raw = Array.copy data'
            $replacement = 'Raw = data'
        }
        'AlwaysError' {
            $project = 'tests/Xanthos.PropertyTests/Xanthos.PropertyTests.fsproj'
            $filter = 'FullyQualifiedName~parseRaceCard'
            $target = Join-Path $copy 'src/Xanthos/Core/Serialization.fs'
            $code = Get-Content -LiteralPath $target -Raw
            $needle = '    let parseRaceCard (payload: byte[]) : Result<RaceInfo list, XanthosError> ='
            $replacement = "    let parseRaceCard (_payload: byte[]) : Result<RaceInfo list, XanthosError> = Error(ValidationError `"injected always-error`")`n`n    let private originalParseRaceCard (payload: byte[]) : Result<RaceInfo list, XanthosError> ="
        }
        'IgnoredSetter' {
            $needle = '    let setSaveFlag enabled session ='
            $replacement = "    let setSaveFlag (_enabled: bool) (_session: Session) : Result<unit, JvError> = Ok()`n`n    let private originalSetSaveFlag enabled session ="
        }
        'ConstantGetter' {
            $needle = '    let getSaveFlag session = property<int> "m_saveflag" session'
            $replacement = '    let getSaveFlag (_session: Session) : Result<int, JvError> = Ok 1'
        }
    }
    if (-not $code.Contains($needle)) { throw "Mutation anchor missing: $fault" }
    Set-Content -LiteralPath $target -Value $code.Replace($needle, $replacement) -Encoding utf8
    Push-Location $copy
    try {
        & dotnet build $project -c Release *> '../build.log'
        $buildExit = $LASTEXITCODE
        if ($buildExit -ne 0) { throw "Mutation did not build: $fault (not valid failure evidence)." }
        & dotnet test $project -c Release --no-build --filter $filter --logger 'trx;LogFileName=fault.trx' --results-directory ../results *> '../test.log'
        $testExit = $LASTEXITCODE
        [xml]$trx = Get-Content '../results/fault.trx' -Raw
        $failed = @($trx.TestRun.Results.UnitTestResult | Where-Object outcome -EQ Failed)
        if ($testExit -eq 0 -or $failed.Count -eq 0) { throw "Mutation survived: $fault" }
        $results += @{ fault = $fault; buildExitCode = $buildExit; testExitCode = $testExit; failed = @($failed | ForEach-Object testName); sourceSha256 = (Get-FileHash -LiteralPath $target).Hash }
    }
    finally { Pop-Location }
}
$results | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $output 'fault-results.json') -Encoding utf8
Write-Output "PASS: $($results.Count) independently built mutants failed their intended production assertions."

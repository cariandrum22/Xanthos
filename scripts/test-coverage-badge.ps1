$ErrorActionPreference = 'Stop'
$root = Join-Path ([IO.Path]::GetTempPath()) ('coverage-badge-test-' + [Guid]::NewGuid().ToString('N'))
$reports = Join-Path $root 'reports'
$output = Join-Path $root 'output'
[void] (New-Item -ItemType Directory -Path $reports)
function Write-Report([string] $Name, [string] $Lines, [string] $Assembly = 'Example') {
    "<coverage><packages><package name='$Assembly'><classes><class filename='src/File.fs'><lines>$Lines</lines></class></classes></package></packages></coverage>" | Set-Content (Join-Path $reports $Name)
}
function Generate {
    & "$PSScriptRoot/new-coverage-badge.ps1" -ReportsDirectory $reports -Assembly Example -SourcePrefix src -ExpectedReports 2 -Commit ('a' * 40) -RunUrl 'https://github.com/example/example/actions/runs/1' -OutputDirectory $output
}
function Reject([scriptblock] $Action) {
    $failed = $false
    try { & $Action | Out-Null } catch { $failed = $true }
    if (-not $failed) { throw 'Invalid coverage was accepted.' }
}
try {
    Reject { Generate }
    Write-Report 'one.cobertura.xml' "<line number='1' hits='1'/><line number='2' hits='0'/><line number='2' hits='0'/>"
    Write-Report 'two.cobertura.xml' "<line number='1' hits='0'/><line number='2' hits='1'/><line number='3' hits='0'/>"
    $second = Join-Path $reports 'two.cobertura.xml'
    (Get-Content $second -Raw).Replace('src/File.fs', 'File.fs') | Set-Content $second
    Copy-Item (Join-Path $reports 'one.cobertura.xml') (Join-Path $reports 'copy.cobertura.xml')
    Generate | Out-Null
    $summary = Get-Content (Join-Path $output 'coverage.json') -Raw | ConvertFrom-Json
    if ($summary.coveredLines -ne 2 -or $summary.totalLines -ne 3 -or $summary.percentage -ne '66.7') { throw 'Line union or rounding is incorrect.' }
    [xml] $svg = Get-Content (Join-Path $output 'coverage.svg') -Raw
    if ($svg.svg.'aria-label' -ne 'managed lines: 66.7% (2/3)') { throw 'Invalid SVG label.' }
    Write-Report 'two.cobertura.xml' "<line number='3' hits='1'/>" WrongAssembly
    Reject { Generate }
    Write-Report 'two.cobertura.xml' "<line number='3' hits='-1'/>"
    Reject { Generate }
    '<coverage>' | Set-Content (Join-Path $reports 'two.cobertura.xml')
    Reject { Generate }
    Write-Output 'Coverage badge controls passed: line union, duplicate classes/attachments, rounding, missing/malformed reports and invalid measurements.'
}
finally {
    $resolved = (Resolve-Path -LiteralPath $root).Path
    if ($resolved -ne [IO.Path]::GetFullPath($root) -or -not ([IO.Path]::GetFileName($resolved).StartsWith('coverage-badge-test-'))) { throw 'Unexpected cleanup path.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}

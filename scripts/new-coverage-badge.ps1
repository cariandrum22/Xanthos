param(
    [Parameter(Mandatory)][string] $ReportsDirectory,
    [Parameter(Mandatory)][string] $Assembly,
    [Parameter(Mandatory)][string] $SourcePrefix,
    [Parameter(Mandatory)][ValidateRange(1, 100)][int] $ExpectedReports,
    [Parameter(Mandatory)][string] $Commit,
    [Parameter(Mandatory)][string] $RunUrl,
    [Parameter(Mandatory)][string] $OutputDirectory
)

$ErrorActionPreference = 'Stop'
if ($Commit -cnotmatch '^[0-9a-f]{40}$') { throw 'A full source commit is required.' }
if ($RunUrl -notmatch '^https://github\.com/[^/]+/[^/]+/actions/runs/\d+$') { throw 'A GitHub Actions run URL is required.' }
$reports = @(Get-ChildItem -LiteralPath $ReportsDirectory -Recurse -File -Filter '*cobertura*.xml')
# VSTest may retain identical attachment copies. Count each measurement once.
$unique = @($reports | Group-Object { (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash } | ForEach-Object { $_.Group[0] })
if ($unique.Count -ne $ExpectedReports) { throw "Expected $ExpectedReports distinct reports; found $($unique.Count)." }
$lines = [Collections.Generic.Dictionary[string, bool]]::new([StringComparer]::Ordinal)
foreach ($report in $unique) {
    [xml] $xml = Get-Content -LiteralPath $report.FullName -Raw
    $packages = @($xml.coverage.packages.package | Where-Object name -CEQ $Assembly)
    if ($packages.Count -ne 1) { throw "Missing or ambiguous production assembly in $($report.Name)." }
    $reportLines = 0
    foreach ($class in $packages[0].classes.class) {
        $file = ([string] $class.filename).Replace('\', '/')
        # Collectors may use either the repository root or the assembly directory
        # as their source root. Strip only the configured, exact project prefix.
        $prefix = $SourcePrefix.Trim('/') + '/'
        if ($file.StartsWith($prefix, [StringComparison]::Ordinal)) { $file = $file.Substring($prefix.Length) }
        if (-not $file -or $file.StartsWith('/') -or $file.Contains(':') -or $file.Split('/') -contains '..') {
            throw 'Coverage source filenames must be relative and stable across reports.'
        }
        foreach ($line in $class.lines.line) {
            $number = 0L
            $hits = 0L
            if (-not [long]::TryParse([string] $line.number, [ref] $number) -or $number -le 0 -or
                -not [long]::TryParse([string] $line.hits, [ref] $hits) -or $hits -lt 0) { throw 'Invalid line measurement.' }
            $key = "${file}:$number"
            if (-not $lines.ContainsKey($key)) { $lines[$key] = $false }
            $lines[$key] = $lines[$key] -or ($hits -gt 0)
            $reportLines++
        }
    }
    if ($reportLines -eq 0) { throw 'Empty production coverage report.' }
}
$covered = @($lines.Values | Where-Object { $_ }).Count
if ($covered -eq 0) { throw 'No production lines were executed.' }
$percentage = (100.0 * $covered / $lines.Count).ToString('F1', [Globalization.CultureInfo]::InvariantCulture)
$color = if ($covered / $lines.Count -ge 0.8) { '#4c1' } elseif ($covered / $lines.Count -ge 0.6) { '#dfb317' } else { '#e05d44' }
$title = "managed lines: $percentage% ($covered/$($lines.Count))"
$description = [Security.SecurityElement]::Escape("$Assembly; source $Commit; $RunUrl; excludes native COM execution.")
[void] (New-Item -ItemType Directory -Force -Path $OutputDirectory)
$svg = @"
<svg xmlns="http://www.w3.org/2000/svg" width="170" height="20" role="img" aria-label="$title">
  <title>$title</title><desc>$description</desc>
  <linearGradient id="s" x2="0" y2="100%"><stop offset="0" stop-color="#bbb" stop-opacity=".1"/><stop offset="1" stop-opacity=".1"/></linearGradient>
  <clipPath id="r"><rect width="170" height="20" rx="3"/></clipPath>
  <g clip-path="url(#r)"><path fill="#555" d="M0 0h110v20H0z"/><path fill="$color" d="M110 0h60v20h-60z"/><path fill="url(#s)" d="M0 0h170v20H0z"/></g>
  <g fill="#fff" text-anchor="middle" font-family="Verdana,Geneva,DejaVu Sans,sans-serif" font-size="11"><text x="55" y="14">managed lines</text><text x="140" y="14">$percentage%</text></g>
</svg>
"@
[IO.File]::WriteAllText((Join-Path $OutputDirectory 'coverage.svg'), $svg.Replace("`r`n", "`n") + "`n")
$summary = [ordered]@{
    assembly = $Assembly; commit = $Commit; runUrl = $RunUrl; reports = $unique.Count
    coveredLines = $covered; totalLines = $lines.Count; percentage = $percentage
    scope = 'Portable managed line union; native COM execution excluded.'
    mergePolicy = 'Count each relative source file and line once; covered if any report has positive hits. No averaging or branch-union claim.'
}
$summary | ConvertTo-Json | Set-Content (Join-Path $OutputDirectory 'coverage.json') -Encoding utf8
Write-Output $title

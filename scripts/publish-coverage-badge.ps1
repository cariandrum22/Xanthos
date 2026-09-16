param(
    [Parameter(Mandatory)][ValidateSet('main', 'develop')][string] $Branch,
    [Parameter(Mandatory)][string] $SourceCommit
)

$ErrorActionPreference = 'Stop'
if ($env:GITHUB_EVENT_NAME -ne 'push' -or $env:GITHUB_REF -ne "refs/heads/$Branch") { throw 'Badge publication requires a trusted branch push.' }
$head = git rev-parse HEAD
if ($LASTEXITCODE -ne 0 -or $head -cne $SourceCommit) { throw 'Checkout does not match the measured source commit.' }
$summary = Get-Content .github/badges/coverage.json -Raw | ConvertFrom-Json
if ($summary.commit -cne $SourceCommit) { throw 'Badge does not describe this source commit.' }
git fetch origin $Branch --no-tags
if ($LASTEXITCODE -ne 0) { throw 'Could not check the branch tip.' }
$remoteHead = git rev-parse FETCH_HEAD
if ($remoteHead -cne $SourceCommit) { Write-Output 'A newer commit exists; leave its badge update to its CI run.'; exit 0 }
git add -- .github/badges/coverage.svg .github/badges/coverage.json
if ($LASTEXITCODE -ne 0) { throw 'Could not stage generated badges.' }
git diff --cached --quiet
if ($LASTEXITCODE -eq 0) { Write-Output 'Badge is already current.'; exit 0 }
if ($LASTEXITCODE -ne 1) { throw 'Could not inspect generated badge changes.' }
$staged = @(git diff --cached --name-only)
if (@($staged | Where-Object { $_ -notin @('.github/badges/coverage.svg', '.github/badges/coverage.json') }).Count) { throw 'Only generated coverage files may be published.' }
git -c 'user.name=github-actions[bot]' -c 'user.email=41898282+github-actions[bot]@users.noreply.github.com' commit -m 'docs: refresh measured coverage badge'
if ($LASTEXITCODE -ne 0) { throw 'Could not commit generated badges.' }
# GITHUB_TOKEN commits do not trigger another push workflow. Never force/rebase
# a newer source commit just to publish an older measurement.
git push origin "HEAD:refs/heads/$Branch"
if ($LASTEXITCODE -ne 0) {
    git fetch origin $Branch --no-tags
    if ($LASTEXITCODE -ne 0) { throw 'Badge push failed and the branch could not be rechecked.' }
    if ((git rev-parse FETCH_HEAD) -ceq $SourceCommit) { throw 'Badge push failed; check repository write permissions.' }
    Write-Output 'Branch advanced during publication; newer source and badge retained.'
}

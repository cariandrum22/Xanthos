# Contributing to Xanthos

Thank you for your interest in contributing to Xanthos!
This document explains how to contribute to the project.

## Development Environment Setup

### Prerequisites

- .NET 10 SDK
- Git

### Using Nix (Recommended)

```bash
nix develop
```

### Manual Setup

```bash
# Clone the repository
git clone https://github.com/cariandrum22/Xanthos.git
cd Xanthos

# Restore .NET tools
dotnet tool restore

# Build
dotnet build

# Test
dotnet test
```

## Development Workflow

### Branch Strategy (GitFlow)

This project follows a GitFlow-like branching strategy.

```text
main (release branch)
  ↑ On merge: version increment + CHANGELOG finalization
develop (development branch)
  ↑ PR merge
feature/*, fix/* (topic branches)
```

| Branch | Purpose | Merge Target |
| ------ | ------- | ------------ |
| `main` | Stable releases. Published to NuGet | - |
| `develop` | Development integration. Next release candidate | `main` |
| `feature/*` | New feature development | `develop` |
| `fix/*` | Bug fixes | `develop` |

**Important**:

- Topic branches are created from `develop` and merged back to `develop`
- Merging to `main` is only done during releases
- Direct commits to `main` are prohibited

### Branch Naming

Use a consistent, descriptive branch name format:

- **With an Issue**: `<type>/<issue-number>-<slug>`
- **Without an Issue**: `<type>/<slug>`
  (or `<type>/no-issue-<slug>` if you want to make that explicit)

Rules:

- `type` should align with Conventional Commits types (e.g. `feat/`, `fix/`, `docs/`,
  `refactor/`, `test/`, `ci/`, `chore/`)
- `issue-number` is digits only (no `issue-` prefix)
- `slug` should be short, kebab-case, and descriptive

Examples:

- `fix/3-jvgets-default`
- `docs/6-gitflow-workflow`
- `chore/no-issue-ci-cleanup`

### Contribution Process

1. Create an Issue to discuss changes (for major changes)
2. Create a branch from `develop`
3. Implement your changes
4. Add/run tests
5. Create a Pull Request

### Commit Messages

This project follows the [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/)
specification.

```text
<type>[optional scope]: <description>

[optional body]

[optional footer(s)]
```

**Types:**

| Type | Description | SemVer |
| ---- | ----------- | ----- |
| `feat` | New feature | MINOR |
| `fix` | Bug fix | PATCH |
| `docs` | Documentation only | - |
| `style` | Formatting (no code change) | - |
| `refactor` | Code refactoring | - |
| `perf` | Performance improvement | - |
| `test` | Adding/fixing tests | - |
| `build` | Build system changes | - |
| `ci` | CI/CD changes | - |
| `chore` | Other maintenance | - |

**Breaking Changes:**

Append `!` after type/scope or add `BREAKING CHANGE:` in footer:

```text
feat!: remove deprecated API

BREAKING CHANGE: The old API has been removed.
```

**Examples:**

```text
feat: add realtime data streaming support
fix: correct SavePath property access
docs: update installation instructions
refactor(parser): simplify record parsing logic
feat(cli)!: change command argument format
```

## Coding Conventions

### Formatting

Use Fantomas to format code:

```bash
dotnet fantomas .
```

### Linting

Check code quality with FSharpLint:

```bash
dotnet fsharplint lint Xanthos.sln
```

CI requires this command to succeed without warnings. Fix diagnostics in the
source; do not accept them through a warning baseline or `continue-on-error`.
Recursive functions should use compiler-checked `[<TailCall>]` declarations at
module or class scope, or iterative traversal where recursion is not tail-safe.
`FS3569` is treated as a build error.

Four asynchronous stream methods retain a narrowly scoped
`SynchronousFunctionNames` exception: FSharpLint 0.27's
[type classifier](https://github.com/fsprojects/FSharpLint/blob/v0.27.0/src/FSharpLint.Core/Rules/NamingHelper.fs)
recognises `Task` and `Async`, but not `IAsyncEnumerable`.
Each exception is documented beside the declaration and should be removed when
the tool recognises asynchronous streams. Keep public API names stable; any
additional suppression requires a concrete false-positive explanation.

### Naming Conventions

- **Modules/Types**: PascalCase (`JvLinkService`, `ComError`)
- **Functions/Values**: camelCase (`fetchPayloads`, `parseRecord`)
- **Constants**: PascalCase (`DefaultTimeout`)
- **Private members**: camelCase (leading underscore optional)

### Error Handling

- Functional SDK operations: `Result<'T, JvError>`; inspect `JvErrorKind`, `Api`, the native `Code` and `Outputs` without discarding unknown SDK codes.
- Record parsing: `Result<'T, RecordParseError>` with the record, field and original byte position.
- Legacy COM/service operations retain `ComError` / `XanthosError`; runtime parsing failures use `XanthosError.RecordError`.
- Catch exceptions at boundaries and convert to Result

## Testing

### Test Categories

| Category | Description | Environment |
| -------- | ----------- | ----------- |
| Unit | Pure F# unit tests | CI (any OS) |
| Property | FsCheck property-based tests | CI (any OS) |
| Fixtures | Fixture-based parser tests | CI (if fixtures exist) |
| Functional scenarios | Production CLI/F# functions with controlled native calls | CI (any OS) |
| WindowsManaged | WINDOWS assembly, STA and ABI without JV-Link | CI (Windows x64) |
| E2E (Stub) | Legacy CLI smoke | CI (any OS) |
| E2E (COM) | CLI tests with real COM | Windows only |

### Running Tests

```bash
# Required managed tests (PowerShell 7)
pwsh scripts/run-test-profile.ps1 -Profile Fast -RunId local-fast-01

# Unit tests only
dotnet test tests/Xanthos.UnitTests

# E2E tests (Stub mode - default)
dotnet test tests/Xanthos.Cli.E2E

# E2E tests (COM mode - Windows only)
pwsh scripts/run-test-profile.ps1 -Profile Com -RunId local-com-01 -FromTime 20260905000000
```

### Writing Tests

- Use pure inputs or a controlled native boundary for functional tests; keep legacy Stub smoke explicitly classified
- Add corresponding tests for new features
- E2E tests verify CLI command behavior
- See [tests/README.md](tests/README.md) for detailed guidelines

### Manual COM Verification

Some functionality requires testing with real JV-Link COM on Windows.
Use `net10.0-windows`, an x64 process and JV-Link 5.0 x64 with its key already
registered. Run `scripts/run-com-verification.ps1 -FromTime YYYYMMDDHHmmss`
from the signed-in desktop, choosing an available RACE publication interval.
Keep captures and machine-specific evidence under ignored `.artifacts/`.
Separate Contract/explicit Stub tests from actual COM results; deferred image,
playback or live-notification checks remain unverified. See the
[COM test guide](tests/Xanthos.ComTests/README.md) and
[public functional contract](docs/functional-api.md).
See [tests/README.md - Manual COM Verification](tests/README.md#manual-com-verification)
for:

- Step-by-step verification procedures
- Pre-release checklist template
- Troubleshooting guide

### Test Coverage

```bash
pwsh scripts/run-test-profile.ps1 -Profile Coverage -RunId local-coverage-01
```

## Pull Requests

### Before Creating a PR

1. Ensure all tests pass
2. Ensure code is formatted
3. Update documentation as needed
4. Update CHANGELOG.md (if applicable)
5. For COM-related changes: Run manual COM verification on Windows

### Titles and Descriptions

Use a Conventional Commit title, such as `fix(records): decode CP932 aliases`.
Release PRs use `release: vX.Y.Z`. Supported types are the commit types above,
plus `revert` and `release`; breaking changes may include `!`.

The [PR template](.github/pull_request_template.md) requires these headings in order:

- `## Summary`: the concrete problem and resulting behavior.
- `## Changes`: implementation, compatibility and documentation changes.
- `## Validation`: commands and outcomes, CI links, and remaining gaps. Explain
  tests that were not run; keep actual COM results separate from managed/Stub tests.
- `## Related issues`: closing or related references, or `None` for standalone work.

Fill every section; template comments and placeholders do not count. Preserve
unchecked tasks until verified. Additional subsections are welcome. These rules
also apply to PRs created or edited through `gh` or the API.

### Issues

Choose a [bug, enhancement, question or release verification form](.github/ISSUE_TEMPLATE).
Use a descriptive title and complete `### Summary`, `### Details` and
`### Expected outcome`, in that order. Include reproduction/environment details
for bugs and acceptance criteria for proposed work. Do not publish service keys,
licensed records or private machine logs; use synthetic examples and public links.

When reorganizing historical descriptions, preserve technical content, links,
recorded outcomes and unfinished checks. A merged PR is not evidence that an
unrecorded test passed. Leave comments, reviews and discussion history intact.

### Format Checks

The `Contribution format` workflow validates PR titles and issue/PR descriptions
on creation, edits and reopening, and rechecks PRs when commits change. It reads
event JSON as data with a read-only token; it does not post comments or edit issues.
Issue events use a separate `Issue format` check so their results do not replace
the PR's `Contribution format` result. Superseded runs for the same item are canceled.
Issue forms require GUI input, while workflow failures report invalid CLI/API
submissions after creation. Templates and issue-event automation take effect when
merged into the default branch (`develop`). To enforce the PR check at merge time,
maintainers can require `Contribution format` in the branch ruleset.

Run the dependency-free checker locally with Python 3.10 or newer (optional for
library development; CI uses the Python supplied by its Ubuntu runner):

```bash
python3 -m unittest discover -s scripts -p test_contribution_format.py -v
python3 scripts/contribution_format.py --kind pull_request --title 'fix: describe the change' --body-file pr.md
python3 scripts/contribution_format.py --kind issue --title 'Describe the problem' --body-file issue.md
```

On Windows, use your installed Python command, such as `py -3`, in place of
`python3`. Exit codes are 0 for valid metadata, 1 for format violations, and 2 for
invalid command arguments or unreadable input.

### Merge Strategy

To keep the `develop` history readable and consistent:

- **Topic branches → `develop`**: prefer **Squash and merge** (one PR = one commit).
- **`develop` → `main` (releases)**: prefer a **merge commit** to preserve a clear
  release boundary.
- **Rebase and merge**: only use when each commit is intentionally curated and
  meaningful on its own.

## Architecture

The project follows a three-layer architecture:

1. **Core** (`Xanthos.Core`) - Domain models, error types, text/encoding
2. **Interop** (`Xanthos.Interop`) - COM interface implementations and test stubs
3. **Runtime** (`Xanthos.Runtime`) - High-level service orchestration

See [design/architecture/README.md](design/architecture/README.md) for details.

## Releases

### CHANGELOG Management

This project follows the [Keep a Changelog](https://keepachangelog.com/) format.

#### Recording Changes During Development

When merging a PR, add changes to the `[Unreleased]` section in `CHANGELOG.md`:

```markdown
## [Unreleased]

### Added
- Description of new feature

### Changed
- Description of changes

### Fixed
- Description of fix

### Breaking Changes
- Description of breaking change (interface changes, etc.)
```

Categories:

- **Added**: New features
- **Changed**: Changes to existing features
- **Deprecated**: Features that are deprecated
- **Removed**: Features that were removed
- **Fixed**: Bug fixes
- **Security**: Security fixes
- **Breaking Changes**: Breaking changes (API/interface changes)

#### Finalizing CHANGELOG on Release

When merging `develop` → `main`:

1. Change `[Unreleased]` to `[X.Y.Z] - YYYY-MM-DD`
2. Add a new empty `[Unreleased]` section
3. Update version comparison links

### Version Management

This project follows [Semantic Versioning](https://semver.org/):

- **MAJOR**: Breaking changes (interface changes, etc.)
- **MINOR**: Backward-compatible new features
- **PATCH**: Backward-compatible bug fixes

Versions are centrally managed by `VersionPrefix` in `Directory.Build.props`.
During the 0.x development series, increment MINOR for API additions or incompatible
changes and PATCH for compatible fixes. Document incompatible changes explicitly;
1.0.0 will establish the stable public API contract.

### Pre-Release Checklist

Before tagging a release, complete the following:

1. **CI Tests**: All GitHub Actions workflows pass
2. **Manual COM Verification**: Complete the
   [verification checklist](tests/README.md#verification-checklist) on Windows
3. **CHANGELOG**: Finalize `[Unreleased]` section with release version
4. **Version**: Update version in `Directory.Build.props`

Release tags (`vX.Y.Z`) and manual workflow version inputs must match the source
version. The release workflow packs both target frameworks from that version;
it does not override the version while publishing. Merge the reviewed release
preparation into `develop`, then merge `develop` into `main` before tagging.

### Release Verification Evidence

For each release, use the [release verification form](.github/ISSUE_TEMPLATE/release-verification.yml).
Identify the version and source commit, environment, required profile results,
actual COM results, deferred checks and accepted SDK limitations. Use the current
[COM test guide](tests/Xanthos.ComTests/README.md) for native verification.
Record publication/package checks when completed and link this evidence from
the release notes. Keep unperformed verification explicitly pending.

## Questions & Support

- Create an Issue to ask questions
- Discuss in Pull Requests

Thank you for contributing!

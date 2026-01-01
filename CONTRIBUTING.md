# Contributing to Xanthos

Thank you for your interest in contributing to Xanthos! This document explains how to contribute to the project.

## Development Environment Setup

### Prerequisites

- .NET 10 SDK (preview)
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

```
main (release branch)
  ↑ On merge: version increment + CHANGELOG finalization
develop (development branch)
  ↑ PR merge
feature/*, fix/* (topic branches)
```

| Branch | Purpose | Merge Target |
|--------|---------|--------------|
| `main` | Stable releases. Published to NuGet | - |
| `develop` | Development integration. Next release candidate | `main` |
| `feature/*` | New feature development | `develop` |
| `fix/*` | Bug fixes | `develop` |

**Important**:
- Topic branches are created from `develop` and merged back to `develop`
- Merging to `main` is only done during releases
- Direct commits to `main` are prohibited

### Contribution Process

1. Create an Issue to discuss changes (for major changes)
2. Create a branch from `develop`
3. Implement your changes
4. Add/run tests
5. Create a Pull Request

### Commit Messages

This project follows the [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/) specification.

```
<type>[optional scope]: <description>

[optional body]

[optional footer(s)]
```

**Types:**

| Type | Description | SemVer |
|------|-------------|--------|
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

```
feat!: remove deprecated API

BREAKING CHANGE: The old API has been removed.
```

**Examples:**

```
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
dotnet fsharplint lint src tests
```

### Naming Conventions

- **Modules/Types**: PascalCase (`JvLinkService`, `ComError`)
- **Functions/Values**: camelCase (`fetchPayloads`, `parseRecord`)
- **Constants**: PascalCase (`DefaultTimeout`)
- **Private members**: camelCase (leading underscore optional)

### Error Handling

- COM errors: `Result<'T, ComError>`
- Business logic errors: `Result<'T, XanthosError>`
- Catch exceptions at boundaries and convert to Result

## Testing

### Test Categories

| Category | Description | Environment |
|----------|-------------|-------------|
| Unit | Pure F# unit tests | CI (any OS) |
| Property | FsCheck property-based tests | CI (any OS) |
| Fixtures | Fixture-based parser tests | CI (if fixtures exist) |
| E2E (Stub) | CLI tests with mock COM | CI (any OS) |
| E2E (COM) | CLI tests with real COM | Windows only |

### Running Tests

```bash
# All tests (CI-compatible)
dotnet test

# Unit tests only
dotnet test tests/Xanthos.UnitTests

# E2E tests (Stub mode - default)
dotnet test tests/Xanthos.Cli.E2E

# E2E tests (COM mode - Windows only)
XANTHOS_E2E_MODE=COM XANTHOS_SID=YOUR_SID dotnet test tests/Xanthos.Cli.E2E
```

### Writing Tests

- Use `JvLinkStub` for unit tests
- Add corresponding tests for new features
- E2E tests verify CLI command behavior
- See [tests/README.md](tests/README.md) for detailed guidelines

### Manual COM Verification

Some functionality requires testing with real JV-Link COM on Windows.
See [tests/README.md - Manual COM Verification](tests/README.md#manual-com-verification) for:

- Step-by-step verification procedures
- Pre-release checklist template
- Troubleshooting guide

### Test Coverage

```bash
dotnet test --collect:"XPlat Code Coverage"
```

## Pull Requests

### Before Creating a PR

1. Ensure all tests pass
2. Ensure code is formatted
3. Update documentation as needed
4. Update CHANGELOG.md (if applicable)
5. For COM-related changes: Run manual COM verification on Windows

### PR Template

```markdown
## Summary

Brief description of changes

## Type of Change

- [ ] Bug fix
- [ ] New feature
- [ ] Breaking change
- [ ] Documentation

## Testing

- [ ] Added/updated unit tests
- [ ] Added/updated E2E tests
- [ ] Existing tests pass

## Checklist

- [ ] Code is formatted
- [ ] Documentation updated
- [ ] CHANGELOG.md updated
```

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

Versions are centrally managed in `Directory.Build.props`.

### Pre-Release Checklist

Before tagging a release, complete the following:

1. **CI Tests**: All GitHub Actions workflows pass
2. **Manual COM Verification**: Complete the [verification checklist](tests/README.md#verification-checklist) on Windows
3. **CHANGELOG**: Finalize `[Unreleased]` section with release version
4. **Version**: Update version in `Directory.Build.props`

### Release Verification Evidence

For each release, create a GitHub Issue or Gist with:

```markdown
# Release Verification - v{VERSION}

## Environment
- Windows: {version}
- JV-Link: {version}
- .NET: {version}

## CI Status
- [ ] All workflows passing

## Manual COM Verification
- [ ] CLI E2E (COM mode): {pass/fail}
- [ ] Fixture capture: {file count} files
- [ ] JVGets mode: {pass/fail}

## Notes
{Any issues or observations}

Verified by: {name}
Date: {date}
```

Link the verification evidence in the release notes.

## Questions & Support

- Create an Issue to ask questions
- Discuss in Pull Requests

Thank you for contributing!

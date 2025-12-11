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

### Branch Strategy

- `main` - Stable releases
- `develop` - Development branch
- `feature/*` - New features
- `fix/*` - Bug fixes

### Contribution Process

1. Create an Issue to discuss changes (for major changes)
2. Create a branch from `develop`
3. Implement your changes
4. Add/run tests
5. Create a Pull Request

### Commit Messages

Use clear and concise commit messages:

```
<type>: <summary>

<body (optional)>
```

Types:
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation
- `test`: Tests
- `refactor`: Refactoring
- `ci`: CI/CD

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

### Pre-Release Checklist

Before tagging a release, complete the following:

1. **CI Tests**: All GitHub Actions workflows pass
2. **Manual COM Verification**: Complete the [verification checklist](tests/README.md#verification-checklist) on Windows
3. **CHANGELOG**: Update with release notes
4. **Version**: Update version numbers as needed

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

# Contributing to AureTTY

Thank you for your interest in contributing to AureTTY! This document provides guidelines and instructions for contributing.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Getting Started](#getting-started)
- [Development Setup](#development-setup)
- [Making Changes](#making-changes)
- [Testing](#testing)
- [Submitting Changes](#submitting-changes)
- [Code Style](#code-style)
- [Documentation](#documentation)
- [Release Process](#release-process)

## Code of Conduct

This project follows a simple code of conduct:

- Be respectful and inclusive
- Focus on constructive feedback
- Help others learn and grow
- Assume good intentions

## Getting Started

### Prerequisites

- .NET 10 SDK
- Git
- Linux: `util-linux` package
- Windows: Windows 10 1809+ or Windows Server 2019+

### Fork and Clone

1. Fork the repository on GitHub
2. Clone your fork:
   ```bash
   git clone https://github.com/YOUR_USERNAME/AureTTY.git
   cd AureTTY
   ```
3. Add upstream remote:
   ```bash
   git remote add upstream https://github.com/vitkuz573/AureTTY.git
   ```

## Development Setup

### Build the Project

```bash
# Restore dependencies
dotnet restore AureTTY.slnx

# Build
dotnet build AureTTY.slnx -c Debug

# Run tests
dotnet test tests/AureTTY.Tests/AureTTY.Tests.csproj
dotnet test tests/AureTTY.Core.Tests/AureTTY.Core.Tests.csproj
```

### Run the Service

```bash
# Linux
dotnet run --project src/AureTTY/AureTTY.csproj -f net10.0 -- \
  --transport http \
  --api-key dev-key \
  --allow-api-key-query

# Windows
dotnet run --project src/AureTTY/AureTTY.csproj -f net10.0-windows -- `
  --transport http `
  --api-key dev-key `
  --allow-api-key-query
```

### IDE Setup

**Visual Studio Code:**
- Install C# Dev Kit extension
- Open folder in VS Code
- Use `.vscode/launch.json` for debugging

**Visual Studio:**
- Open `AureTTY.slnx`
- Set `AureTTY` as startup project
- Configure command line arguments in project properties

**JetBrains Rider:**
- Open `AureTTY.slnx`
- Configure run configuration with arguments

## Making Changes

### Branch Naming

Use descriptive branch names:

- `feature/add-session-recording` - New features
- `fix/websocket-reconnection` - Bug fixes
- `docs/api-reference` - Documentation
- `refactor/session-management` - Code refactoring
- `test/integration-tests` - Test improvements

### Commit Messages

Follow conventional commits format:

```
<type>(<scope>): <subject>

<body>

<footer>
```

**Types:**
- `feat` - New feature
- `fix` - Bug fix
- `docs` - Documentation changes
- `refactor` - Code refactoring
- `test` - Test changes
- `perf` - Performance improvements
- `chore` - Build/tooling changes

**Examples:**

```
feat(websocket): add session multiplexing support

Implement multiplexed WebSocket endpoint that allows multiple
terminal sessions over a single connection. Includes per-session
event filtering and subscription management.

Closes #123
```

```
fix(linux): handle script command not found error

Add proper error handling when util-linux script command is not
available. Provide clear error message with installation instructions.

Fixes #456
```

### Keep Your Fork Updated

```bash
# Fetch upstream changes
git fetch upstream

# Merge upstream main into your branch
git checkout main
git merge upstream/main

# Push to your fork
git push origin main
```

## Testing

### Running Tests

```bash
# Run all tests
dotnet test AureTTY.slnx -c Debug

# Run specific test project
dotnet test tests/AureTTY.Tests/AureTTY.Tests.csproj

# Run specific test
dotnet test --filter "FullyQualifiedName~ClassName.MethodName"

# Run with coverage
dotnet test --collect:"XPlat Code Coverage" --settings coverlet.runsettings
```

### Writing Tests

**Unit Tests** (`AureTTY.Core.Tests`):
- Test business logic in isolation
- Mock dependencies
- Fast execution
- High coverage

**Integration Tests** (`AureTTY.Tests`):
- Test full stack with TestHost
- Real HTTP/WebSocket clients
- Real serialization
- End-to-end scenarios

**Test Structure:**

```csharp
[Fact]
public async Task MethodName_WhenCondition_ExpectedBehavior()
{
    // Arrange
    var service = CreateService();
    var request = new Request { ... };

    // Act
    var result = await service.MethodAsync(request);

    // Assert
    Assert.NotNull(result);
    Assert.Equal(expected, result.Value);
}
```

### Test Coverage

Maintain test coverage above 85%:

```bash
# Generate coverage report
dotnet test --collect:"XPlat Code Coverage" --settings coverlet.runsettings
.\.tools\reportgenerator.exe -reports:"coverage-results\**\*.xml" -targetdir:"coverage-report"
```

## Submitting Changes

### Before Submitting

1. **Update your branch:**
   ```bash
   git fetch upstream
   git rebase upstream/main
   ```

2. **Run tests:**
   ```bash
   dotnet test AureTTY.slnx -c Release
   ```

3. **Check code style:**
   ```bash
   dotnet format AureTTY.slnx --verify-no-changes
   ```

4. **Build release:**
   ```bash
   dotnet build AureTTY.slnx -c Release
   ```

### Create Pull Request

1. Push your branch to your fork:
   ```bash
   git push origin feature/your-feature
   ```

2. Go to GitHub and create a Pull Request

3. Fill in the PR template:
   - Description of changes
   - Related issues
   - Testing performed
   - Breaking changes (if any)

4. Wait for CI checks to pass

5. Address review feedback

### PR Review Process

- Maintainers will review your PR
- Address feedback by pushing new commits
- Once approved, maintainers will merge

## Code Style

### C# Style Guide

Follow the project's `.editorconfig`:

- **Braces:** Allman style (braces on new line)
- **Indentation:** 4 spaces
- **Line endings:** LF (Unix)
- **Namespaces:** File-scoped
- **Var:** Prefer `var` for local variables
- **Braces:** Always required (even for single-line blocks)
- **Naming:**
  - PascalCase for types, methods, properties
  - camelCase for parameters, local variables
  - _camelCase for private fields
  - Interfaces prefixed with `I`

**Example:**

```csharp
namespace AureTTY.Core;

public sealed class TerminalSession : IDisposable
{
    private readonly string _sessionId;
    private readonly ILogger _logger;

    public TerminalSession(string sessionId, ILogger logger)
    {
        _sessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result> ProcessAsync(Request request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var result = await DoWorkAsync(cancellationToken);
        return result;
    }

    public void Dispose()
    {
        // Cleanup
    }
}
```

### Format Code

```bash
# Format all files
dotnet format AureTTY.slnx

# Check formatting without changes
dotnet format AureTTY.slnx --verify-no-changes
```

### Architecture Guidelines

1. **Layered Architecture:**
   - Contracts → Execution → Protocol → Core → Platform → Host
   - No circular dependencies
   - Lower layers don't reference upper layers

2. **Dependency Injection:**
   - Use constructor injection
   - Register services in `Program.cs`
   - Avoid service locator pattern

3. **Async/Await:**
   - All I/O operations must be async
   - Use `ConfigureAwait(false)` in libraries
   - Proper cancellation token support

4. **Error Handling:**
   - Use specific exception types
   - Don't catch and ignore exceptions
   - Log errors appropriately

5. **Resource Management:**
   - Implement `IDisposable` or `IAsyncDisposable`
   - Use `using` statements
   - Clean up resources in finally blocks

## Documentation

### Code Documentation

- **Public APIs:** XML documentation comments required
- **Complex logic:** Inline comments explaining why, not what
- **TODOs:** Use `// TODO:` with issue number

**Example:**

```csharp
/// <summary>
/// Starts a new terminal session with the specified configuration.
/// </summary>
/// <param name="viewerId">The viewer identifier.</param>
/// <param name="request">The session start request.</param>
/// <param name="cancellationToken">Cancellation token.</param>
/// <returns>A handle to the created session.</returns>
/// <exception cref="ArgumentNullException">Thrown when viewerId or request is null.</exception>
/// <exception cref="TerminalSessionConflictException">Thrown when session already exists.</exception>
public async Task<TerminalSessionHandle> StartAsync(
    string viewerId,
    TerminalSessionStartRequest request,
    CancellationToken cancellationToken = default)
{
    // Implementation
}
```

### User Documentation

When adding features, update:

- `README.md` - If user-facing feature
- `docs/api/REST_API.md` - If adding HTTP endpoints
- `docs/api/WEBSOCKET_API.md` - If adding WebSocket methods
- `docs/GETTING_STARTED.md` - If changing setup/usage
- `CLAUDE.md` - If changing build/development process

## Release Process

### Version Numbers

Follow Semantic Versioning (SemVer):

- **Major** (1.0.0): Breaking changes
- **Minor** (0.1.0): New features, backward compatible
- **Patch** (0.0.1): Bug fixes, backward compatible

### Release Checklist

1. Update version in `version.json`
2. Update `CHANGELOG.md`
3. Run full test suite
4. Build release binaries
5. Test release binaries
6. Create GitHub release
7. Publish NuGet packages (if applicable)

## Getting Help

- **Questions:** Open a [Discussion](https://github.com/vitkuz573/AureTTY/discussions)
- **Bugs:** Open an [Issue](https://github.com/vitkuz573/AureTTY/issues)
- **Documentation:** Check [docs/](docs/)

## Recognition

Contributors will be:
- Listed in release notes
- Credited in commit messages (`Co-Authored-By`)
- Mentioned in `CONTRIBUTORS.md` (if significant contribution)

## License

By contributing, you agree that your contributions will be licensed under the same terms as the project (MIT and Apache-2.0 dual license).

---

Thank you for contributing to AureTTY! 🚀

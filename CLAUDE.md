# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

AureTTY — standalone terminal runtime with HTTP+SSE and local pipe (IPC) transports. .NET 10, C#, multi-platform (Linux PTY via `script`, Windows ConPTY).

## Build & Test

```bash
# Restore & build (Linux — only net10.0 projects will build)
dotnet restore AureTTY.slnx
dotnet build AureTTY.slnx -c Release --no-restore

# On Windows, add flags to suppress OpenAPI doc generation:
# -p:OpenApiGenerateDocuments=false -p:OpenApiGenerateDocumentsOnBuild=false

# Run all tests
dotnet test tests/AureTTY.Tests/AureTTY.Tests.csproj -c Release
dotnet test tests/AureTTY.Core.Tests/AureTTY.Core.Tests.csproj -c Release

# Run a single test
dotnet test tests/AureTTY.Tests/AureTTY.Tests.csproj -c Release --filter "FullyQualifiedName~ClassName.MethodName"

# Publish (Linux, self-contained)
dotnet publish src/AureTTY/AureTTY.csproj -f net10.0 -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true

# Publish NativeAOT (Linux)
dotnet publish src/AureTTY/AureTTY.csproj -f net10.0 -c Release -r linux-x64 --self-contained true -p:PublishAot=true
```

Requires dotnet SDK 10.0.103. CI runs on AppVeyor (appveyor.yml).

## Architecture

Layered design, bottom-up:

- **AureTTY.Contracts** — interfaces, DTOs (MessagePack-annotated records), enums. No logic.
- **AureTTY.Execution** — process execution abstractions (`INativeProcess`, `INativeProcessFactory`).
- **AureTTY.Protocol** — IPC pipe protocol, MessagePack serialization.
- **AureTTY.Core** — transport-agnostic session management (`TerminalSessionService`), metrics, runtime limits.
- **AureTTY.Linux** — Linux PTY backend (launches `script` from util-linux). Target: `net10.0`.
- **AureTTY.Windows** — Windows ConPTY backend (CsWin32 interop). Target: `net10.0-windows`.
- **AureTTY** — host app. ASP.NET HTTP API + SSE event stream, pipe transport, CLI (System.CommandLine), Serilog logging.

Test projects mirror source: `AureTTY.Tests` (integration, uses TestHost), `AureTTY.Core.Tests` (unit).

## Multi-targeting

The host project multi-targets `net10.0` (Linux) and `net10.0-windows` (Windows). Platform backends are selected via conditional compilation: `AURETTY_WINDOWS_BACKEND`, `AURETTY_LINUX_BACKEND`, `AURETTY_NATIVEAOT`.

When running/publishing from CLI, specify framework explicitly: `-f net10.0` (Linux) or `-f net10.0-windows` (Windows).

## Conventions

- Allman brace style, 4-space indent, LF line endings (.editorconfig enforced)
- File-scoped namespaces, `var` preferred, braces always required
- Sealed classes for implementations, records for DTOs
- Primary constructors where applicable
- Interfaces prefixed with `I`, PascalCase everywhere
- Nullable reference types enabled globally
- Central package versioning (Directory.Packages.props)
- Versioning via Nerdbank.GitVersioning (version.json)

## Commit style

Conventional commits: `fix:`, `feat:`, `refactor:`, etc. Short English description.

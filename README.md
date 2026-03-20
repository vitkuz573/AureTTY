# AureTTY

[![Build](https://ci.appveyor.com/api/projects/status/github/vitkuz573/AureTTY?svg=true)](https://ci.appveyor.com/project/vitkuz573/AureTTY)
[![Latest Release](https://img.shields.io/github/v/release/vitkuz573/AureTTY?sort=semver)](https://github.com/vitkuz573/AureTTY/releases/latest)
[![Coverage](https://img.shields.io/badge/coverage-87.9%25-brightgreen)](#test-coverage)
[![License: MIT](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE-MIT)
[![License: Apache-2.0](https://img.shields.io/badge/license-Apache--2.0-blue.svg)](LICENSE-APACHE)

**AureTTY** is a high-performance, standalone terminal runtime with multiple transport options. Built with .NET 10, it provides HTTP REST API, Server-Sent Events (SSE), WebSocket, and local IPC pipe transports for language-agnostic terminal integration.

## Features

### Transport Options

- **HTTP REST API** (`/api/v1/*`) - Full-featured REST endpoints for terminal management
- **Server-Sent Events** (`/api/v1/viewers/{viewerId}/events`) - Real-time event streaming
- **WebSocket** (`/api/v1/viewers/{viewerId}/ws`) - Bidirectional real-time communication
  - **MessagePack Protocol** - Binary protocol for ~30-40% bandwidth reduction
  - **Session Multiplexing** - Multiple terminal sessions over single WebSocket connection
  - **Automatic Reconnection** - Resume with state recovery from 4096-event replay buffer
- **Local IPC Pipe** - High-performance named pipe transport for co-located processes

All transports can run simultaneously and share the same session state.

### Platform Support

- **Linux**: PTY backend via `script` from `util-linux` (target: `net10.0`)
- **Windows**: Native ConPTY backend (targets: `net10.0-windows`, `net10.0`)
- **NativeAOT**: Full support for ahead-of-time compilation on both platforms

### Advanced Capabilities

- **Session Management**: Create, attach, resize, send input, signal, and close terminal sessions
- **Multi-Viewer Support**: Isolated session namespaces per viewer with configurable limits
- **Replay Buffer**: Circular buffer (4096 events) for reconnection and late subscribers
- **Backpressure Handling**: Automatic event dropping with notifications when subscribers are slow
- **Runtime Limits**: Configurable session limits, buffer sizes, and resource constraints
- **Security**: Mandatory API key authentication with header-only or query parameter modes

## Quick Start

### Installation

```bash
# Clone the repository
git clone https://github.com/vitkuz573/AureTTY.git
cd AureTTY

# Build
dotnet build AureTTY.slnx -c Release

# Run with both HTTP and pipe transports
dotnet run --project src/AureTTY/AureTTY.csproj -f net10.0 -c Release -- \
  --transport pipe --transport http \
  --pipe-name auretty-terminal \
  --pipe-token your-secure-token \
  --http-listen-url http://127.0.0.1:17850 \
  --api-key your-secure-api-key
```

### Basic Usage

```bash
# Check service health
curl -H "X-AureTTY-Key: your-secure-api-key" http://localhost:17850/api/v1/health

# Create a terminal session
curl -X POST -H "X-AureTTY-Key: your-secure-api-key" \
  -H "Content-Type: application/json" \
  -d '{"sessionId":"my-session","shell":"bash"}' \
  http://localhost:17850/api/v1/viewers/my-viewer/sessions

# Send input to the session
curl -X POST -H "X-AureTTY-Key: your-secure-api-key" \
  -H "Content-Type: application/json" \
  -d '{"text":"echo hello\n","sequence":1}' \
  http://localhost:17850/api/v1/viewers/my-viewer/sessions/my-session/inputs

# Stream events via SSE
curl -H "X-AureTTY-Key: your-secure-api-key" \
  http://localhost:17850/api/v1/viewers/my-viewer/events
```

### WebSocket Example

```javascript
// Connect with MessagePack protocol for bandwidth efficiency
const ws = new WebSocket('ws://localhost:17850/api/v1/viewers/my-viewer/ws?protocol=msgpack&api_key=your-key');

ws.onopen = () => {
  // Start a terminal session
  ws.send(msgpack.encode({
    type: 'request',
    id: 'req-1',
    method: 'terminal.start',
    payload: {
      viewerId: 'my-viewer',
      request: { sessionId: 'session-1', shell: 'bash' }
    }
  }));
};

ws.onmessage = (event) => {
  const msg = msgpack.decode(event.data);
  if (msg.type === 'event' && msg.method === 'terminal.session') {
    console.log('Terminal output:', msg.payload.event.text);
  }
};
```

### Multiplexed WebSocket Example

```javascript
// Use the multiplexing endpoint for multiple sessions
const ws = new WebSocket('ws://localhost:17850/api/v1/viewers/my-viewer/sessions/ws?protocol=msgpack&api_key=your-key');

// Start multiple sessions on the same connection
ws.send(msgpack.encode({
  type: 'request',
  method: 'terminal.start',
  payload: { viewerId: 'my-viewer', request: { sessionId: 'session-1', shell: 'bash' } }
}));

ws.send(msgpack.encode({
  type: 'request',
  method: 'terminal.start',
  payload: { viewerId: 'my-viewer', request: { sessionId: 'session-2', shell: 'zsh' } }
}));

// Events include sessionId for routing
ws.onmessage = (event) => {
  const msg = msgpack.decode(event.data);
  console.log(`Session ${msg.payload.event.sessionId}: ${msg.payload.event.text}`);
};
```

## Configuration

### Command Line Options

```bash
# Transport configuration
--transport pipe|http              # Enable transports (repeatable)
--pipe-name <name>                 # Named pipe name (default: auretty-terminal)
--pipe-token <token>               # Pipe authentication token (required for pipe)
--http-listen-url <url>            # HTTP listen URL (default: http://127.0.0.1:17850)
--api-key <key>                    # HTTP API key (required for HTTP)
--allow-api-key-query              # Allow API key in query string (disabled by default)

# Runtime limits
--max-concurrent-sessions <n>      # Max total sessions (default: 32)
--max-sessions-per-viewer <n>      # Max sessions per viewer (default: 8)
--replay-buffer-capacity <n>       # Event replay buffer size (default: 4096)
--max-pending-input-chunks <n>     # Max queued input chunks (default: 8192)
--sse-subscription-buffer-capacity <n>  # SSE buffer size (default: 2048)
```

### Environment Variables

All CLI options have corresponding environment variables:

```bash
AURETTY_TRANSPORTS=pipe,http
AURETTY_PIPE_NAME=auretty-terminal
AURETTY_PIPE_TOKEN=your-token
AURETTY_HTTP_LISTEN_URL=http://127.0.0.1:17850
AURETTY_API_KEY=your-api-key
AURETTY_ALLOW_API_KEY_QUERY=false
AURETTY_MAX_CONCURRENT_SESSIONS=32
AURETTY_MAX_SESSIONS_PER_VIEWER=8
AURETTY_REPLAY_BUFFER_CAPACITY=4096
AURETTY_MAX_PENDING_INPUT_CHUNKS=8192
AURETTY_SSE_SUBSCRIPTION_BUFFER_CAPACITY=2048
```

## API Reference

### HTTP Endpoints

#### Health & Discovery

- `GET /api/v1/health` - Service health and capabilities
- `GET /openapi/v1.json` - OpenAPI specification (requires authentication)

#### Session Management

- `GET /api/v1/sessions` - List all sessions
- `DELETE /api/v1/sessions` - Close all sessions
- `GET /api/v1/viewers/{viewerId}/sessions` - List viewer sessions
- `POST /api/v1/viewers/{viewerId}/sessions` - Create new session
- `GET /api/v1/viewers/{viewerId}/sessions/{sessionId}` - Get session details
- `POST /api/v1/viewers/{viewerId}/sessions/{sessionId}/attachments` - Attach to existing session
- `DELETE /api/v1/viewers/{viewerId}/sessions/{sessionId}` - Close session
- `DELETE /api/v1/viewers/{viewerId}/sessions` - Close all viewer sessions

#### Session Operations

- `POST /api/v1/viewers/{viewerId}/sessions/{sessionId}/inputs` - Send input
- `GET /api/v1/viewers/{viewerId}/sessions/{sessionId}/input-diagnostics` - Get input diagnostics
- `PUT /api/v1/viewers/{viewerId}/sessions/{sessionId}/terminal-size` - Resize terminal
- `POST /api/v1/viewers/{viewerId}/sessions/{sessionId}/signals` - Send signal (SIGINT, SIGTERM, etc.)

#### Event Streaming

- `GET /api/v1/viewers/{viewerId}/events` - Server-Sent Events stream

### WebSocket Endpoints

- `GET /api/v1/viewers/{viewerId}/ws` - Single-session WebSocket (legacy)
- `GET /api/v1/viewers/{viewerId}/sessions/ws` - Multiplexed WebSocket (recommended)

Query parameters:
- `protocol=json|msgpack|messagepack` - Protocol selection (default: json)
- `api_key=<key>` - API key authentication (if enabled)

### WebSocket IPC Methods

All WebSocket messages follow the IPC message format:

```json
{
  "type": "request|response|event|error",
  "id": "unique-request-id",
  "method": "terminal.method",
  "payload": { ... },
  "error": "error message"
}
```

#### Available Methods

- `terminal.ping` - Ping/pong for keepalive
- `terminal.start` - Start new terminal session
- `terminal.resume` - Resume existing session with replay
- `terminal.input` - Send input to session
- `terminal.input-diagnostics` - Get input diagnostics
- `terminal.resize` - Resize terminal
- `terminal.signal` - Send signal to session
- `terminal.close` - Close session
- `terminal.close-viewer-sessions` - Close all viewer sessions
- `terminal.close-all-sessions` - Close all sessions

#### Event Types

Events are published via `terminal.session` method:

- `Started` - Session started
- `Attached` - Session attached
- `Output` - Terminal output
- `Exited` - Process exited
- `Closed` - Session closed
- `Dropped` - Events were dropped (backpressure)

### Reconnection & State Recovery

When a WebSocket disconnects, clients can reconnect and resume:

```javascript
let lastSequenceNumber = 0;

ws.onmessage = (event) => {
  const msg = decode(event.data);
  if (msg.payload?.event?.sequenceNumber) {
    lastSequenceNumber = msg.payload.event.sequenceNumber;
  }
};

// On reconnect
ws.send(encode({
  type: 'request',
  method: 'terminal.resume',
  payload: {
    viewerId: 'my-viewer',
    request: {
      sessionId: 'my-session',
      lastReceivedSequenceNumber: lastSequenceNumber
    }
  }
}));
```

The server will replay all events with `sequenceNumber > lastReceivedSequenceNumber` from the circular buffer (4096 events).

## Architecture

### Project Structure

```
src/
├── AureTTY.Contracts/      # Interfaces, DTOs, enums
├── AureTTY.Execution/      # Process execution abstractions
├── AureTTY.Protocol/       # IPC protocol, MessagePack serialization
├── AureTTY.Core/           # Session management, metrics, limits
├── AureTTY.Linux/          # Linux PTY backend (net10.0)
├── AureTTY.Windows/        # Windows ConPTY backend (net10.0-windows)
└── AureTTY/                # Host app, HTTP API, WebSocket, CLI

tests/
├── AureTTY.Tests/          # Integration tests (117 tests)
└── AureTTY.Core.Tests/     # Unit tests (35 tests)

demos/
├── linux/                  # Linux demo scripts
└── windows/                # Windows demo scripts
```

### Design Principles

- **Layered Architecture**: Clear separation between contracts, execution, protocol, core logic, and platform backends
- **Transport Agnostic**: Session management is independent of transport layer
- **Multi-Platform**: Conditional compilation for platform-specific backends
- **NativeAOT Ready**: Source generation for JSON/MessagePack, no reflection
- **Testable**: 117 tests with 87.9% line coverage

## Platform-Specific Notes

### Linux

**Requirements:**
- `script` binary from `util-linux` package
- `sudo` for credential switching (if using explicit credentials)

**Installation:**
```bash
# Debian/Ubuntu
sudo apt-get install util-linux

# RHEL/CentOS/Fedora
sudo yum install util-linux
```

**NativeAOT Build:**
```bash
dotnet publish src/AureTTY/AureTTY.csproj \
  -f net10.0 -c Release -r linux-x64 \
  --self-contained true -p:PublishAot=true \
  -p:OpenApiGenerateDocuments=false \
  -p:OpenApiGenerateDocumentsOnBuild=false \
  -o artifacts/publish/linux-x64-aot

# Run smoke test
bash demos/linux/run-linux-aot-smoke.sh
```

### Windows

**Requirements:**
- Windows 10 1809+ or Windows Server 2019+ (for ConPTY support)
- PowerShell 7 (`pwsh`) or Windows PowerShell (`powershell`)

**NativeAOT Build:**
```powershell
dotnet publish src/AureTTY/AureTTY.csproj `
  -f net10.0-windows -c Release -r win-x64 `
  --self-contained true -p:PublishAot=true `
  -p:OpenApiGenerateDocuments=false `
  -p:OpenApiGenerateDocumentsOnBuild=false `
  -o artifacts/publish/win-x64-aot

# Run smoke test
pwsh -NoLogo -NoProfile -File demos/windows/run-windows-aot-smoke.ps1 `
  -AureTTYExecutable artifacts/publish/win-x64-aot/AureTTY.exe
```

## Development

### Building

```bash
# Restore dependencies
dotnet restore AureTTY.slnx

# Build (Linux)
dotnet build AureTTY.slnx -c Release --no-restore

# Build (Windows - suppress OpenAPI generation)
dotnet build AureTTY.slnx -c Release --no-restore `
  -p:OpenApiGenerateDocuments=false `
  -p:OpenApiGenerateDocumentsOnBuild=false
```

### Testing

```bash
# Run all tests
dotnet test tests/AureTTY.Tests/AureTTY.Tests.csproj -c Release
dotnet test tests/AureTTY.Core.Tests/AureTTY.Core.Tests.csproj -c Release

# Run specific test
dotnet test tests/AureTTY.Tests/AureTTY.Tests.csproj -c Release \
  --filter "FullyQualifiedName~ClassName.MethodName"

# Run with coverage
dotnet test tests/AureTTY.Tests/AureTTY.Tests.csproj -c Debug \
  --collect:"XPlat Code Coverage" \
  --settings coverlet.runsettings \
  --results-directory coverage-results/tests

dotnet test tests/AureTTY.Core.Tests/AureTTY.Core.Tests.csproj -c Debug \
  --collect:"XPlat Code Coverage" \
  --settings coverlet.runsettings \
  --results-directory coverage-results/core

# Generate coverage report
.\.tools\reportgenerator.exe \
  -reports:"coverage-results\**\coverage.cobertura.xml" \
  -targetdir:"coverage-report" \
  -reporttypes:"HtmlInline;TextSummary;Cobertura;Badges"
```

### Code Style

- Allman brace style, 4-space indentation, LF line endings
- File-scoped namespaces, `var` preferred, braces always required
- Sealed classes for implementations, records for DTOs
- Primary constructors where applicable
- Nullable reference types enabled globally
- See `.editorconfig` for full style rules

## Test Coverage

Current baseline (2026-03-20):

- **Line Coverage**: 87.9%
- **Branch Coverage**: 72.2%
- **Total Tests**: 117 (82 integration + 35 unit)

## Security Considerations

### Authentication

- API key is **mandatory** when HTTP transport is enabled
- Pipe token is **mandatory** when pipe transport is enabled
- Default: API key accepted only via `X-AureTTY-Key` header
- Query parameter auth (`?api_key=...`) is **disabled by default**
- Use `--allow-api-key-query` only for development/testing

### Best Practices

- Use strong, randomly generated API keys and pipe tokens
- Enable HTTPS in production (use reverse proxy like nginx/Caddy)
- Restrict network access to trusted clients
- Monitor session limits to prevent resource exhaustion
- Review logs for unauthorized access attempts

## Performance

### Benchmarks

- **WebSocket MessagePack**: ~30-40% bandwidth reduction vs JSON
- **Session Multiplexing**: Single connection for unlimited sessions
- **Replay Buffer**: 4096 events with O(1) access
- **NativeAOT**: ~50ms startup time, ~30MB memory footprint

### Tuning

```bash
# High-throughput configuration
--max-concurrent-sessions 128 \
--max-sessions-per-viewer 32 \
--replay-buffer-capacity 8192 \
--max-pending-input-chunks 16384 \
--sse-subscription-buffer-capacity 4096

# Low-latency configuration
--replay-buffer-capacity 1024 \
--max-pending-input-chunks 2048 \
--sse-subscription-buffer-capacity 512
```

## Troubleshooting

### Common Issues

**"API key is required"**
- Ensure `X-AureTTY-Key` header is set
- Or enable query parameter auth with `--allow-api-key-query`

**"Maximum concurrent sessions reached"**
- Increase `--max-concurrent-sessions` limit
- Close unused sessions

**"Events dropped" notifications**
- Subscriber is too slow to consume events
- Increase `--sse-subscription-buffer-capacity`
- Optimize client event processing

**Linux: "script: command not found"**
- Install `util-linux` package

**Windows: ConPTY errors**
- Requires Windows 10 1809+ or Windows Server 2019+
- Update Windows to latest version

### Debug Logging

```bash
# Enable verbose logging
AURETTY_LOG_LEVEL=Debug dotnet run --project src/AureTTY/AureTTY.csproj ...
```

## Contributing

Contributions are welcome! Please:

1. Fork the repository
2. Create a feature branch
3. Follow existing code style (see `.editorconfig`)
4. Add tests for new functionality
5. Ensure all tests pass
6. Submit a pull request

## License

Dual licensed under your choice of:

- MIT License ([LICENSE-MIT](LICENSE-MIT))
- Apache License 2.0 ([LICENSE-APACHE](LICENSE-APACHE))

## Acknowledgments

Built with:
- [.NET 10](https://dotnet.microsoft.com/)
- [MessagePack-CSharp](https://github.com/MessagePack-CSharp/MessagePack-CSharp)
- [Serilog](https://serilog.net/)
- [xUnit](https://xunit.net/)

## Links

- [GitHub Repository](https://github.com/vitkuz573/AureTTY)
- [Issue Tracker](https://github.com/vitkuz573/AureTTY/issues)
- [CI/CD Pipeline](https://ci.appveyor.com/project/vitkuz573/AureTTY)
- [Latest Release](https://github.com/vitkuz573/AureTTY/releases/latest)

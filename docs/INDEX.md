# Documentation Index

Complete documentation for AureTTY - a high-performance terminal runtime with multiple transport options.

## Getting Started

- **[Getting Started Guide](GETTING_STARTED.md)** - Installation, quick start, and first steps
- **[README](../README.md)** - Project overview and features

## API Reference

- **[REST API Reference](api/REST_API.md)** - Complete HTTP API documentation
- **[WebSocket API Reference](api/WEBSOCKET_API.md)** - WebSocket protocol and IPC methods

## Guides

- **[WebSocket Guide](guides/WEBSOCKET_GUIDE.md)** - Advanced WebSocket features, multiplexing, and reconnection

## Examples

- **[Python Client](examples/python_client.md)** - Complete Python client with HTTP and WebSocket support
- **[JavaScript/TypeScript Client](examples/javascript_client.md)** - Complete JS/TS client with React hooks

## Architecture

- **[Architecture Overview](architecture/OVERVIEW.md)** - System design, layers, and implementation details

## Troubleshooting

- **[Troubleshooting Guide](TROUBLESHOOTING.md)** - Common issues and solutions

## Platform-Specific

- **[OpenWRT Support](openwrt/README.md)** - Building and deploying on OpenWRT routers
  - [Build Guide](openwrt/BUILD.md) - Cross-compilation for embedded devices
  - [Package Guide](openwrt/PACKAGE.md) - Creating OpenWRT packages
  - [QEMU Testing](openwrt/QEMU_TESTING.md) - Testing in virtual machines

## Quick Links

### For New Users
1. [Installation](GETTING_STARTED.md#installation)
2. [Basic Usage](GETTING_STARTED.md#basic-usage)
3. [WebSocket Example](GETTING_STARTED.md#using-websocket)

### For Developers
1. [REST API Endpoints](api/REST_API.md#endpoints)
2. [WebSocket IPC Methods](api/WEBSOCKET_API.md#ipc-methods)
3. [Client Examples](examples/)

### For System Architects
1. [Architecture Overview](architecture/OVERVIEW.md#high-level-architecture)
2. [Layered Design](architecture/OVERVIEW.md#layered-design)
3. [Performance Considerations](architecture/OVERVIEW.md#performance-considerations)

### For Embedded Systems
1. [OpenWRT Overview](openwrt/README.md)
2. [Build for OpenWRT](openwrt/BUILD.md)
3. [Test in QEMU](openwrt/QEMU_TESTING.md)

## Features by Topic

### Transport Options
- [HTTP REST API](api/REST_API.md)
- [WebSocket (JSON/MessagePack)](api/WEBSOCKET_API.md)
- [IPC Pipe](architecture/OVERVIEW.md#ipc-pipe)

### Advanced WebSocket Features
- [MessagePack Protocol](guides/WEBSOCKET_GUIDE.md#messagepack-protocol) - ~30-40% bandwidth reduction
- [Session Multiplexing](guides/WEBSOCKET_GUIDE.md#session-multiplexing) - Multiple sessions per connection
- [Reconnection & State Recovery](guides/WEBSOCKET_GUIDE.md#reconnection-and-state-recovery) - Resume with replay buffer

### Session Management
- [Creating Sessions](api/REST_API.md#create-terminal-session)
- [Sending Input](api/REST_API.md#send-input)
- [Resizing Terminals](api/REST_API.md#resize-terminal)
- [Sending Signals](api/REST_API.md#send-signal)
- [Closing Sessions](api/REST_API.md#close-session)

### Platform Support
- [Linux Backend](architecture/OVERVIEW.md#linux-backend-aurelinux) - PTY via `script`
- [Windows Backend](architecture/OVERVIEW.md#windows-backend-aurewindows) - Native ConPTY
- [NativeAOT](../README.md#nativeaot) - Ahead-of-time compilation

## Common Tasks

### How do I...

**...create a terminal session?**
- HTTP: [Create Session](api/REST_API.md#create-terminal-session)
- WebSocket: [terminal.start](api/WEBSOCKET_API.md#terminalstart)

**...stream terminal output?**
- WebSocket: [Events](api/WEBSOCKET_API.md#events)

**...use MessagePack for better performance?**
- [MessagePack Protocol Guide](guides/WEBSOCKET_GUIDE.md#messagepack-protocol)
- [Protocol Selection](api/WEBSOCKET_API.md#protocol-selection)

**...handle multiple sessions efficiently?**
- [Session Multiplexing Guide](guides/WEBSOCKET_GUIDE.md#session-multiplexing)
- [Multiplexed Endpoint](api/WEBSOCKET_API.md#multiplexed-websocket-recommended)

**...implement reconnection?**
- [Reconnection Guide](guides/WEBSOCKET_GUIDE.md#reconnection-and-state-recovery)
- [Replay Buffer](architecture/OVERVIEW.md#replay-buffer)

**...integrate with my application?**
- Python: [Python Client Example](examples/python_client.md)
- JavaScript: [JavaScript Client Example](examples/javascript_client.md)
- Other languages: [REST API Reference](api/REST_API.md)

**...troubleshoot connection issues?**
- [Connection Issues](TROUBLESHOOTING.md#connection-issues)
- [Authentication Issues](TROUBLESHOOTING.md#authentication-issues)

**...optimize performance?**
- [Performance Optimization](guides/WEBSOCKET_GUIDE.md#performance-optimization)
- [Performance Considerations](architecture/OVERVIEW.md#performance-considerations)

## API Quick Reference

### HTTP Endpoints

```
GET    /api/v1/health
GET    /api/v1/sessions
DELETE /api/v1/sessions
GET    /api/v1/viewers/{viewerId}/sessions
POST   /api/v1/viewers/{viewerId}/sessions
GET    /api/v1/viewers/{viewerId}/sessions/{sessionId}
POST   /api/v1/viewers/{viewerId}/sessions/{sessionId}/attachments
POST   /api/v1/viewers/{viewerId}/sessions/{sessionId}/inputs
GET    /api/v1/viewers/{viewerId}/sessions/{sessionId}/input-diagnostics
PUT    /api/v1/viewers/{viewerId}/sessions/{sessionId}/terminal-size
POST   /api/v1/viewers/{viewerId}/sessions/{sessionId}/signals
DELETE /api/v1/viewers/{viewerId}/sessions/{sessionId}
DELETE /api/v1/viewers/{viewerId}/sessions
```

### WebSocket Endpoints

```
GET /api/v1/viewers/{viewerId}/sessions/ws
```

Query parameters: `?protocol=json|msgpack`

### WebSocket IPC Methods

```
hello                            - Required WebSocket handshake
terminal.ping                    - Ping/pong keepalive
terminal.start                   - Start new session
terminal.resume                  - Resume with replay
terminal.input                   - Send input
terminal.input-diagnostics       - Get input diagnostics
terminal.resize                  - Resize terminal
terminal.signal                  - Send signal
terminal.close                   - Close session
terminal.close-viewer-sessions   - Close all viewer sessions
terminal.close-all-sessions      - Close all sessions
```

### Event Types

```
started   - Session started
attached  - Session attached/resumed
output    - Terminal output
exited    - Process exited
closed    - Session closed
dropped   - Events dropped (backpressure)
```

## Configuration Quick Reference

### CLI Options

```bash
--transport pipe|http              # Enable transports
--pipe-name <name>                 # Pipe name
--pipe-token <token>               # Pipe token
--http-listen-url <url>            # HTTP URL
--api-key <key>                    # API key
--ws-subscription-buffer-capacity <n>  # WebSocket buffer size
--ws-hello-timeout-seconds <n>         # Hello handshake timeout
--max-concurrent-sessions <n>      # Max total sessions
--max-sessions-per-viewer <n>      # Max per viewer
--replay-buffer-capacity <n>       # Replay buffer size
--max-pending-input-chunks <n>     # Input queue size
--session-idle-timeout-seconds <n> # Idle timeout
--session-hard-lifetime-seconds <n># Hard lifetime timeout
```

### Environment Variables

```bash
AURETTY_TRANSPORTS
AURETTY_PIPE_NAME
AURETTY_PIPE_TOKEN
AURETTY_HTTP_LISTEN_URL
AURETTY_API_KEY
AURETTY_WS_SUBSCRIPTION_BUFFER_CAPACITY
AURETTY_WS_HELLO_TIMEOUT_SECONDS
AURETTY_MAX_CONCURRENT_SESSIONS
AURETTY_MAX_SESSIONS_PER_VIEWER
AURETTY_REPLAY_BUFFER_CAPACITY
AURETTY_MAX_PENDING_INPUT_CHUNKS
AURETTY_SESSION_IDLE_TIMEOUT_SECONDS
AURETTY_SESSION_HARD_LIFETIME_SECONDS
```

## Code Examples

### cURL

```bash
# Create session
curl -X POST -H "X-AureTTY-Key: key" \
  -d '{"shell":"bash"}' \
  http://localhost:17850/api/v1/viewers/v1/sessions

# Send input
curl -X POST -H "X-AureTTY-Key: key" \
  -d '{"text":"echo hello\n","sequence":1}' \
  http://localhost:17850/api/v1/viewers/v1/sessions/s1/inputs

# Realtime output is available via WebSocket /sessions/ws
```

### Python

```python
from auretty_client import AureTTYClient

client = AureTTYClient('http://localhost:17850/api/v1', 'api-key')
session = client.create_session('viewer-1', shell='bash')
client.send_input('viewer-1', session['sessionId'], 'ls\n', 1)
```

### JavaScript

```javascript
const client = new AureTTYWebSocketClient(
  'ws://localhost:17850/api/v1',
  'viewer-1',
  'api-key',
  { protocol: 'msgpack' }
);

await client.connect();
await client.startSession('session-1', 'bash');
await client.sendInput('session-1', 'ls\n', 1);
```

## Support

- **GitHub Issues**: https://github.com/vitkuz573/AureTTY/issues
- **Discussions**: https://github.com/vitkuz573/AureTTY/discussions
- **Documentation**: https://github.com/vitkuz573/AureTTY/tree/main/docs

## Contributing

See [CLAUDE.md](../CLAUDE.md) for development guidelines and conventions.

## License

Dual licensed under MIT and Apache-2.0. See [LICENSE-MIT](../LICENSE-MIT) and [LICENSE-APACHE](../LICENSE-APACHE).

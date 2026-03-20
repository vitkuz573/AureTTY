# AureTTY Architecture Overview

This document provides a comprehensive overview of AureTTY's architecture, design principles, and implementation details.

## Table of Contents

- [High-Level Architecture](#high-level-architecture)
- [Layered Design](#layered-design)
- [Transport Layer](#transport-layer)
- [Session Management](#session-management)
- [Platform Backends](#platform-backends)
- [Data Flow](#data-flow)
- [Concurrency Model](#concurrency-model)
- [Security Architecture](#security-architecture)
- [Performance Considerations](#performance-considerations)

## High-Level Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                         Clients                              │
│  (HTTP, SSE, WebSocket, IPC Pipe)                           │
└────────────────┬────────────────────────────────────────────┘
                 │
┌────────────────▼────────────────────────────────────────────┐
│                    Transport Layer                           │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐   │
│  │   HTTP   │  │   SSE    │  │WebSocket │  │IPC Pipe  │   │
│  │   API    │  │  Stream  │  │(JSON/MP) │  │(JSON/MP) │   │
│  └──────────┘  └──────────┘  └──────────┘  └──────────┘   │
└────────────────┬────────────────────────────────────────────┘
                 │
┌────────────────▼────────────────────────────────────────────┐
│              Session Management Layer                        │
│  ┌──────────────────────────────────────────────────────┐  │
│  │         TerminalSessionService                        │  │
│  │  - Session lifecycle (create, attach, close)         │  │
│  │  - Input queue management                            │  │
│  │  - Event replay buffer (4096 events)                 │  │
│  │  - Runtime limits enforcement                        │  │
│  └──────────────────────────────────────────────────────┘  │
└────────────────┬────────────────────────────────────────────┘
                 │
┌────────────────▼────────────────────────────────────────────┐
│              Platform Abstraction Layer                      │
│  ┌──────────────────────────────────────────────────────┐  │
│  │         INativeProcessFactory                         │  │
│  │         INativeProcess                                │  │
│  └──────────────────────────────────────────────────────┘  │
└────────────────┬────────────────────────────────────────────┘
                 │
         ┌───────┴───────┐
         │               │
┌────────▼──────┐  ┌────▼──────────┐
│ Linux Backend │  │Windows Backend│
│  (PTY/script) │  │   (ConPTY)    │
└───────────────┘  └───────────────┘
```

## Layered Design

AureTTY follows a strict layered architecture with clear separation of concerns:

### Layer 1: Contracts (Bottom)

**Project:** `AureTTY.Contracts`

**Purpose:** Define interfaces, DTOs, and enums shared across all layers.

**Key Components:**
- `ITerminalSessionService` - Core session management interface
- `ITerminalSessionEventPublisher` - Event publishing interface
- `TerminalSessionHandle` - Session state DTO
- `TerminalSessionEvent` - Event DTO
- `TerminalSessionState` - State enum

**Dependencies:** None (pure contracts)

**Design Principles:**
- No implementation logic
- Immutable DTOs (records)
- MessagePack annotations for serialization
- Platform-agnostic

### Layer 2: Execution

**Project:** `AureTTY.Execution`

**Purpose:** Abstract process execution without platform-specific details.

**Key Components:**
- `INativeProcessFactory` - Factory for creating processes
- `INativeProcess` - Process abstraction
- `ProcessStartInfo` - Platform-agnostic start parameters

**Dependencies:** `AureTTY.Contracts`

**Design Principles:**
- Platform-agnostic abstractions
- Async/await throughout
- Cancellation token support
- Resource cleanup via IDisposable

### Layer 3: Protocol

**Project:** `AureTTY.Protocol`

**Purpose:** IPC protocol definitions and serialization.

**Key Components:**
- `TerminalIpcMessage` - Base IPC message
- `TerminalIpcPayloads` - Request/response payloads
- `TerminalIpcMethods` - Method name constants
- `TerminalIpcMessageTypes` - Message type constants

**Dependencies:** `AureTTY.Contracts`

**Design Principles:**
- Protocol versioning support
- MessagePack and JSON serialization
- Strongly-typed payloads
- Backward compatibility

### Layer 4: Core

**Project:** `AureTTY.Core`

**Purpose:** Transport-agnostic session management and business logic.

**Key Components:**
- `TerminalSessionService` - Main session orchestrator
- `TerminalSession` - Session state machine
- `TerminalMetrics` - Performance metrics
- `RuntimeLimits` - Resource constraints

**Dependencies:** `AureTTY.Contracts`, `AureTTY.Execution`

**Design Principles:**
- Transport-agnostic (no HTTP/WebSocket knowledge)
- Thread-safe (ConcurrentDictionary)
- Event-driven architecture
- Circular replay buffer
- Backpressure handling

**Key Features:**
- **Session Lifecycle:** Start → Running → Exited → Closed
- **Input Queue:** Bounded queue with sequence numbers
- **Replay Buffer:** Circular buffer (4096 events) for reconnection
- **Runtime Limits:** Max sessions, max sessions per viewer
- **Metrics:** Session count, event count, dropped events

### Layer 5: Platform Backends

**Projects:** `AureTTY.Linux`, `AureTTY.Windows`

**Purpose:** Platform-specific terminal implementations.

#### Linux Backend (`AureTTY.Linux`)

**Target:** `net10.0`

**Implementation:**
- Uses `script` command from `util-linux`
- PTY (pseudo-terminal) support
- Credential switching via `sudo -S`

**Key Components:**
- `LinuxNativeProcessFactory`
- `LinuxNativeProcess`
- `ScriptProcessLauncher`

**Process Flow:**
```bash
# Standard launch
script -q -c "bash" /dev/null

# With credential switching
sudo -S -u username script -q -c "bash" /dev/null
```

#### Windows Backend (`AureTTY.Windows`)

**Target:** `net10.0-windows`

**Implementation:**
- Native ConPTY API via CsWin32
- Windows process creation
- Credential switching via CreateProcessWithLogonW

**Key Components:**
- `WindowsNativeProcessFactory`
- `WindowsNativeProcess`
- `ConPtyProcessLauncher`

**Requirements:**
- Windows 10 1809+ or Windows Server 2019+
- ConPTY support

### Layer 6: Host Application

**Project:** `AureTTY`

**Purpose:** HTTP API, WebSocket, IPC pipe, and CLI.

**Key Components:**
- `TerminalHttpEndpointRouteBuilderExtensions` - HTTP endpoints
- `TerminalWebSocketHandler` - WebSocket handler
- `TerminalPipeServer` - IPC pipe server
- `HttpTerminalSessionEventPublisher` - SSE publisher
- `WebSocketTerminalSessionEventPublisher` - WebSocket publisher
- `PipeTerminalSessionEventPublisher` - Pipe publisher

**Dependencies:** All layers

**Design Principles:**
- ASP.NET Core minimal APIs
- System.CommandLine for CLI
- Serilog for logging
- NativeAOT compatible

## Transport Layer

### HTTP REST API

**Endpoints:** `/api/v1/*`

**Features:**
- Full CRUD operations
- OpenAPI specification
- JSON serialization (source-generated)
- API key authentication

**Implementation:**
- ASP.NET Core minimal APIs
- `IResult` return types
- Dependency injection
- Middleware pipeline

### Server-Sent Events (SSE)

**Endpoint:** `/api/v1/viewers/{viewerId}/events`

**Features:**
- Real-time event streaming
- One-way server-to-client
- Automatic reconnection (browser)
- Text-based protocol

**Implementation:**
- `HttpTerminalSessionEventPublisher`
- Channel-based event distribution
- Bounded channel with backpressure
- Per-viewer subscriptions

**Event Format:**
```
event: terminal.session
data: {"sessionId":"s1","eventType":"output","text":"hello\n"}

```

### WebSocket

**Endpoints:**
- `/api/v1/viewers/{viewerId}/ws` - Single-session (legacy)
- `/api/v1/viewers/{viewerId}/sessions/ws` - Multiplexed (recommended)

**Features:**
- Bidirectional communication
- JSON and MessagePack protocols
- Session multiplexing
- Automatic reconnection with replay

**Implementation:**
- `TerminalWebSocketHandler`
- `WebSocketTerminalSessionEventPublisher`
- Protocol negotiation via query parameter
- Connection state tracking for multiplexing

**Protocol Selection:**
```
?protocol=json       → WebSocketMessageType.Text
?protocol=msgpack    → WebSocketMessageType.Binary
```

### IPC Pipe

**Platform:** Named pipes (Windows/Linux)

**Features:**
- High-performance local IPC
- JSON and MessagePack protocols
- Token-based authentication
- Single client connection

**Implementation:**
- `TerminalPipeServer`
- `PipeTerminalSessionEventPublisher`
- Line-delimited JSON/MessagePack
- Handshake protocol

**Handshake Flow:**
```
Client → Server: {"type":"hello","payload":{"token":"..."}}
Server → Client: {"type":"hello","payload":{"token":"..."}}
```

## Session Management

### Session Lifecycle

```
┌─────────┐
│ Created │
└────┬────┘
     │ Start
     ▼
┌─────────┐
│ Running │◄──┐
└────┬────┘   │ Attach/Resume
     │        │
     │ Exit   │
     ▼        │
┌─────────┐  │
│ Exited  ├──┘
└────┬────┘
     │ Close
     ▼
┌─────────┐
│ Closed  │
└─────────┘
```

### State Transitions

- **Created → Running:** `StartAsync()` - Launch process
- **Running → Running:** `ResumeAsync()` - Attach to existing session
- **Running → Exited:** Process exits naturally
- **Exited → Closed:** `CloseAsync()` - Clean up resources
- **Running → Closed:** `CloseAsync()` - Terminate and clean up

### Session Components

```
TerminalSession
├── SessionId (string)
├── ViewerId (string)
├── State (enum)
├── Process (INativeProcess)
├── InputQueue (Channel<InputChunk>)
├── OutputBuffer (CircularBuffer<Event>)
└── Metrics (counters, timestamps)
```

### Input Processing

```
Client Input
     │
     ▼
┌──────────────┐
│ Input Queue  │ (Bounded Channel)
│ Max: 8192    │
└──────┬───────┘
       │
       ▼
┌──────────────┐
│ Sequencing   │ (Order by sequence number)
└──────┬───────┘
       │
       ▼
┌──────────────┐
│ Write to PTY │
└──────────────┘
```

### Output Processing

```
PTY Output
     │
     ▼
┌──────────────┐
│ Read Buffer  │
└──────┬───────┘
       │
       ▼
┌──────────────┐
│ Event Create │ (Add sequence number)
└──────┬───────┘
       │
       ├──────────────┐
       │              │
       ▼              ▼
┌──────────────┐  ┌──────────────┐
│Replay Buffer │  │Event Publisher│
│ (4096 events)│  │ (All transports)
└──────────────┘  └──────────────┘
```

### Replay Buffer

**Implementation:** Circular buffer with sequence numbers

**Capacity:** 4096 events (configurable)

**Purpose:**
- Reconnection support
- Late subscriber catch-up
- Event replay on `ResumeAsync()`

**Algorithm:**
```csharp
public IReadOnlyList<Event> GetReplay(long lastSeq)
{
    return buffer
        .Where(e => e.SequenceNumber > lastSeq)
        .OrderBy(e => e.SequenceNumber)
        .ToList();
}
```

## Concurrency Model

### Thread Safety

**Session Storage:**
```csharp
ConcurrentDictionary<string, TerminalSession> _sessions
```

**Event Publishing:**
```csharp
ConcurrentDictionary<string, EventSubscription> _subscriptions
```

**Input Queue:**
```csharp
Channel<InputChunk> (thread-safe by design)
```

### Async/Await

- All I/O operations are async
- No blocking calls in hot paths
- CancellationToken support throughout
- Proper async disposal (IAsyncDisposable)

### Backpressure Handling

**Input Queue:**
- Bounded channel (8192 chunks)
- Returns 429 when full
- Client must slow down

**Event Subscriptions:**
- Bounded channel (2048 events)
- Drops events when full
- Sends "dropped" notification

**Strategy:**
```csharp
if (!channel.Writer.TryWrite(event))
{
    // Record dropped event
    subscription.RecordDroppedEvent();
    metrics.RecordEventDropped();
}
```

## Security Architecture

### Authentication

**HTTP/WebSocket:**
- API key required (mandatory)
- Header: `X-AureTTY-Key`
- Query parameter: `?api_key=...` (opt-in)
- Constant-time comparison (timing attack prevention)

**IPC Pipe:**
- Token-based handshake
- Single client connection
- Token must match exactly

### Authorization

**Viewer Isolation:**
- Sessions belong to viewers
- Cross-viewer access denied (403)
- Viewer ID in all operations

**Session Ownership:**
```csharp
if (session.ViewerId != requestViewerId)
{
    throw new TerminalSessionForbiddenException();
}
```

### Input Validation

- Session ID format validation
- Shell name whitelist
- Terminal size limits (1-500 cols, 1-200 rows)
- Input text size limit (64 KB)
- Sequence number validation

### Resource Limits

- Max concurrent sessions (32)
- Max sessions per viewer (8)
- Max pending input chunks (8192)
- Replay buffer capacity (4096)
- SSE subscription buffer (2048)

## Performance Considerations

### Memory Management

**Session State:**
- Minimal per-session overhead (~1 KB)
- Replay buffer: ~4 MB per session (worst case)
- Input queue: ~512 KB per session (worst case)

**Event Publishing:**
- Zero-copy where possible
- Shared event instances
- Channel-based distribution (no locks)

### Serialization

**JSON:**
- Source-generated serializers (NativeAOT)
- No reflection
- Minimal allocations

**MessagePack:**
- Binary protocol (~30-40% smaller)
- Faster serialization
- Lower CPU usage

### Network Optimization

**WebSocket:**
- Binary frames for MessagePack
- Text frames for JSON
- No compression (handled by reverse proxy)

**SSE:**
- Chunked transfer encoding
- Keep-alive
- Minimal overhead

### NativeAOT

**Benefits:**
- ~50ms startup time
- ~30MB memory footprint
- No JIT compilation
- Smaller binary size

**Trade-offs:**
- Longer build time
- Some reflection limitations
- Larger binary than framework-dependent

### Scalability

**Horizontal:**
- Stateless HTTP API (except sessions)
- Session affinity required for WebSocket/SSE
- Shared storage needed for multi-instance

**Vertical:**
- Async I/O (high concurrency)
- Minimal per-session overhead
- Bounded queues (predictable memory)

**Limits:**
- ~1000 concurrent sessions per instance
- ~10,000 events/sec throughput
- ~100 MB/sec network bandwidth

## Design Patterns

### Factory Pattern

```csharp
INativeProcessFactory
├── LinuxNativeProcessFactory
└── WindowsNativeProcessFactory
```

### Repository Pattern

```csharp
ITerminalSessionService
└── TerminalSessionService (in-memory)
```

### Publisher-Subscriber

```csharp
ITerminalSessionEventPublisher
├── HttpTerminalSessionEventPublisher (SSE)
├── WebSocketTerminalSessionEventPublisher
└── PipeTerminalSessionEventPublisher
```

### Strategy Pattern

```csharp
IpcProtocol (enum)
├── Json → JsonSerializer
└── MessagePack → MessagePackSerializer
```

### State Machine

```csharp
TerminalSessionState
├── Running
├── Exited
└── Closed
```

## Testing Strategy

### Unit Tests

**Project:** `AureTTY.Core.Tests`

**Coverage:** Core business logic

**Approach:**
- Mock `INativeProcessFactory`
- Test session lifecycle
- Test input queue
- Test replay buffer
- Test runtime limits

### Integration Tests

**Project:** `AureTTY.Tests`

**Coverage:** Full stack with TestHost

**Approach:**
- In-memory session service
- Real HTTP/WebSocket clients
- Real serialization
- Real event publishing

**Test Categories:**
- HTTP API tests
- SSE streaming tests
- WebSocket tests (JSON/MessagePack)
- Pipe transport tests
- Multiplexing tests
- Reconnection tests

### Test Coverage

- **Line Coverage:** 87.9%
- **Branch Coverage:** 72.2%
- **Total Tests:** 117

## Future Enhancements

### Potential Improvements

1. **Distributed Sessions**
   - Redis-backed session storage
   - Multi-instance support
   - Session migration

2. **Advanced Features**
   - Session recording/playback
   - Terminal sharing (multiple viewers)
   - File transfer support
   - X11 forwarding

3. **Performance**
   - Connection pooling
   - Event batching
   - Compression support
   - HTTP/2 and HTTP/3

4. **Monitoring**
   - Prometheus metrics
   - Health checks
   - Distributed tracing
   - Performance profiling

5. **Security**
   - OAuth2/OIDC support
   - mTLS support
   - Rate limiting
   - Audit logging

## See Also

- [Getting Started Guide](../GETTING_STARTED.md)
- [REST API Reference](../api/REST_API.md)
- [WebSocket API Reference](../api/WEBSOCKET_API.md)
- [CLAUDE.md](../CLAUDE.md) - Development guidelines

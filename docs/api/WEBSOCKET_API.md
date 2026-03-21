# AureTTY WebSocket API Reference

Complete reference for the AureTTY WebSocket API with support for JSON and MessagePack protocols.

## WebSocket Endpoints

### Multiplexed WebSocket

```
ws://localhost:17850/api/v1/viewers/{viewerId}/sessions/ws
```

Supports multiple terminal sessions over a single WebSocket connection. Sessions must be explicitly started or resumed.

## Connection

### Query Parameters

- `protocol` (optional) - Protocol selection: `json` (default), `msgpack`, `messagepack`

### Authentication

WebSocket connection must be authenticated with `hello` request as the first message:
```javascript
const ws = new WebSocket('ws://localhost:17850/api/v1/viewers/my-viewer/sessions/ws');

ws.send({
  type: 'request',
  id: 'hello-1',
  method: 'hello',
  payload: {
    token: 'your-api-key',
    protocolVersion: 1
  }
});
```

### Protocol Selection

**JSON Protocol (Default):**
```javascript
const ws = new WebSocket('ws://localhost:17850/api/v1/viewers/my-viewer/sessions/ws');
ws.binaryType = 'arraybuffer'; // Not used for JSON
```

**MessagePack Protocol (~30-40% bandwidth reduction):**
```javascript
import msgpack from 'msgpack-lite';

const ws = new WebSocket('ws://localhost:17850/api/v1/viewers/my-viewer/sessions/ws?protocol=msgpack');
ws.binaryType = 'arraybuffer';

ws.onmessage = (event) => {
  const msg = msgpack.decode(new Uint8Array(event.data));
  console.log(msg);
};

ws.send(msgpack.encode({
  type: 'request',
  id: 'hello-1',
  method: 'hello',
  payload: { token: 'your-api-key', protocolVersion: 1 }
}));
```

## Message Format

All WebSocket messages follow the IPC message format:

```typescript
interface TerminalIpcMessage {
  type: 'request' | 'response' | 'event' | 'error';
  id?: string;           // Request/response correlation ID
  method?: string;       // IPC method name
  payload?: any;         // Method-specific payload
  error?: string;        // Error message (for error type)
}
```

### Message Types

- **request** - Client request to server
- **response** - Server response to client request
- **event** - Server-initiated event (terminal output, state changes)
- **error** - Error response from server

## IPC Methods

### terminal.ping

Ping/pong for connection keepalive.

**Request:**
```json
{
  "type": "request",
  "id": "ping-1",
  "method": "terminal.ping"
}
```

**Response:**
```json
{
  "type": "response",
  "id": "ping-1",
  "method": "terminal.ping",
  "payload": {
    "success": true
  }
}
```

---

### terminal.start

Start a new terminal session.

**Request:**
```json
{
  "type": "request",
  "id": "start-1",
  "method": "terminal.start",
  "payload": {
    "viewerId": "my-viewer",
    "request": {
      "sessionId": "session-1",
      "shell": "bash",
      "columns": 80,
      "rows": 24,
      "workingDirectory": "/home/user"
    }
  }
}
```

**Payload Fields:**
- `viewerId` (string, required) - Viewer identifier
- `request` (object, required) - Session start request
  - `sessionId` (string, optional) - Session ID (auto-generated if omitted)
  - `shell` (string, required) - Shell: `bash`, `zsh`, `fish`, `sh`, `powershell`, `cmd`, `pwsh`
  - `columns` (integer, optional) - Terminal width (default: 80)
  - `rows` (integer, optional) - Terminal height (default: 24)
  - `workingDirectory` (string, optional) - Initial working directory
  - `runContext` (string, optional) - `user` or `system`
  - `userName` (string, optional) - User for credential switching
  - `domain` (string, optional) - Domain (Windows)
  - `password` (string, optional) - Password for credential switching
  - `loadUserProfile` (boolean, optional) - Load user profile (Windows)

**Response:**
```json
{
  "type": "response",
  "id": "start-1",
  "method": "terminal.start",
  "payload": {
    "sessionId": "session-1",
    "viewerId": "my-viewer",
    "state": "running",
    "processId": 12345,
    "shell": "bash",
    "columns": 80,
    "rows": 24,
    "createdAtUtc": "2026-03-20T10:00:00Z"
  }
}
```

**Error Response:**
```json
{
  "type": "error",
  "id": "start-1",
  "method": "terminal.start",
  "error": "Session with ID 'session-1' already exists"
}
```

---

### terminal.resume

Resume an existing session with optional event replay.

**Request:**
```json
{
  "type": "request",
  "id": "resume-1",
  "method": "terminal.resume",
  "payload": {
    "viewerId": "my-viewer",
    "request": {
      "sessionId": "session-1",
      "lastReceivedSequenceNumber": 42,
      "columns": 80,
      "rows": 24
    }
  }
}
```

**Payload Fields:**
- `viewerId` (string, required) - Viewer identifier
- `request` (object, required) - Resume request
  - `sessionId` (string, required) - Session ID to resume
  - `lastReceivedSequenceNumber` (integer, optional) - Last received event sequence number
  - `columns` (integer, optional) - Resize terminal width
  - `rows` (integer, optional) - Resize terminal height

**Response:**
```json
{
  "type": "response",
  "id": "resume-1",
  "method": "terminal.resume",
  "payload": {
    "sessionId": "session-1",
    "viewerId": "my-viewer",
    "state": "running",
    "processId": 12345,
    "replayedEvents": 15
  }
}
```

**Notes:**
- If `lastReceivedSequenceNumber` is provided, server replays events with `sequenceNumber > lastReceivedSequenceNumber`
- Replay buffer holds 4096 events (configurable)
- Events older than buffer capacity cannot be replayed

---

### terminal.input

Send input to a terminal session.

**Request:**
```json
{
  "type": "request",
  "id": "input-1",
  "method": "terminal.input",
  "payload": {
    "viewerId": "my-viewer",
    "request": {
      "sessionId": "session-1",
      "text": "echo hello\n",
      "sequence": 1
    }
  }
}
```

**Payload Fields:**
- `viewerId` (string, required) - Viewer identifier
- `request` (object, required) - Input request
  - `sessionId` (string, required) - Target session ID
  - `text` (string, required) - Input text (max 64 KB)
  - `sequence` (integer, required) - Sequence number for ordering

**Response:**
```json
{
  "type": "response",
  "id": "input-1",
  "method": "terminal.input",
  "payload": {
    "success": true
  }
}
```

---

### terminal.input-diagnostics

Get input queue diagnostics.

**Request:**
```json
{
  "type": "request",
  "id": "diag-1",
  "method": "terminal.input-diagnostics",
  "payload": {
    "viewerId": "my-viewer",
    "sessionId": "session-1"
  }
}
```

**Response:**
```json
{
  "type": "response",
  "id": "diag-1",
  "method": "terminal.input-diagnostics",
  "payload": {
    "sessionId": "session-1",
    "viewerId": "my-viewer",
    "state": "running",
    "pendingInputChunks": 5,
    "totalInputChunksProcessed": 142,
    "lastInputSequenceNumber": 147,
    "generatedAtUtc": "2026-03-20T10:05:00Z"
  }
}
```

---

### terminal.resize

Resize terminal window.

**Request:**
```json
{
  "type": "request",
  "id": "resize-1",
  "method": "terminal.resize",
  "payload": {
    "viewerId": "my-viewer",
    "request": {
      "sessionId": "session-1",
      "columns": 120,
      "rows": 30
    }
  }
}
```

**Response:**
```json
{
  "type": "response",
  "id": "resize-1",
  "method": "terminal.resize",
  "payload": {
    "success": true
  }
}
```

---

### terminal.signal

Send signal to terminal process.

**Request:**
```json
{
  "type": "request",
  "id": "signal-1",
  "method": "terminal.signal",
  "payload": {
    "viewerId": "my-viewer",
    "sessionId": "session-1",
    "signal": "SIGINT"
  }
}
```

**Payload Fields:**
- `viewerId` (string, required) - Viewer identifier
- `sessionId` (string, required) - Target session ID
- `signal` (string, required) - Signal: `SIGINT`, `SIGTERM`, `SIGKILL`, `SIGHUP`, `SIGQUIT`

**Response:**
```json
{
  "type": "response",
  "id": "signal-1",
  "method": "terminal.signal",
  "payload": {
    "success": true
  }
}
```

---

### terminal.close

Close a terminal session.

**Request:**
```json
{
  "type": "request",
  "id": "close-1",
  "method": "terminal.close",
  "payload": {
    "viewerId": "my-viewer",
    "sessionId": "session-1"
  }
}
```

**Response:**
```json
{
  "type": "response",
  "id": "close-1",
  "method": "terminal.close",
  "payload": {
    "success": true
  }
}
```

---

### terminal.close-viewer-sessions

Close all sessions for a viewer.

**Request:**
```json
{
  "type": "request",
  "id": "close-all-1",
  "method": "terminal.close-viewer-sessions",
  "payload": {
    "viewerId": "my-viewer"
  }
}
```

**Response:**
```json
{
  "type": "response",
  "id": "close-all-1",
  "method": "terminal.close-viewer-sessions",
  "payload": {
    "success": true
  }
}
```

---

### terminal.close-all-sessions

Close all sessions across all viewers.

**Request:**
```json
{
  "type": "request",
  "id": "close-all-2",
  "method": "terminal.close-all-sessions"
}
```

**Response:**
```json
{
  "type": "response",
  "id": "close-all-2",
  "method": "terminal.close-all-sessions",
  "payload": {
    "success": true
  }
}
```

---

## Events

Events are server-initiated messages sent via `terminal.session` method.

### Event Format

```json
{
  "type": "event",
  "method": "terminal.session",
  "payload": {
    "viewerId": "my-viewer",
    "event": {
      "sessionId": "session-1",
      "eventType": "output",
      "sequenceNumber": 1,
      "text": "hello\r\n",
      "state": "running",
      "processId": 12345,
      "exitCode": null,
      "error": null,
      "generatedAtUtc": "2026-03-20T10:00:00Z"
    }
  }
}
```

### Event Types

#### started

Session started successfully.

```json
{
  "sessionId": "session-1",
  "eventType": "started",
  "sequenceNumber": 1,
  "state": "running",
  "processId": 12345
}
```

#### attached

Session attached (resumed).

```json
{
  "sessionId": "session-1",
  "eventType": "attached",
  "sequenceNumber": 2,
  "state": "running",
  "processId": 12345
}
```

#### output

Terminal output.

```json
{
  "sessionId": "session-1",
  "eventType": "output",
  "sequenceNumber": 3,
  "text": "hello world\r\n",
  "state": "running"
}
```

#### exited

Process exited.

```json
{
  "sessionId": "session-1",
  "eventType": "exited",
  "sequenceNumber": 4,
  "state": "exited",
  "exitCode": 0
}
```

#### closed

Session closed.

```json
{
  "sessionId": "session-1",
  "eventType": "closed",
  "sequenceNumber": 5,
  "state": "closed"
}
```

#### dropped

Events were dropped due to backpressure.

```json
{
  "sessionId": "__auretty__",
  "eventType": "dropped",
  "sequenceNumber": 0,
  "error": "Dropped 15 WebSocket event(s) because subscriber is slow."
}
```

**Note:** `dropped` events indicate the client is not consuming events fast enough. Consider:
- Increasing buffer size (`--ws-subscription-buffer-capacity`)
- Optimizing client event processing
- Using MessagePack protocol for lower overhead

---

## Complete Examples

### Basic WebSocket Client (JSON)

```javascript
class AureTTYWebSocket {
  constructor(baseUrl, viewerId, apiKey) {
    this.baseUrl = baseUrl;
    this.viewerId = viewerId;
    this.apiKey = apiKey;
    this.ws = null;
    this.requestId = 0;
    this.pendingRequests = new Map();
  }

  connect() {
    return new Promise((resolve, reject) => {
      const url = `${this.baseUrl}/viewers/${this.viewerId}/sessions/ws`;
      this.ws = new WebSocket(url);

      this.ws.onopen = () => {
        console.log('Connected');
        resolve();
      };

      this.ws.onmessage = (event) => {
        const msg = JSON.parse(event.data);
        this.handleMessage(msg);
      };

      this.ws.onerror = (error) => {
        console.error('WebSocket error:', error);
        reject(error);
      };

      this.ws.onclose = () => {
        console.log('Disconnected');
      };
    });
  }

  handleMessage(msg) {
    if (msg.type === 'response' || msg.type === 'error') {
      const pending = this.pendingRequests.get(msg.id);
      if (pending) {
        this.pendingRequests.delete(msg.id);
        if (msg.type === 'response') {
          pending.resolve(msg.payload);
        } else {
          pending.reject(new Error(msg.error));
        }
      }
    } else if (msg.type === 'event') {
      this.handleEvent(msg.payload.event);
    }
  }

  handleEvent(event) {
    console.log(`[${event.sessionId}] ${event.eventType}:`, event);
  }

  request(method, payload) {
    return new Promise((resolve, reject) => {
      const id = `req-${++this.requestId}`;
      this.pendingRequests.set(id, { resolve, reject });

      this.ws.send(JSON.stringify({
        type: 'request',
        id,
        method,
        payload
      }));

      // Timeout after 30 seconds
      setTimeout(() => {
        if (this.pendingRequests.has(id)) {
          this.pendingRequests.delete(id);
          reject(new Error('Request timeout'));
        }
      }, 30000);
    });
  }

  async startSession(sessionId, shell = 'bash', columns = 80, rows = 24) {
    return this.request('terminal.start', {
      viewerId: this.viewerId,
      request: { sessionId, shell, columns, rows }
    });
  }

  async sendInput(sessionId, text, sequence) {
    return this.request('terminal.input', {
      viewerId: this.viewerId,
      request: { sessionId, text, sequence }
    });
  }

  async resizeTerminal(sessionId, columns, rows) {
    return this.request('terminal.resize', {
      viewerId: this.viewerId,
      request: { sessionId, columns, rows }
    });
  }

  async closeSession(sessionId) {
    return this.request('terminal.close', {
      viewerId: this.viewerId,
      sessionId
    });
  }

  disconnect() {
    if (this.ws) {
      this.ws.close();
    }
  }
}

// Usage
const client = new AureTTYWebSocket('ws://localhost:17850/api/v1', 'my-viewer', 'my-api-key');

await client.connect();

const session = await client.startSession('session-1', 'bash');
console.log('Session started:', session);

await client.sendInput('session-1', 'echo hello\n', 1);

// Wait for output events...

await client.closeSession('session-1');
client.disconnect();
```

### Multiplexed WebSocket Client

```javascript
class MultiplexedAureTTYClient {
  constructor(baseUrl, viewerId, apiKey, protocol = 'json') {
    this.baseUrl = baseUrl;
    this.viewerId = viewerId;
    this.apiKey = apiKey;
    this.protocol = protocol;
    this.ws = null;
    this.requestId = 0;
    this.pendingRequests = new Map();
    this.sessions = new Map();
    this.eventHandlers = new Map();
  }

  connect() {
    return new Promise((resolve, reject) => {
      const url = `${this.baseUrl}/viewers/${this.viewerId}/sessions/ws?protocol=${this.protocol}`;
      this.ws = new WebSocket(url);

      if (this.protocol === 'msgpack') {
        this.ws.binaryType = 'arraybuffer';
      }

      this.ws.onopen = () => {
        console.log('Connected (multiplexed)');
        resolve();
      };

      this.ws.onmessage = (event) => {
        const msg = this.decode(event.data);
        this.handleMessage(msg);
      };

      this.ws.onerror = reject;
      this.ws.onclose = () => console.log('Disconnected');
    });
  }

  decode(data) {
    if (this.protocol === 'msgpack') {
      return msgpack.decode(new Uint8Array(data));
    }
    return JSON.parse(data);
  }

  encode(msg) {
    if (this.protocol === 'msgpack') {
      return msgpack.encode(msg);
    }
    return JSON.stringify(msg);
  }

  handleMessage(msg) {
    if (msg.type === 'response' || msg.type === 'error') {
      const pending = this.pendingRequests.get(msg.id);
      if (pending) {
        this.pendingRequests.delete(msg.id);
        if (msg.type === 'response') {
          pending.resolve(msg.payload);
        } else {
          pending.reject(new Error(msg.error));
        }
      }
    } else if (msg.type === 'event') {
      const event = msg.payload.event;
      const handler = this.eventHandlers.get(event.sessionId);
      if (handler) {
        handler(event);
      }
    }
  }

  request(method, payload) {
    return new Promise((resolve, reject) => {
      const id = `req-${++this.requestId}`;
      this.pendingRequests.set(id, { resolve, reject });

      this.ws.send(this.encode({
        type: 'request',
        id,
        method,
        payload
      }));

      setTimeout(() => {
        if (this.pendingRequests.has(id)) {
          this.pendingRequests.delete(id);
          reject(new Error('Request timeout'));
        }
      }, 30000);
    });
  }

  async startSession(sessionId, shell = 'bash', eventHandler) {
    const result = await this.request('terminal.start', {
      viewerId: this.viewerId,
      request: { sessionId, shell }
    });

    this.sessions.set(sessionId, result);
    if (eventHandler) {
      this.eventHandlers.set(sessionId, eventHandler);
    }

    return result;
  }

  async sendInput(sessionId, text, sequence) {
    return this.request('terminal.input', {
      viewerId: this.viewerId,
      request: { sessionId, text, sequence }
    });
  }

  async closeSession(sessionId) {
    await this.request('terminal.close', {
      viewerId: this.viewerId,
      sessionId
    });

    this.sessions.delete(sessionId);
    this.eventHandlers.delete(sessionId);
  }

  disconnect() {
    if (this.ws) {
      this.ws.close();
    }
  }
}

// Usage with multiple sessions
const client = new MultiplexedAureTTYClient(
  'ws://localhost:17850/api/v1',
  'my-viewer',
  'my-api-key',
  'msgpack'
);

await client.connect();

// Start multiple sessions on the same connection
await client.startSession('session-1', 'bash', (event) => {
  console.log('[Session 1]', event.eventType, event.text);
});

await client.startSession('session-2', 'zsh', (event) => {
  console.log('[Session 2]', event.eventType, event.text);
});

// Send input to different sessions
await client.sendInput('session-1', 'ls\n', 1);
await client.sendInput('session-2', 'pwd\n', 1);

// Close sessions
await client.closeSession('session-1');
await client.closeSession('session-2');

client.disconnect();
```

### Reconnection with State Recovery

```javascript
class ResilientAureTTYClient {
  constructor(baseUrl, viewerId, apiKey) {
    this.baseUrl = baseUrl;
    this.viewerId = viewerId;
    this.apiKey = apiKey;
    this.ws = null;
    this.sessionId = null;
    this.lastSequenceNumber = 0;
    this.reconnectAttempts = 0;
    this.maxReconnectAttempts = 10;
  }

  async connect() {
    try {
      const url = `${this.baseUrl}/viewers/${this.viewerId}/sessions/ws`;
      this.ws = new WebSocket(url);

      this.ws.onopen = () => {
        console.log('Connected');
        this.reconnectAttempts = 0;

        if (this.sessionId) {
          // Resume existing session
          this.resumeSession();
        }
      };

      this.ws.onmessage = (event) => {
        const msg = JSON.parse(event.data);
        this.handleMessage(msg);
      };

      this.ws.onerror = (error) => {
        console.error('WebSocket error:', error);
      };

      this.ws.onclose = () => {
        console.log('Disconnected');
        this.reconnect();
      };
    } catch (error) {
      console.error('Connection failed:', error);
      this.reconnect();
    }
  }

  handleMessage(msg) {
    if (msg.type === 'event') {
      const event = msg.payload.event;

      // Track sequence numbers for replay
      if (event.sequenceNumber) {
        this.lastSequenceNumber = Math.max(
          this.lastSequenceNumber,
          event.sequenceNumber
        );
      }

      this.handleEvent(event);
    }
  }

  handleEvent(event) {
    console.log(`[${event.sessionId}] ${event.eventType}:`, event);
  }

  async resumeSession() {
    console.log(`Resuming session ${this.sessionId} from sequence ${this.lastSequenceNumber}`);

    this.ws.send(JSON.stringify({
      type: 'request',
      id: 'resume-1',
      method: 'terminal.resume',
      payload: {
        viewerId: this.viewerId,
        request: {
          sessionId: this.sessionId,
          lastReceivedSequenceNumber: this.lastSequenceNumber
        }
      }
    }));
  }

  reconnect() {
    if (this.reconnectAttempts >= this.maxReconnectAttempts) {
      console.error('Max reconnection attempts reached');
      return;
    }

    this.reconnectAttempts++;
    const delay = Math.min(1000 * Math.pow(2, this.reconnectAttempts), 30000);

    console.log(`Reconnecting in ${delay}ms (attempt ${this.reconnectAttempts})`);

    setTimeout(() => {
      this.connect();
    }, delay);
  }

  async startSession(sessionId, shell = 'bash') {
    this.sessionId = sessionId;
    this.lastSequenceNumber = 0;

    this.ws.send(JSON.stringify({
      type: 'request',
      id: 'start-1',
      method: 'terminal.start',
      payload: {
        viewerId: this.viewerId,
        request: { sessionId, shell }
      }
    }));
  }
}

// Usage
const client = new ResilientAureTTYClient(
  'ws://localhost:17850/api/v1',
  'my-viewer',
  'my-api-key'
);

await client.connect();
await client.startSession('resilient-session', 'bash');

// Connection will automatically reconnect and resume on disconnect
```

## Best Practices

1. **Use Multiplexed Endpoint** - Single connection for all sessions
2. **Enable MessagePack** - 30-40% bandwidth reduction
3. **Track Sequence Numbers** - For reliable reconnection
4. **Implement Exponential Backoff** - For reconnection attempts
5. **Handle Dropped Events** - Monitor and respond to backpressure
6. **Use Request IDs** - For request/response correlation
7. **Set Timeouts** - Don't wait forever for responses
8. **Clean Up Sessions** - Close sessions when done
9. **Handle Connection Loss** - Implement reconnection logic
10. **Validate Messages** - Check message format before processing

## Performance Tips

1. **MessagePack Protocol** - Use for high-throughput scenarios
2. **Batch Input** - Send multiple inputs in quick succession
3. **Buffer Events** - Don't process every event immediately
4. **Use Binary Type** - Set `ws.binaryType = 'arraybuffer'` for MessagePack
5. **Compress Large Payloads** - Consider compression for large text
6. **Monitor Latency** - Track round-trip time for requests
7. **Limit Concurrent Sessions** - Don't exceed viewer limits
8. **Use Connection Pooling** - Reuse connections when possible
9. **Implement Backpressure** - Slow down if events are dropped
10. **Profile Client Code** - Optimize event processing

## Troubleshooting

### Connection Refused
- Check if service is running
- Verify URL and port
- Check firewall rules

### Authentication Failed
- Verify API key is correct
- Check if query parameter auth is enabled
- Ensure header name is correct (`X-AureTTY-Key`)

### Events Dropped
- Increase buffer size: `--ws-subscription-buffer-capacity`
- Optimize client event processing
- Use MessagePack protocol
- Reduce event frequency

### Session Not Found
- Session may have been closed
- Check viewer ID matches
- Verify session ID is correct

### Input Not Processed
- Check input queue: use `terminal.input-diagnostics`
- Verify sequence numbers are increasing
- Check session state (must be running)

## See Also

- [REST API Reference](REST_API.md)
- [WebSocket Guide](../guides/WEBSOCKET_GUIDE.md)
- [Getting Started](../GETTING_STARTED.md)
- [Code Examples](../examples/)

# WebSocket Guide

Complete guide to using AureTTY's WebSocket transport with advanced features.

## Overview

AureTTY provides one WebSocket endpoint:

1. **Multiplexed WebSocket** (`/api/v1/viewers/{viewerId}/sessions/ws`) - Multiple sessions per connection

The endpoint supports:
- **JSON Protocol** (default) - Human-readable, easy debugging
- **MessagePack Protocol** - Binary, ~30-40% bandwidth reduction
- **Automatic Reconnection** - Resume with state recovery
- **Bidirectional Communication** - Full-duplex, low latency

## Quick Start

### Basic Connection (JSON)

```javascript
const ws = new WebSocket('ws://localhost:17850/api/v1/viewers/my-viewer/sessions/ws?api_key=your-key');

ws.onopen = () => {
  console.log('Connected');

  // Start a session
  ws.send(JSON.stringify({
    type: 'request',
    id: 'req-1',
    method: 'terminal.start',
    payload: {
      viewerId: 'my-viewer',
      request: {
        sessionId: 'session-1',
        shell: 'bash'
      }
    }
  }));
};

ws.onmessage = (event) => {
  const msg = JSON.parse(event.data);
  console.log('Received:', msg);
};

ws.onerror = (error) => {
  console.error('Error:', error);
};

ws.onclose = () => {
  console.log('Disconnected');
};
```

### MessagePack Protocol

```javascript
import msgpack from 'msgpack-lite';

const ws = new WebSocket('ws://localhost:17850/api/v1/viewers/my-viewer/sessions/ws?protocol=msgpack&api_key=your-key');
ws.binaryType = 'arraybuffer';

ws.onopen = () => {
  const message = {
    type: 'request',
    id: 'req-1',
    method: 'terminal.start',
    payload: {
      viewerId: 'my-viewer',
      request: { sessionId: 'session-1', shell: 'bash' }
    }
  };

  ws.send(msgpack.encode(message));
};

ws.onmessage = (event) => {
  const msg = msgpack.decode(new Uint8Array(event.data));
  console.log('Received:', msg);
};
```

## Protocol Selection

### JSON Protocol

**Advantages:**
- Human-readable
- Easy debugging
- Browser DevTools support
- No additional dependencies

**Disadvantages:**
- Larger message size
- Slower serialization
- Higher CPU usage

**Use When:**
- Development and debugging
- Low-traffic applications
- Browser compatibility is critical

### MessagePack Protocol

**Advantages:**
- ~30-40% smaller messages
- Faster serialization
- Lower CPU usage
- Better for high-throughput

**Disadvantages:**
- Binary format (harder to debug)
- Requires MessagePack library
- Not human-readable

**Use When:**
- Production deployments
- High-traffic applications
- Bandwidth is limited
- Performance is critical

### Comparison

| Feature | JSON | MessagePack |
|---------|------|-------------|
| Message Size | 100% | ~60-70% |
| Serialization Speed | Baseline | ~2x faster |
| CPU Usage | Baseline | ~50% less |
| Debugging | Easy | Harder |
| Browser Support | Native | Requires library |

## Session Multiplexing

### Why Multiplexing?

**Without Multiplexing:**
- One WebSocket per terminal session
- High connection overhead
- More server resources
- Complex client management

**With Multiplexing:**
- One WebSocket for all sessions
- Lower connection overhead
- Fewer server resources
- Simpler client code

### Using Multiplexed Endpoint

```javascript
// Connect to multiplexed endpoint
const ws = new WebSocket('ws://localhost:17850/api/v1/viewers/my-viewer/sessions/ws?protocol=msgpack&api_key=your-key');
ws.binaryType = 'arraybuffer';

const sessions = new Map();
let requestId = 0;

ws.onopen = async () => {
  // Start multiple sessions
  await startSession('session-1', 'bash');
  await startSession('session-2', 'zsh');
  await startSession('session-3', 'fish');
};

ws.onmessage = (event) => {
  const msg = msgpack.decode(new Uint8Array(event.data));

  if (msg.type === 'event') {
    const sessionId = msg.payload.event.sessionId;
    const handler = sessions.get(sessionId);

    if (handler) {
      handler(msg.payload.event);
    }
  }
};

function startSession(sessionId, shell) {
  return new Promise((resolve, reject) => {
    const id = `req-${++requestId}`;

    // Register event handler
    sessions.set(sessionId, (event) => {
      if (event.eventType === 'output') {
        console.log(`[${sessionId}] ${event.text}`);
      }
    });

    // Send start request
    ws.send(msgpack.encode({
      type: 'request',
      id,
      method: 'terminal.start',
      payload: {
        viewerId: 'my-viewer',
        request: { sessionId, shell }
      }
    }));

    // Wait for response
    const handler = (event) => {
      const msg = msgpack.decode(new Uint8Array(event.data));
      if (msg.id === id) {
        ws.removeEventListener('message', handler);
        if (msg.type === 'response') {
          resolve(msg.payload);
        } else {
          reject(new Error(msg.error));
        }
      }
    };

    ws.addEventListener('message', handler);
  });
}
```

## Reconnection and State Recovery

### Tracking Sequence Numbers

```javascript
class ResilientTerminal {
  constructor(url, viewerId, sessionId) {
    this.url = url;
    this.viewerId = viewerId;
    this.sessionId = sessionId;
    this.lastSequenceNumber = 0;
    this.ws = null;
    this.reconnectAttempts = 0;
  }

  connect() {
    this.ws = new WebSocket(this.url);

    this.ws.onopen = () => {
      console.log('Connected');
      this.reconnectAttempts = 0;

      if (this.lastSequenceNumber > 0) {
        // Resume existing session
        this.resume();
      } else {
        // Start new session
        this.start();
      }
    };

    this.ws.onmessage = (event) => {
      const msg = JSON.parse(event.data);

      if (msg.type === 'event') {
        const evt = msg.payload.event;

        // Track sequence numbers
        if (evt.sequenceNumber) {
          this.lastSequenceNumber = Math.max(
            this.lastSequenceNumber,
            evt.sequenceNumber
          );
        }

        this.handleEvent(evt);
      }
    };

    this.ws.onclose = () => {
      console.log('Disconnected');
      this.reconnect();
    };

    this.ws.onerror = (error) => {
      console.error('Error:', error);
    };
  }

  start() {
    this.ws.send(JSON.stringify({
      type: 'request',
      id: 'start',
      method: 'terminal.start',
      payload: {
        viewerId: this.viewerId,
        request: {
          sessionId: this.sessionId,
          shell: 'bash'
        }
      }
    }));
  }

  resume() {
    console.log(`Resuming from sequence ${this.lastSequenceNumber}`);

    this.ws.send(JSON.stringify({
      type: 'request',
      id: 'resume',
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
    if (this.reconnectAttempts >= 10) {
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

  handleEvent(event) {
    switch (event.eventType) {
      case 'started':
        console.log('Session started');
        break;
      case 'attached':
        console.log('Session resumed');
        break;
      case 'output':
        process.stdout.write(event.text);
        break;
      case 'exited':
        console.log(`Process exited with code ${event.exitCode}`);
        break;
      case 'closed':
        console.log('Session closed');
        break;
    }
  }

  sendInput(text, sequence) {
    this.ws.send(JSON.stringify({
      type: 'request',
      id: `input-${sequence}`,
      method: 'terminal.input',
      payload: {
        viewerId: this.viewerId,
        request: {
          sessionId: this.sessionId,
          text,
          sequence
        }
      }
    }));
  }

  close() {
    this.ws.send(JSON.stringify({
      type: 'request',
      id: 'close',
      method: 'terminal.close',
      payload: {
        viewerId: this.viewerId,
        sessionId: this.sessionId
      }
    }));
  }
}

// Usage
const terminal = new ResilientTerminal(
  'ws://localhost:17850/api/v1/viewers/my-viewer/sessions/ws?api_key=key',
  'my-viewer',
  'resilient-session'
);

terminal.connect();

// Send input
let seq = 1;
terminal.sendInput('echo hello\n', seq++);
terminal.sendInput('ls -la\n', seq++);

// Connection will automatically reconnect and resume on disconnect
```

### Replay Buffer

The server maintains a circular buffer of the last 4096 events per session. When resuming:

1. Client sends `lastReceivedSequenceNumber`
2. Server replays all events with `sequenceNumber > lastReceivedSequenceNumber`
3. Client receives missed events in order
4. Normal event flow resumes

**Example:**

```
Client disconnects at sequence 100
Server continues to sequence 150
Client reconnects with lastReceivedSequenceNumber=100
Server replays events 101-150
Client is now caught up
```

**Limitations:**

- Buffer holds 4096 events (configurable)
- Events older than buffer capacity cannot be replayed
- Client receives error if sequence is too old

## Advanced Patterns

### Request-Response Pattern

```javascript
class WebSocketClient {
  constructor(url) {
    this.url = url;
    this.ws = null;
    this.requestId = 0;
    this.pendingRequests = new Map();
  }

  connect() {
    return new Promise((resolve, reject) => {
      this.ws = new WebSocket(this.url);

      this.ws.onopen = () => resolve();
      this.ws.onerror = reject;

      this.ws.onmessage = (event) => {
        const msg = JSON.parse(event.data);

        if (msg.type === 'response' || msg.type === 'error') {
          const pending = this.pendingRequests.get(msg.id);

          if (pending) {
            this.pendingRequests.delete(msg.id);
            clearTimeout(pending.timeout);

            if (msg.type === 'response') {
              pending.resolve(msg.payload);
            } else {
              pending.reject(new Error(msg.error));
            }
          }
        } else if (msg.type === 'event') {
          this.handleEvent(msg.payload.event);
        }
      };
    });
  }

  request(method, payload, timeoutMs = 30000) {
    return new Promise((resolve, reject) => {
      const id = `req-${++this.requestId}`;

      const timeout = setTimeout(() => {
        this.pendingRequests.delete(id);
        reject(new Error('Request timeout'));
      }, timeoutMs);

      this.pendingRequests.set(id, { resolve, reject, timeout });

      this.ws.send(JSON.stringify({
        type: 'request',
        id,
        method,
        payload
      }));
    });
  }

  async startSession(viewerId, sessionId, shell) {
    return this.request('terminal.start', {
      viewerId,
      request: { sessionId, shell }
    });
  }

  async sendInput(viewerId, sessionId, text, sequence) {
    return this.request('terminal.input', {
      viewerId,
      request: { sessionId, text, sequence }
    });
  }

  async closeSession(viewerId, sessionId) {
    return this.request('terminal.close', {
      viewerId,
      sessionId
    });
  }

  handleEvent(event) {
    // Override in subclass
    console.log('Event:', event);
  }
}

// Usage
const client = new WebSocketClient('ws://localhost:17850/api/v1/viewers/v1/sessions/ws?api_key=key');

await client.connect();

const session = await client.startSession('v1', 's1', 'bash');
console.log('Session started:', session);

await client.sendInput('v1', 's1', 'echo hello\n', 1);

await client.closeSession('v1', 's1');
```

### Event Buffering

```javascript
class BufferedTerminal {
  constructor(ws, sessionId) {
    this.ws = ws;
    this.sessionId = sessionId;
    this.buffer = [];
    this.flushInterval = 100; // ms
    this.maxBufferSize = 1000;

    this.startFlushing();
  }

  handleEvent(event) {
    if (event.sessionId !== this.sessionId) return;

    if (event.eventType === 'output') {
      this.buffer.push(event.text);

      if (this.buffer.length >= this.maxBufferSize) {
        this.flush();
      }
    }
  }

  startFlushing() {
    this.flushTimer = setInterval(() => {
      this.flush();
    }, this.flushInterval);
  }

  flush() {
    if (this.buffer.length === 0) return;

    const text = this.buffer.join('');
    this.buffer = [];

    // Process buffered output
    this.processOutput(text);
  }

  processOutput(text) {
    // Override in subclass
    process.stdout.write(text);
  }

  stop() {
    clearInterval(this.flushTimer);
    this.flush();
  }
}
```

### Ping/Pong Keepalive

```javascript
class KeepaliveWebSocket {
  constructor(url, pingInterval = 30000) {
    this.url = url;
    this.pingInterval = pingInterval;
    this.ws = null;
    this.pingTimer = null;
    this.pongReceived = true;
  }

  connect() {
    this.ws = new WebSocket(this.url);

    this.ws.onopen = () => {
      console.log('Connected');
      this.startPinging();
    };

    this.ws.onmessage = (event) => {
      const msg = JSON.parse(event.data);

      if (msg.method === 'terminal.ping') {
        this.pongReceived = true;
      }

      this.handleMessage(msg);
    };

    this.ws.onclose = () => {
      console.log('Disconnected');
      this.stopPinging();
    };
  }

  startPinging() {
    this.pingTimer = setInterval(() => {
      if (!this.pongReceived) {
        console.warn('Pong not received, connection may be dead');
        this.ws.close();
        return;
      }

      this.pongReceived = false;

      this.ws.send(JSON.stringify({
        type: 'request',
        id: `ping-${Date.now()}`,
        method: 'terminal.ping'
      }));
    }, this.pingInterval);
  }

  stopPinging() {
    if (this.pingTimer) {
      clearInterval(this.pingTimer);
      this.pingTimer = null;
    }
  }

  handleMessage(msg) {
    // Override in subclass
  }
}
```

## Performance Optimization

### 1. Use MessagePack

```javascript
// ~30-40% bandwidth reduction
const ws = new WebSocket('ws://localhost:17850/api/v1/viewers/v1/sessions/ws?protocol=msgpack');
```

### 2. Enable Multiplexing

```javascript
// One connection for all sessions
const ws = new WebSocket('ws://localhost:17850/api/v1/viewers/v1/sessions/ws');
```

### 3. Buffer Events

```javascript
// Don't process every event immediately
const buffer = [];
const flushInterval = 100; // ms

setInterval(() => {
  if (buffer.length > 0) {
    processEvents(buffer);
    buffer.length = 0;
  }
}, flushInterval);
```

### 4. Batch Input

```javascript
// Send multiple inputs quickly
const inputs = ['ls\n', 'pwd\n', 'whoami\n'];
let seq = 1;

for (const text of inputs) {
  sendInput(text, seq++);
}
```

### 5. Use Binary Type

```javascript
// For MessagePack
ws.binaryType = 'arraybuffer';
```

### 6. Implement Backpressure

```javascript
ws.onmessage = (event) => {
  const msg = decode(event.data);

  if (msg.payload?.event?.eventType === 'dropped') {
    console.warn('Events dropped, slowing down');
    // Implement backpressure logic
  }
};
```

## Error Handling

### Connection Errors

```javascript
ws.onerror = (error) => {
  console.error('WebSocket error:', error);

  // Implement reconnection logic
  setTimeout(() => reconnect(), 1000);
};
```

### Request Errors

```javascript
ws.onmessage = (event) => {
  const msg = JSON.parse(event.data);

  if (msg.type === 'error') {
    console.error(`Request ${msg.id} failed: ${msg.error}`);

    // Handle specific errors
    if (msg.error.includes('Session not found')) {
      // Session was closed, restart
      startNewSession();
    }
  }
};
```

### Event Errors

```javascript
ws.onmessage = (event) => {
  const msg = JSON.parse(event.data);

  if (msg.type === 'event') {
    const evt = msg.payload.event;

    if (evt.eventType === 'dropped') {
      console.warn('Events dropped:', evt.error);
      // Increase buffer size or slow down
    }

    if (evt.error) {
      console.error('Event error:', evt.error);
    }
  }
};
```

## Best Practices

1. **Always implement reconnection** - Networks are unreliable
2. **Track sequence numbers** - For reliable state recovery
3. **Use MessagePack in production** - Better performance
4. **Enable multiplexing** - Lower overhead
5. **Implement timeouts** - Don't wait forever
6. **Handle backpressure** - Watch for dropped events
7. **Buffer events** - Don't process every event immediately
8. **Use ping/pong** - Detect dead connections
9. **Clean up resources** - Close sessions when done
10. **Log errors** - For debugging and monitoring

## Debugging

### Enable Verbose Logging

```javascript
const ws = new WebSocket(url);

ws.onopen = () => console.log('[WS] Connected');
ws.onclose = () => console.log('[WS] Disconnected');
ws.onerror = (e) => console.error('[WS] Error:', e);

ws.onmessage = (event) => {
  const msg = JSON.parse(event.data);
  console.log('[WS] Received:', JSON.stringify(msg, null, 2));
};

// Log outgoing messages
const originalSend = ws.send.bind(ws);
ws.send = (data) => {
  console.log('[WS] Sending:', data);
  originalSend(data);
};
```

### Browser DevTools

1. Open DevTools (F12)
2. Go to Network tab
3. Filter by WS
4. Click on WebSocket connection
5. View Messages tab

### Message Inspector

```javascript
function inspectMessage(msg) {
  console.group(`Message: ${msg.type}`);
  console.log('ID:', msg.id);
  console.log('Method:', msg.method);
  console.log('Payload:', msg.payload);
  console.log('Error:', msg.error);
  console.groupEnd();
}
```

## See Also

- [WebSocket API Reference](../api/WEBSOCKET_API.md)
- [REST API Reference](../api/REST_API.md)
- [Getting Started Guide](../GETTING_STARTED.md)
- [Code Examples](../examples/)

# JavaScript/TypeScript Client Example

Complete JavaScript/TypeScript client for AureTTY with support for HTTP REST API and WebSocket.

## Installation

```bash
npm install axios ws msgpack-lite
# or
yarn add axios ws msgpack-lite
```

## TypeScript Types

```typescript
// types.ts

export interface TerminalSessionHandle {
  sessionId: string;
  viewerId: string;
  state: 'running' | 'exited' | 'closed';
  processId?: number;
  shell: string;
  columns: number;
  rows: number;
  exitCode?: number;
  createdAtUtc: string;
  lastActivityUtc?: string;
}

export interface TerminalSessionEvent {
  sessionId: string;
  eventType: 'started' | 'attached' | 'output' | 'exited' | 'closed' | 'dropped';
  sequenceNumber: number;
  text?: string;
  state: string;
  processId?: number;
  exitCode?: number;
  error?: string;
  generatedAtUtc: string;
}

export interface TerminalIpcMessage {
  type: 'request' | 'response' | 'event' | 'error';
  id?: string;
  method?: string;
  payload?: any;
  error?: string;
}

export interface CreateSessionRequest {
  sessionId?: string;
  shell: string;
  runContext?: 'user' | 'system';
  userName?: string;
  domain?: string;
  password?: string;
  loadUserProfile?: boolean;
  workingDirectory?: string;
  columns?: number;
  rows?: number;
}

export interface AttachSessionRequest {
  lastReceivedSequenceNumber?: number;
  columns?: number;
  rows?: number;
}

export interface SendInputRequest {
  text: string;
  sequence: number;
}

export interface ResizeTerminalRequest {
  columns: number;
  rows: number;
}

export interface SendSignalRequest {
  signal: 'SIGINT' | 'SIGTERM' | 'SIGKILL' | 'SIGHUP' | 'SIGQUIT';
}
```

## HTTP REST Client

```typescript
// http-client.ts

import axios, { AxiosInstance } from 'axios';
import {
  TerminalSessionHandle,
  CreateSessionRequest,
  AttachSessionRequest,
  SendInputRequest,
  ResizeTerminalRequest,
  SendSignalRequest,
  TerminalSessionEvent
} from './types';

export class AureTTYHttpClient {
  private client: AxiosInstance;

  constructor(baseUrl: string, apiKey: string) {
    this.client = axios.create({
      baseURL: baseUrl,
      headers: {
        'X-AureTTY-Key': apiKey,
        'Content-Type': 'application/json'
      }
    });
  }

  async health(): Promise<any> {
    const response = await this.client.get('/health');
    return response.data;
  }

  async listSessions(): Promise<TerminalSessionHandle[]> {
    const response = await this.client.get('/sessions');
    return response.data;
  }

  async listViewerSessions(viewerId: string): Promise<TerminalSessionHandle[]> {
    const response = await this.client.get(`/viewers/${viewerId}/sessions`);
    return response.data;
  }

  async createSession(
    viewerId: string,
    request: CreateSessionRequest
  ): Promise<TerminalSessionHandle> {
    const response = await this.client.post(
      `/viewers/${viewerId}/sessions`,
      request
    );
    return response.data;
  }

  async getSession(
    viewerId: string,
    sessionId: string
  ): Promise<TerminalSessionHandle> {
    const response = await this.client.get(
      `/viewers/${viewerId}/sessions/${sessionId}`
    );
    return response.data;
  }

  async attachSession(
    viewerId: string,
    sessionId: string,
    request: AttachSessionRequest
  ): Promise<TerminalSessionHandle> {
    const response = await this.client.post(
      `/viewers/${viewerId}/sessions/${sessionId}/attachments`,
      request
    );
    return response.data;
  }

  async sendInput(
    viewerId: string,
    sessionId: string,
    request: SendInputRequest
  ): Promise<void> {
    await this.client.post(
      `/viewers/${viewerId}/sessions/${sessionId}/inputs`,
      request
    );
  }

  async getInputDiagnostics(
    viewerId: string,
    sessionId: string
  ): Promise<any> {
    const response = await this.client.get(
      `/viewers/${viewerId}/sessions/${sessionId}/input-diagnostics`
    );
    return response.data;
  }

  async resizeTerminal(
    viewerId: string,
    sessionId: string,
    request: ResizeTerminalRequest
  ): Promise<void> {
    await this.client.put(
      `/viewers/${viewerId}/sessions/${sessionId}/terminal-size`,
      request
    );
  }

  async sendSignal(
    viewerId: string,
    sessionId: string,
    request: SendSignalRequest
  ): Promise<void> {
    await this.client.post(
      `/viewers/${viewerId}/sessions/${sessionId}/signals`,
      request
    );
  }

  async closeSession(viewerId: string, sessionId: string): Promise<void> {
    await this.client.delete(`/viewers/${viewerId}/sessions/${sessionId}`);
  }

  async closeViewerSessions(viewerId: string): Promise<void> {
    await this.client.delete(`/viewers/${viewerId}/sessions`);
  }

  async closeAllSessions(): Promise<void> {
    await this.client.delete('/sessions');
  }

  async *streamEvents(viewerId: string): AsyncGenerator<TerminalSessionEvent> {
    const response = await this.client.get(
      `/viewers/${viewerId}/events`,
      { responseType: 'stream' }
    );

    for await (const chunk of response.data) {
      const lines = chunk.toString().split('\n');
      for (const line of lines) {
        if (line.startsWith('data: ')) {
          yield JSON.parse(line.substring(6));
        }
      }
    }
  }
}

// Example usage
async function example() {
  const client = new AureTTYHttpClient(
    'http://localhost:17850/api/v1',
    'your-api-key'
  );

  // Check health
  const health = await client.health();
  console.log('Health:', health);

  // Create session
  const session = await client.createSession('my-viewer', {
    shell: 'bash',
    columns: 80,
    rows: 24
  });
  console.log('Session created:', session.sessionId);

  // Send input
  await client.sendInput('my-viewer', session.sessionId, {
    text: 'echo hello\n',
    sequence: 1
  });

  // Close session
  await client.closeSession('my-viewer', session.sessionId);
}
```

## WebSocket Client

```typescript
// websocket-client.ts

import WebSocket from 'ws';
import msgpack from 'msgpack-lite';
import { EventEmitter } from 'events';
import {
  TerminalIpcMessage,
  TerminalSessionHandle,
  TerminalSessionEvent
} from './types';

export type Protocol = 'json' | 'msgpack';

export interface WebSocketClientOptions {
  protocol?: Protocol;
  reconnect?: boolean;
  maxReconnectAttempts?: number;
  reconnectDelay?: number;
}

export class AureTTYWebSocketClient extends EventEmitter {
  private ws: WebSocket | null = null;
  private url: string;
  private viewerId: string;
  private apiKey: string;
  private options: Required<WebSocketClientOptions>;
  private requestId = 0;
  private pendingRequests = new Map<string, {
    resolve: (value: any) => void;
    reject: (error: Error) => void;
    timeout: NodeJS.Timeout;
  }>();
  private reconnectAttempts = 0;
  private reconnectTimer: NodeJS.Timeout | null = null;

  constructor(
    url: string,
    viewerId: string,
    apiKey: string,
    options: WebSocketClientOptions = {}
  ) {
    super();
    this.url = url;
    this.viewerId = viewerId;
    this.apiKey = apiKey;
    this.options = {
      protocol: options.protocol || 'json',
      reconnect: options.reconnect !== false,
      maxReconnectAttempts: options.maxReconnectAttempts || 10,
      reconnectDelay: options.reconnectDelay || 1000
    };
  }

  connect(): Promise<void> {
    return new Promise((resolve, reject) => {
      const wsUrl = `${this.url}/viewers/${this.viewerId}/ws?protocol=${this.options.protocol}&api_key=${this.apiKey}`;

      this.ws = new WebSocket(wsUrl);

      this.ws.on('open', () => {
        this.reconnectAttempts = 0;
        this.emit('connected');
        resolve();
      });

      this.ws.on('message', (data: Buffer) => {
        this.handleMessage(data);
      });

      this.ws.on('error', (error) => {
        this.emit('error', error);
        reject(error);
      });

      this.ws.on('close', () => {
        this.emit('disconnected');
        this.handleDisconnect();
      });
    });
  }

  private handleMessage(data: Buffer): void {
    const msg = this.decode(data);

    if (msg.type === 'response' || msg.type === 'error') {
      const pending = this.pendingRequests.get(msg.id!);
      if (pending) {
        this.pendingRequests.delete(msg.id!);
        clearTimeout(pending.timeout);

        if (msg.type === 'response') {
          pending.resolve(msg.payload);
        } else {
          pending.reject(new Error(msg.error));
        }
      }
    } else if (msg.type === 'event') {
      const event = msg.payload.event as TerminalSessionEvent;
      this.emit('event', event);
      this.emit(`event:${event.sessionId}`, event);
    }
  }

  private handleDisconnect(): void {
    // Clear pending requests
    for (const [id, pending] of this.pendingRequests) {
      clearTimeout(pending.timeout);
      pending.reject(new Error('Connection closed'));
    }
    this.pendingRequests.clear();

    // Attempt reconnection
    if (this.options.reconnect && this.reconnectAttempts < this.options.maxReconnectAttempts) {
      this.reconnectAttempts++;
      const delay = this.options.reconnectDelay * Math.pow(2, this.reconnectAttempts - 1);

      this.emit('reconnecting', this.reconnectAttempts, delay);

      this.reconnectTimer = setTimeout(() => {
        this.connect().catch(() => {
          // Will retry again if needed
        });
      }, delay);
    } else if (this.reconnectAttempts >= this.options.maxReconnectAttempts) {
      this.emit('reconnect-failed');
    }
  }

  private encode(msg: TerminalIpcMessage): Buffer {
    if (this.options.protocol === 'msgpack') {
      return msgpack.encode(msg);
    }
    return Buffer.from(JSON.stringify(msg));
  }

  private decode(data: Buffer): TerminalIpcMessage {
    if (this.options.protocol === 'msgpack') {
      return msgpack.decode(data);
    }
    return JSON.parse(data.toString());
  }

  private request(
    method: string,
    payload?: any,
    timeout = 30000
  ): Promise<any> {
    return new Promise((resolve, reject) => {
      if (!this.ws || this.ws.readyState !== WebSocket.OPEN) {
        reject(new Error('WebSocket not connected'));
        return;
      }

      const id = `req-${++this.requestId}`;

      const timeoutHandle = setTimeout(() => {
        this.pendingRequests.delete(id);
        reject(new Error(`Request timeout: ${method}`));
      }, timeout);

      this.pendingRequests.set(id, { resolve, reject, timeout: timeoutHandle });

      const message: TerminalIpcMessage = {
        type: 'request',
        id,
        method,
        payload
      };

      this.ws.send(this.encode(message));
    });
  }

  async ping(): Promise<void> {
    await this.request('terminal.ping');
  }

  async startSession(
    sessionId: string,
    shell: string = 'bash',
    columns: number = 80,
    rows: number = 24
  ): Promise<TerminalSessionHandle> {
    return this.request('terminal.start', {
      viewerId: this.viewerId,
      request: { sessionId, shell, columns, rows }
    });
  }

  async resumeSession(
    sessionId: string,
    lastReceivedSequenceNumber?: number
  ): Promise<TerminalSessionHandle> {
    return this.request('terminal.resume', {
      viewerId: this.viewerId,
      request: { sessionId, lastReceivedSequenceNumber }
    });
  }

  async sendInput(
    sessionId: string,
    text: string,
    sequence: number
  ): Promise<void> {
    await this.request('terminal.input', {
      viewerId: this.viewerId,
      request: { sessionId, text, sequence }
    });
  }

  async resizeTerminal(
    sessionId: string,
    columns: number,
    rows: number
  ): Promise<void> {
    await this.request('terminal.resize', {
      viewerId: this.viewerId,
      request: { sessionId, columns, rows }
    });
  }

  async sendSignal(
    sessionId: string,
    signal: string
  ): Promise<void> {
    await this.request('terminal.signal', {
      viewerId: this.viewerId,
      sessionId,
      signal
    });
  }

  async closeSession(sessionId: string): Promise<void> {
    await this.request('terminal.close', {
      viewerId: this.viewerId,
      sessionId
    });
  }

  disconnect(): void {
    if (this.reconnectTimer) {
      clearTimeout(this.reconnectTimer);
      this.reconnectTimer = null;
    }

    if (this.ws) {
      this.ws.close();
      this.ws = null;
    }
  }
}

// Example usage
async function example() {
  const client = new AureTTYWebSocketClient(
    'ws://localhost:17850/api/v1',
    'my-viewer',
    'your-api-key',
    { protocol: 'msgpack', reconnect: true }
  );

  // Event handlers
  client.on('connected', () => {
    console.log('Connected');
  });

  client.on('disconnected', () => {
    console.log('Disconnected');
  });

  client.on('event', (event: TerminalSessionEvent) => {
    if (event.eventType === 'output') {
      process.stdout.write(event.text!);
    }
  });

  // Connect
  await client.connect();

  // Start session
  const session = await client.startSession('session-1', 'bash');
  console.log('Session started:', session.sessionId);

  // Send input
  await client.sendInput('session-1', 'echo hello\n', 1);
  await client.sendInput('session-1', 'ls -la\n', 2);

  // Wait for output
  await new Promise(resolve => setTimeout(resolve, 2000));

  // Close
  await client.closeSession('session-1');
  client.disconnect();
}
```

## Multiplexed WebSocket Client

```typescript
// multiplexed-client.ts

import WebSocket from 'ws';
import msgpack from 'msgpack-lite';
import { EventEmitter } from 'events';
import { TerminalSessionHandle, TerminalSessionEvent } from './types';

export class MultiplexedAureTTYClient extends EventEmitter {
  private ws: WebSocket | null = null;
  private url: string;
  private viewerId: string;
  private apiKey: string;
  private protocol: 'json' | 'msgpack';
  private requestId = 0;
  private pendingRequests = new Map();
  private sessions = new Map<string, TerminalSessionHandle>();

  constructor(
    url: string,
    viewerId: string,
    apiKey: string,
    protocol: 'json' | 'msgpack' = 'msgpack'
  ) {
    super();
    this.url = url;
    this.viewerId = viewerId;
    this.apiKey = apiKey;
    this.protocol = protocol;
  }

  async connect(): Promise<void> {
    return new Promise((resolve, reject) => {
      const wsUrl = `${this.url}/viewers/${this.viewerId}/sessions/ws?protocol=${this.protocol}&api_key=${this.apiKey}`;

      this.ws = new WebSocket(wsUrl);

      this.ws.on('open', () => {
        this.emit('connected');
        resolve();
      });

      this.ws.on('message', (data: Buffer) => {
        const msg = this.decode(data);

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
          const event = msg.payload.event as TerminalSessionEvent;
          this.emit('event', event);
          this.emit(`session:${event.sessionId}`, event);
        }
      });

      this.ws.on('error', (error) => {
        this.emit('error', error);
        reject(error);
      });

      this.ws.on('close', () => {
        this.emit('disconnected');
      });
    });
  }

  private encode(msg: any): Buffer {
    if (this.protocol === 'msgpack') {
      return msgpack.encode(msg);
    }
    return Buffer.from(JSON.stringify(msg));
  }

  private decode(data: Buffer): any {
    if (this.protocol === 'msgpack') {
      return msgpack.decode(data);
    }
    return JSON.parse(data.toString());
  }

  private request(method: string, payload?: any): Promise<any> {
    return new Promise((resolve, reject) => {
      const id = `req-${++this.requestId}`;

      const timeout = setTimeout(() => {
        this.pendingRequests.delete(id);
        reject(new Error(`Request timeout: ${method}`));
      }, 30000);

      this.pendingRequests.set(id, { resolve, reject, timeout });

      this.ws!.send(this.encode({
        type: 'request',
        id,
        method,
        payload
      }));
    });
  }

  async startSession(
    sessionId: string,
    shell: string = 'bash'
  ): Promise<TerminalSessionHandle> {
    const handle = await this.request('terminal.start', {
      viewerId: this.viewerId,
      request: { sessionId, shell }
    });

    this.sessions.set(sessionId, handle);
    return handle;
  }

  async sendInput(
    sessionId: string,
    text: string,
    sequence: number
  ): Promise<void> {
    await this.request('terminal.input', {
      viewerId: this.viewerId,
      request: { sessionId, text, sequence }
    });
  }

  async closeSession(sessionId: string): Promise<void> {
    await this.request('terminal.close', {
      viewerId: this.viewerId,
      sessionId
    });

    this.sessions.delete(sessionId);
  }

  disconnect(): void {
    if (this.ws) {
      this.ws.close();
      this.ws = null;
    }
  }
}

// Example usage with multiple sessions
async function multiplexExample() {
  const client = new MultiplexedAureTTYClient(
    'ws://localhost:17850/api/v1',
    'my-viewer',
    'your-api-key',
    'msgpack'
  );

  // Event handlers for different sessions
  client.on('session:session-1', (event: TerminalSessionEvent) => {
    if (event.eventType === 'output') {
      console.log('[Session 1]', event.text);
    }
  });

  client.on('session:session-2', (event: TerminalSessionEvent) => {
    if (event.eventType === 'output') {
      console.log('[Session 2]', event.text);
    }
  });

  await client.connect();

  // Start multiple sessions on the same connection
  await client.startSession('session-1', 'bash');
  await client.startSession('session-2', 'zsh');

  // Send input to different sessions
  await client.sendInput('session-1', 'echo "Hello from bash"\n', 1);
  await client.sendInput('session-2', 'echo "Hello from zsh"\n', 1);

  // Wait for output
  await new Promise(resolve => setTimeout(resolve, 2000));

  // Close sessions
  await client.closeSession('session-1');
  await client.closeSession('session-2');

  client.disconnect();
}
```

## React Hook

```typescript
// use-terminal.ts

import { useState, useEffect, useCallback, useRef } from 'react';
import { AureTTYWebSocketClient } from './websocket-client';
import { TerminalSessionEvent, TerminalSessionHandle } from './types';

export interface UseTerminalOptions {
  url: string;
  viewerId: string;
  apiKey: string;
  sessionId: string;
  shell?: string;
  protocol?: 'json' | 'msgpack';
  autoConnect?: boolean;
}

export function useTerminal(options: UseTerminalOptions) {
  const [connected, setConnected] = useState(false);
  const [session, setSession] = useState<TerminalSessionHandle | null>(null);
  const [output, setOutput] = useState<string>('');
  const [error, setError] = useState<Error | null>(null);

  const clientRef = useRef<AureTTYWebSocketClient | null>(null);
  const sequenceRef = useRef(0);

  useEffect(() => {
    const client = new AureTTYWebSocketClient(
      options.url,
      options.viewerId,
      options.apiKey,
      { protocol: options.protocol || 'json', reconnect: true }
    );

    clientRef.current = client;

    client.on('connected', () => {
      setConnected(true);
      setError(null);
    });

    client.on('disconnected', () => {
      setConnected(false);
    });

    client.on('error', (err: Error) => {
      setError(err);
    });

    client.on(`event:${options.sessionId}`, (event: TerminalSessionEvent) => {
      if (event.eventType === 'output') {
        setOutput(prev => prev + event.text);
      } else if (event.eventType === 'exited') {
        console.log('Process exited:', event.exitCode);
      }
    });

    if (options.autoConnect !== false) {
      client.connect()
        .then(() => client.startSession(
          options.sessionId,
          options.shell || 'bash'
        ))
        .then(setSession)
        .catch(setError);
    }

    return () => {
      client.disconnect();
    };
  }, [options.url, options.viewerId, options.apiKey, options.sessionId]);

  const sendInput = useCallback(async (text: string) => {
    if (!clientRef.current || !connected) {
      throw new Error('Not connected');
    }

    await clientRef.current.sendInput(
      options.sessionId,
      text,
      ++sequenceRef.current
    );
  }, [connected, options.sessionId]);

  const resize = useCallback(async (columns: number, rows: number) => {
    if (!clientRef.current || !connected) {
      throw new Error('Not connected');
    }

    await clientRef.current.resizeTerminal(options.sessionId, columns, rows);
  }, [connected, options.sessionId]);

  const close = useCallback(async () => {
    if (!clientRef.current) return;

    await clientRef.current.closeSession(options.sessionId);
    clientRef.current.disconnect();
  }, [options.sessionId]);

  return {
    connected,
    session,
    output,
    error,
    sendInput,
    resize,
    close
  };
}

// Example React component
function TerminalComponent() {
  const terminal = useTerminal({
    url: 'ws://localhost:17850/api/v1',
    viewerId: 'my-viewer',
    apiKey: 'your-api-key',
    sessionId: 'react-session',
    shell: 'bash',
    protocol: 'msgpack'
  });

  const handleCommand = async () => {
    await terminal.sendInput('echo hello\n');
  };

  return (
    <div>
      <div>Status: {terminal.connected ? 'Connected' : 'Disconnected'}</div>
      {terminal.error && <div>Error: {terminal.error.message}</div>}
      <pre>{terminal.output}</pre>
      <button onClick={handleCommand}>Send Command</button>
      <button onClick={terminal.close}>Close</button>
    </div>
  );
}
```

## See Also

- [REST API Reference](../../api/REST_API.md)
- [WebSocket API Reference](../../api/WEBSOCKET_API.md)
- [Getting Started Guide](../../GETTING_STARTED.md)
- [Python Client Example](python_client.md)

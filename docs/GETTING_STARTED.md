# Getting Started with AureTTY

This guide will help you get up and running with AureTTY in minutes.

## Prerequisites

### Linux
- .NET 10 SDK or Runtime
- `util-linux` package (provides `script` command)
- `sudo` (for credential switching, optional)

```bash
# Debian/Ubuntu
sudo apt-get install dotnet-sdk-10.0 util-linux

# RHEL/CentOS/Fedora
sudo yum install dotnet-sdk-10.0 util-linux
```

### Windows
- .NET 10 SDK or Runtime
- Windows 10 1809+ or Windows Server 2019+ (for ConPTY support)
- PowerShell 7 (recommended) or Windows PowerShell

Download .NET 10 from: https://dotnet.microsoft.com/download/dotnet/10.0

## Installation

### Option 1: Build from Source

```bash
# Clone the repository
git clone https://github.com/vitkuz573/AureTTY.git
cd AureTTY

# Restore dependencies
dotnet restore AureTTY.slnx

# Build
dotnet build AureTTY.slnx -c Release

# Run
dotnet run --project src/AureTTY/AureTTY.csproj -f net10.0 -c Release -- \
  --transport http \
  --api-key your-secure-api-key
```

### Option 2: Use Pre-built Binary

Download the latest release from [GitHub Releases](https://github.com/vitkuz573/AureTTY/releases/latest).

```bash
# Linux
chmod +x AureTTY
./AureTTY --transport http --api-key your-secure-api-key

# Windows
.\AureTTY.exe --transport http --api-key your-secure-api-key
```

### Option 3: NativeAOT (Fastest Startup)

```bash
# Linux
dotnet publish src/AureTTY/AureTTY.csproj \
  -f net10.0 -c Release -r linux-x64 \
  --self-contained true -p:PublishAot=true \
  -p:OpenApiGenerateDocuments=false \
  -o publish

./publish/AureTTY --transport http --api-key your-secure-api-key

# Windows
dotnet publish src/AureTTY/AureTTY.csproj `
  -f net10.0-windows -c Release -r win-x64 `
  --self-contained true -p:PublishAot=true `
  -p:OpenApiGenerateDocuments=false `
  -o publish

.\publish\AureTTY.exe --transport http --api-key your-secure-api-key
```

## First Steps

### 1. Start the Service

```bash
# Start with HTTP transport only
dotnet run --project src/AureTTY/AureTTY.csproj -f net10.0 -- \
  --transport http \
  --http-listen-url http://127.0.0.1:17850 \
  --api-key my-secret-key
```

You should see output like:
```
[INF] AureTTY terminal service starting...
[INF] HTTP API listening on http://127.0.0.1:17850
[INF] AureTTY terminal service started
```

### 2. Check Service Health

```bash
curl -H "X-AureTTY-Key: my-secret-key" http://localhost:17850/api/v1/health
```

Response:
```json
{
  "status": "ok",
  "apiVersion": "v1",
  "transports": ["http", "ws"],
  "allowApiKeyQueryParameter": false,
  "maxConcurrentSessions": 32,
  "maxSessionsPerViewer": 8
}
```

### 3. Create Your First Terminal Session

```bash
curl -X POST \
  -H "X-AureTTY-Key: my-secret-key" \
  -H "Content-Type: application/json" \
  -d '{
    "sessionId": "my-first-session",
    "shell": "bash",
    "columns": 80,
    "rows": 24
  }' \
  http://localhost:17850/api/v1/viewers/my-viewer/sessions
```

Response:
```json
{
  "sessionId": "my-first-session",
  "viewerId": "my-viewer",
  "state": "running",
  "processId": 12345,
  "shell": "bash",
  "columns": 80,
  "rows": 24,
  "createdAtUtc": "2026-03-20T10:30:00Z"
}
```

### 4. Send Input to the Session

```bash
curl -X POST \
  -H "X-AureTTY-Key: my-secret-key" \
  -H "Content-Type: application/json" \
  -d '{
    "text": "echo Hello, AureTTY!\n",
    "sequence": 1
  }' \
  http://localhost:17850/api/v1/viewers/my-viewer/sessions/my-first-session/inputs
```

### 5. Stream Output via SSE

```bash
curl -N -H "X-AureTTY-Key: my-secret-key" \
  http://localhost:17850/api/v1/viewers/my-viewer/events
```

You'll see events like:
```
event: terminal.session
data: {"sessionId":"my-first-session","eventType":"output","text":"Hello, AureTTY!\r\n","sequenceNumber":1}

event: terminal.session
data: {"sessionId":"my-first-session","eventType":"output","text":"$ ","sequenceNumber":2}
```

### 6. Close the Session

```bash
curl -X DELETE \
  -H "X-AureTTY-Key: my-secret-key" \
  http://localhost:17850/api/v1/viewers/my-viewer/sessions/my-first-session
```

## Using WebSocket

### Basic WebSocket Connection

```javascript
const ws = new WebSocket('ws://localhost:17850/api/v1/viewers/my-viewer/ws?api_key=my-secret-key');

ws.onopen = () => {
  console.log('Connected!');

  // Start a session
  ws.send(JSON.stringify({
    type: 'request',
    id: 'req-1',
    method: 'terminal.start',
    payload: {
      viewerId: 'my-viewer',
      request: {
        sessionId: 'ws-session-1',
        shell: 'bash',
        columns: 80,
        rows: 24
      }
    }
  }));
};

ws.onmessage = (event) => {
  const msg = JSON.parse(event.data);
  console.log('Received:', msg);

  if (msg.type === 'response' && msg.id === 'req-1') {
    console.log('Session started:', msg.payload);

    // Send input
    ws.send(JSON.stringify({
      type: 'request',
      id: 'req-2',
      method: 'terminal.input',
      payload: {
        viewerId: 'my-viewer',
        request: {
          sessionId: 'ws-session-1',
          text: 'echo Hello from WebSocket!\n',
          sequence: 1
        }
      }
    }));
  }

  if (msg.type === 'event' && msg.method === 'terminal.session') {
    const event = msg.payload.event;
    if (event.eventType === 'output') {
      console.log('Output:', event.text);
    }
  }
};

ws.onerror = (error) => {
  console.error('WebSocket error:', error);
};

ws.onclose = () => {
  console.log('Disconnected');
};
```

### Using MessagePack Protocol

```javascript
import msgpack from 'msgpack-lite';

const ws = new WebSocket('ws://localhost:17850/api/v1/viewers/my-viewer/ws?protocol=msgpack&api_key=my-secret-key');
ws.binaryType = 'arraybuffer';

ws.onopen = () => {
  const message = {
    type: 'request',
    id: 'req-1',
    method: 'terminal.start',
    payload: {
      viewerId: 'my-viewer',
      request: { sessionId: 'msgpack-session', shell: 'bash' }
    }
  };

  ws.send(msgpack.encode(message));
};

ws.onmessage = (event) => {
  const msg = msgpack.decode(new Uint8Array(event.data));
  console.log('Received:', msg);
};
```

## Common Configurations

### Development Setup

```bash
# Enable query parameter auth for easier testing
dotnet run --project src/AureTTY/AureTTY.csproj -f net10.0 -- \
  --transport http \
  --api-key dev-key \
  --allow-api-key-query
```

### Production Setup

```bash
# Use environment variables for security
export AURETTY_API_KEY="$(openssl rand -base64 32)"
export AURETTY_TRANSPORTS="http"
export AURETTY_HTTP_LISTEN_URL="http://0.0.0.0:17850"
export AURETTY_MAX_CONCURRENT_SESSIONS=128
export AURETTY_MAX_SESSIONS_PER_VIEWER=32

dotnet run --project src/AureTTY/AureTTY.csproj -f net10.0 -c Release
```

### Behind Reverse Proxy (nginx)

```nginx
server {
    listen 443 ssl http2;
    server_name terminal.example.com;

    ssl_certificate /path/to/cert.pem;
    ssl_certificate_key /path/to/key.pem;

    location /api/ {
        proxy_pass http://127.0.0.1:17850;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;

        # For SSE
        proxy_buffering off;
        proxy_cache off;
        proxy_read_timeout 86400s;
    }
}
```

### With Both HTTP and Pipe Transports

```bash
dotnet run --project src/AureTTY/AureTTY.csproj -f net10.0 -- \
  --transport http --transport pipe \
  --api-key http-secret-key \
  --pipe-name auretty-terminal \
  --pipe-token pipe-secret-token
```

## Next Steps

- [API Reference](api/REST_API.md) - Complete HTTP API documentation
- [WebSocket Guide](guides/WEBSOCKET_GUIDE.md) - Advanced WebSocket features
- [Examples](examples/) - Code examples in multiple languages
- [Architecture](architecture/OVERVIEW.md) - System design and internals
- [Troubleshooting](TROUBLESHOOTING.md) - Common issues and solutions

## Quick Tips

1. **Always use strong API keys in production**
   ```bash
   openssl rand -base64 32
   ```

2. **Monitor session limits**
   ```bash
   curl -H "X-AureTTY-Key: key" http://localhost:17850/api/v1/sessions | jq length
   ```

3. **Use WebSocket for real-time applications**
   - Lower latency than SSE
   - Bidirectional communication
   - MessagePack for bandwidth efficiency

4. **Enable multiplexing for multiple sessions**
   ```javascript
   const ws = new WebSocket('ws://localhost:17850/api/v1/viewers/viewer-1/sessions/ws');
   ```

5. **Implement reconnection logic**
   ```javascript
   let lastSeq = 0;
   // Track sequenceNumber from events
   // On reconnect, use terminal.resume with lastReceivedSequenceNumber
   ```

## Getting Help

- [GitHub Issues](https://github.com/vitkuz573/AureTTY/issues) - Report bugs or request features
- [Discussions](https://github.com/vitkuz573/AureTTY/discussions) - Ask questions
- [Documentation](README.md) - Full documentation

## What's Next?

Now that you have AureTTY running, explore:

- **Multiple Sessions**: Create and manage multiple terminal sessions per viewer
- **Session Multiplexing**: Use a single WebSocket for all sessions
- **Reconnection**: Implement automatic reconnection with state recovery
- **Custom Shells**: Use different shells (bash, zsh, fish, powershell)
- **Terminal Resizing**: Dynamically resize terminals
- **Signal Handling**: Send signals (SIGINT, SIGTERM) to processes
- **Input Diagnostics**: Monitor input queue and processing

Happy coding! 🚀

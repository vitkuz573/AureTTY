# AureTTY REST API Reference

Complete reference for the AureTTY HTTP REST API.

## Base URL

```
http://localhost:17850/api/v1
```

## Authentication

All API requests require authentication via API key.

### Header Authentication (Recommended)

```http
X-AureTTY-Key: your-api-key
```

### Query Parameter Authentication (Optional)

Must be explicitly enabled with `--allow-api-key-query` flag.

```http
GET /api/v1/health?api_key=your-api-key
```

## Response Format

### Success Response

```json
{
  "sessionId": "session-1",
  "viewerId": "viewer-1",
  "state": "running"
}
```

### Error Response

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "Session ID is required"
}
```

## Endpoints

### Health & Discovery

#### Get Service Health

```http
GET /api/v1/health
```

Returns service status and capabilities.

**Response:**
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

**Status Codes:**
- `200 OK` - Service is healthy

---

#### Get OpenAPI Specification

```http
GET /openapi/v1.json
```

Returns OpenAPI 3.0 specification for the API.

**Response:** OpenAPI JSON document

**Status Codes:**
- `200 OK` - Specification returned
- `401 Unauthorized` - Missing or invalid API key

---

### Session Management

#### List All Sessions

```http
GET /api/v1/sessions
```

Returns all active terminal sessions across all viewers.

**Response:**
```json
[
  {
    "sessionId": "session-1",
    "viewerId": "viewer-1",
    "state": "running",
    "processId": 12345,
    "shell": "bash",
    "columns": 80,
    "rows": 24,
    "createdAtUtc": "2026-03-20T10:00:00Z",
    "lastActivityUtc": "2026-03-20T10:05:00Z"
  }
]
```

**Status Codes:**
- `200 OK` - Sessions returned
- `401 Unauthorized` - Missing or invalid API key

---

#### Close All Sessions

```http
DELETE /api/v1/sessions
```

Closes all active terminal sessions.

**Status Codes:**
- `204 No Content` - All sessions closed
- `401 Unauthorized` - Missing or invalid API key

---

#### List Viewer Sessions

```http
GET /api/v1/viewers/{viewerId}/sessions
```

Returns all sessions for a specific viewer.

**Path Parameters:**
- `viewerId` (string, required) - Viewer identifier

**Response:**
```json
[
  {
    "sessionId": "session-1",
    "viewerId": "viewer-1",
    "state": "running",
    "processId": 12345,
    "shell": "bash",
    "columns": 80,
    "rows": 24,
    "createdAtUtc": "2026-03-20T10:00:00Z"
  }
]
```

**Status Codes:**
- `200 OK` - Sessions returned
- `401 Unauthorized` - Missing or invalid API key

---

#### Create Terminal Session

```http
POST /api/v1/viewers/{viewerId}/sessions
```

Creates a new terminal session.

**Path Parameters:**
- `viewerId` (string, required) - Viewer identifier

**Request Body:**
```json
{
  "sessionId": "my-session",
  "shell": "bash",
  "runContext": "user",
  "userName": "username",
  "domain": "DOMAIN",
  "password": "password",
  "loadUserProfile": true,
  "workingDirectory": "/home/user",
  "columns": 80,
  "rows": 24
}
```

**Fields:**
- `sessionId` (string, optional) - Session identifier (auto-generated if omitted)
- `shell` (string, required) - Shell to launch: `bash`, `zsh`, `fish`, `sh`, `powershell`, `cmd`, `pwsh`
- `runContext` (string, optional) - Execution context: `user`, `system` (default: `user`)
- `userName` (string, optional) - User name for credential switching (Linux/Windows)
- `domain` (string, optional) - Domain name (Windows only)
- `password` (string, optional) - Password for credential switching
- `loadUserProfile` (boolean, optional) - Load user profile (Windows only, default: false)
- `workingDirectory` (string, optional) - Initial working directory
- `columns` (integer, optional) - Terminal width in columns (default: 80)
- `rows` (integer, optional) - Terminal height in rows (default: 24)

**Response:**
```json
{
  "sessionId": "my-session",
  "viewerId": "viewer-1",
  "state": "running",
  "processId": 12345,
  "shell": "bash",
  "columns": 80,
  "rows": 24,
  "createdAtUtc": "2026-03-20T10:00:00Z"
}
```

**Status Codes:**
- `201 Created` - Session created successfully
- `400 Bad Request` - Invalid request parameters
- `401 Unauthorized` - Missing or invalid API key
- `409 Conflict` - Session with this ID already exists
- `429 Too Many Requests` - Session limit reached

---

#### Get Session Details

```http
GET /api/v1/viewers/{viewerId}/sessions/{sessionId}
```

Returns details for a specific session.

**Path Parameters:**
- `viewerId` (string, required) - Viewer identifier
- `sessionId` (string, required) - Session identifier

**Response:**
```json
{
  "sessionId": "my-session",
  "viewerId": "viewer-1",
  "state": "running",
  "processId": 12345,
  "shell": "bash",
  "columns": 80,
  "rows": 24,
  "exitCode": null,
  "createdAtUtc": "2026-03-20T10:00:00Z",
  "lastActivityUtc": "2026-03-20T10:05:00Z"
}
```

**Status Codes:**
- `200 OK` - Session details returned
- `401 Unauthorized` - Missing or invalid API key
- `403 Forbidden` - Session belongs to another viewer
- `404 Not Found` - Session not found

---

#### Attach to Existing Session

```http
POST /api/v1/viewers/{viewerId}/sessions/{sessionId}/attachments
```

Attaches to an existing session and optionally replays missed events.

**Path Parameters:**
- `viewerId` (string, required) - Viewer identifier
- `sessionId` (string, required) - Session identifier

**Request Body:**
```json
{
  "lastReceivedSequenceNumber": 42,
  "columns": 80,
  "rows": 24
}
```

**Fields:**
- `lastReceivedSequenceNumber` (integer, optional) - Last received event sequence number for replay
- `columns` (integer, optional) - Resize terminal width
- `rows` (integer, optional) - Resize terminal height

**Response:**
```json
{
  "sessionId": "my-session",
  "viewerId": "viewer-1",
  "state": "running",
  "processId": 12345,
  "replayedEvents": 15
}
```

**Status Codes:**
- `200 OK` - Attached successfully
- `400 Bad Request` - Invalid parameters
- `401 Unauthorized` - Missing or invalid API key
- `403 Forbidden` - Session belongs to another viewer
- `404 Not Found` - Session not found

---

#### Close Session

```http
DELETE /api/v1/viewers/{viewerId}/sessions/{sessionId}
```

Closes a terminal session.

**Path Parameters:**
- `viewerId` (string, required) - Viewer identifier
- `sessionId` (string, required) - Session identifier

**Status Codes:**
- `204 No Content` - Session closed
- `401 Unauthorized` - Missing or invalid API key
- `403 Forbidden` - Session belongs to another viewer
- `404 Not Found` - Session not found

---

#### Close All Viewer Sessions

```http
DELETE /api/v1/viewers/{viewerId}/sessions
```

Closes all sessions for a specific viewer.

**Path Parameters:**
- `viewerId` (string, required) - Viewer identifier

**Status Codes:**
- `204 No Content` - All viewer sessions closed
- `401 Unauthorized` - Missing or invalid API key

---

### Session Operations

#### Send Input

```http
POST /api/v1/viewers/{viewerId}/sessions/{sessionId}/inputs
```

Sends input to a terminal session.

**Path Parameters:**
- `viewerId` (string, required) - Viewer identifier
- `sessionId` (string, required) - Session identifier

**Request Body:**
```json
{
  "text": "echo hello\n",
  "sequence": 1
}
```

**Fields:**
- `text` (string, required) - Input text to send
- `sequence` (integer, required) - Sequence number for ordering

**Status Codes:**
- `202 Accepted` - Input queued for processing
- `400 Bad Request` - Invalid input
- `401 Unauthorized` - Missing or invalid API key
- `403 Forbidden` - Session belongs to another viewer
- `404 Not Found` - Session not found
- `429 Too Many Requests` - Input queue full

---

#### Get Input Diagnostics

```http
GET /api/v1/viewers/{viewerId}/sessions/{sessionId}/input-diagnostics
```

Returns input queue diagnostics.

**Path Parameters:**
- `viewerId` (string, required) - Viewer identifier
- `sessionId` (string, required) - Session identifier

**Response:**
```json
{
  "sessionId": "my-session",
  "viewerId": "viewer-1",
  "state": "running",
  "pendingInputChunks": 5,
  "totalInputChunksProcessed": 142,
  "lastInputSequenceNumber": 147,
  "generatedAtUtc": "2026-03-20T10:05:00Z"
}
```

**Status Codes:**
- `200 OK` - Diagnostics returned
- `401 Unauthorized` - Missing or invalid API key
- `403 Forbidden` - Session belongs to another viewer
- `404 Not Found` - Session not found

---

#### Resize Terminal

```http
PUT /api/v1/viewers/{viewerId}/sessions/{sessionId}/terminal-size
```

Resizes the terminal window.

**Path Parameters:**
- `viewerId` (string, required) - Viewer identifier
- `sessionId` (string, required) - Session identifier

**Request Body:**
```json
{
  "columns": 120,
  "rows": 30
}
```

**Fields:**
- `columns` (integer, required) - New width in columns (1-500)
- `rows` (integer, required) - New height in rows (1-200)

**Status Codes:**
- `204 No Content` - Terminal resized
- `400 Bad Request` - Invalid dimensions
- `401 Unauthorized` - Missing or invalid API key
- `403 Forbidden` - Session belongs to another viewer
- `404 Not Found` - Session not found

---

#### Send Signal

```http
POST /api/v1/viewers/{viewerId}/sessions/{sessionId}/signals
```

Sends a signal to the terminal process.

**Path Parameters:**
- `viewerId` (string, required) - Viewer identifier
- `sessionId` (string, required) - Session identifier

**Request Body:**
```json
{
  "signal": "SIGINT"
}
```

**Fields:**
- `signal` (string, required) - Signal name: `SIGINT`, `SIGTERM`, `SIGKILL`, `SIGHUP`, `SIGQUIT`

**Status Codes:**
- `202 Accepted` - Signal sent
- `400 Bad Request` - Invalid signal
- `401 Unauthorized` - Missing or invalid API key
- `403 Forbidden` - Session belongs to another viewer
- `404 Not Found` - Session not found

---

### Event Streaming

#### Stream Events (SSE)

```http
GET /api/v1/viewers/{viewerId}/events
```

Opens a Server-Sent Events stream for real-time terminal events.

**Path Parameters:**
- `viewerId` (string, required) - Viewer identifier

**Response:** SSE stream

**Event Format:**
```
event: terminal.session
data: {"sessionId":"session-1","eventType":"output","text":"hello\r\n","sequenceNumber":1}

event: terminal.session
data: {"sessionId":"session-1","eventType":"exited","exitCode":0,"sequenceNumber":2}
```

**Event Types:**
- `started` - Session started
- `attached` - Session attached
- `output` - Terminal output
- `exited` - Process exited
- `closed` - Session closed
- `dropped` - Events were dropped (backpressure)

**Status Codes:**
- `200 OK` - Stream opened
- `401 Unauthorized` - Missing or invalid API key

**Notes:**
- Keep connection alive with periodic pings
- Handle `dropped` events by reconnecting or increasing buffer size
- Use `Last-Event-ID` header for reconnection (not currently supported)

---

## Rate Limits

### Session Limits

- **Max Concurrent Sessions**: 32 (configurable)
- **Max Sessions Per Viewer**: 8 (configurable)

### Input Limits

- **Max Pending Input Chunks**: 8192 (configurable)
- **Max Input Text Length**: 64 KB per chunk

### Buffer Limits

- **Replay Buffer**: 4096 events (configurable)
- **SSE Subscription Buffer**: 2048 events (configurable)

## Error Codes

| Status Code | Description |
|-------------|-------------|
| 200 OK | Request successful |
| 201 Created | Resource created |
| 202 Accepted | Request accepted for processing |
| 204 No Content | Request successful, no content to return |
| 400 Bad Request | Invalid request parameters |
| 401 Unauthorized | Missing or invalid API key |
| 403 Forbidden | Access denied (wrong viewer) |
| 404 Not Found | Resource not found |
| 409 Conflict | Resource already exists |
| 429 Too Many Requests | Rate limit exceeded |
| 500 Internal Server Error | Server error |

## Examples

### Complete Session Lifecycle

```bash
# 1. Create session
SESSION_ID=$(curl -s -X POST \
  -H "X-AureTTY-Key: my-key" \
  -H "Content-Type: application/json" \
  -d '{"shell":"bash"}' \
  http://localhost:17850/api/v1/viewers/viewer-1/sessions | jq -r .sessionId)

# 2. Send input
curl -X POST \
  -H "X-AureTTY-Key: my-key" \
  -H "Content-Type: application/json" \
  -d '{"text":"ls -la\n","sequence":1}' \
  http://localhost:17850/api/v1/viewers/viewer-1/sessions/$SESSION_ID/inputs

# 3. Stream output (in another terminal)
curl -N -H "X-AureTTY-Key: my-key" \
  http://localhost:17850/api/v1/viewers/viewer-1/events

# 4. Resize terminal
curl -X PUT \
  -H "X-AureTTY-Key: my-key" \
  -H "Content-Type: application/json" \
  -d '{"columns":120,"rows":30}' \
  http://localhost:17850/api/v1/viewers/viewer-1/sessions/$SESSION_ID/terminal-size

# 5. Send interrupt signal
curl -X POST \
  -H "X-AureTTY-Key: my-key" \
  -H "Content-Type: application/json" \
  -d '{"signal":"SIGINT"}' \
  http://localhost:17850/api/v1/viewers/viewer-1/sessions/$SESSION_ID/signals

# 6. Close session
curl -X DELETE \
  -H "X-AureTTY-Key: my-key" \
  http://localhost:17850/api/v1/viewers/viewer-1/sessions/$SESSION_ID
```

### Python Client Example

```python
import requests
import json

class AureTTYClient:
    def __init__(self, base_url, api_key):
        self.base_url = base_url
        self.headers = {'X-AureTTY-Key': api_key}

    def create_session(self, viewer_id, shell='bash', columns=80, rows=24):
        response = requests.post(
            f'{self.base_url}/viewers/{viewer_id}/sessions',
            headers=self.headers,
            json={'shell': shell, 'columns': columns, 'rows': rows}
        )
        response.raise_for_status()
        return response.json()

    def send_input(self, viewer_id, session_id, text, sequence):
        response = requests.post(
            f'{self.base_url}/viewers/{viewer_id}/sessions/{session_id}/inputs',
            headers=self.headers,
            json={'text': text, 'sequence': sequence}
        )
        response.raise_for_status()

    def stream_events(self, viewer_id):
        response = requests.get(
            f'{self.base_url}/viewers/{viewer_id}/events',
            headers=self.headers,
            stream=True
        )
        response.raise_for_status()

        for line in response.iter_lines():
            if line.startswith(b'data: '):
                yield json.loads(line[6:])

    def close_session(self, viewer_id, session_id):
        response = requests.delete(
            f'{self.base_url}/viewers/{viewer_id}/sessions/{session_id}',
            headers=self.headers
        )
        response.raise_for_status()

# Usage
client = AureTTYClient('http://localhost:17850/api/v1', 'my-api-key')

# Create session
session = client.create_session('my-viewer', shell='bash')
print(f"Created session: {session['sessionId']}")

# Send input
client.send_input('my-viewer', session['sessionId'], 'echo hello\n', 1)

# Stream events
for event in client.stream_events('my-viewer'):
    if event.get('eventType') == 'output':
        print(event['text'], end='')

# Close session
client.close_session('my-viewer', session['sessionId'])
```

## Best Practices

1. **Always use HTTPS in production** - Protect API keys in transit
2. **Implement exponential backoff** - For retries on rate limits
3. **Handle reconnection** - SSE streams can disconnect
4. **Monitor session limits** - Track active sessions per viewer
5. **Use sequence numbers** - For input ordering and replay
6. **Clean up sessions** - Close sessions when done
7. **Handle backpressure** - Watch for `dropped` events
8. **Validate input** - Check text length and encoding
9. **Use WebSocket for real-time** - Lower latency than SSE
10. **Enable compression** - Use gzip for HTTP responses

## See Also

- [WebSocket API Reference](WEBSOCKET_API.md)
- [Getting Started Guide](../GETTING_STARTED.md)
- [Code Examples](../examples/)
- [Troubleshooting](../TROUBLESHOOTING.md)

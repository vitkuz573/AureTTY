# Troubleshooting Guide

Common issues and solutions for AureTTY.

## Connection Issues

### Service Won't Start

**Symptom:** Service fails to start or exits immediately.

**Possible Causes:**

1. **Port already in use**
   ```
   Error: Failed to bind to address http://127.0.0.1:17850
   ```

   **Solution:**
   ```bash
   # Check what's using the port
   sudo lsof -i :17850  # Linux
   netstat -ano | findstr :17850  # Windows

   # Use a different port
   --http-listen-url http://127.0.0.1:17851
   ```

2. **Missing API key**
   ```
   Error: API key is required when HTTP transport is enabled
   ```

   **Solution:**
   ```bash
   --api-key your-secure-key
   # or
   export AURETTY_API_KEY=your-secure-key
   ```

3. **Missing pipe token**
   ```
   Error: Pipe token is required when pipe transport is enabled
   ```

   **Solution:**
   ```bash
   --pipe-token your-secure-token
   # or
   export AURETTY_PIPE_TOKEN=your-secure-token
   ```

### Cannot Connect to Service

**Symptom:** Client connection refused or timeout.

**Solutions:**

1. **Check service is running**
   ```bash
   curl http://localhost:17850/api/v1/health
   ```

2. **Check firewall**
   ```bash
   # Linux
   sudo ufw allow 17850/tcp

   # Windows
   netsh advfirewall firewall add rule name="AureTTY" dir=in action=allow protocol=TCP localport=17850
   ```

3. **Check listen address**
   ```bash
   # Listen on all interfaces
   --http-listen-url http://0.0.0.0:17850
   ```

## Authentication Issues

### 401 Unauthorized

**Symptom:** All requests return 401 Unauthorized.

**Solutions:**

1. **Check API key header**
   ```bash
   # Correct
   curl -H "X-AureTTY-Key: your-key" http://localhost:17850/api/v1/health

   # Wrong header name
   curl -H "Authorization: Bearer your-key" http://localhost:17850/api/v1/health
   ```

2. **Enable query parameter auth (development only)**
   ```bash
   --allow-api-key-query

   curl http://localhost:17850/api/v1/health?api_key=your-key
   ```

3. **Check API key matches**
   ```bash
   # Service started with
   --api-key secret123

   # Client must use same key
   -H "X-AureTTY-Key: secret123"
   ```

### WebSocket Authentication Failed

**Symptom:** WebSocket connection rejected with 401.

**Solutions:**

1. **Use query parameter (if enabled)**
   ```javascript
   const ws = new WebSocket('ws://localhost:17850/api/v1/viewers/v1/sessions/ws?api_key=your-key');
   ```

2. **Check header support**
   ```javascript
   // Note: Browser WebSocket API doesn't support custom headers
   // Use query parameter or server-side proxy
   ```

## Session Issues

### Session Creation Failed

**Symptom:** POST /sessions returns 400 or 409.

**Solutions:**

1. **Session ID conflict (409)**
   ```json
   {"error": "Session with ID 'session-1' already exists"}
   ```

   **Solution:** Use unique session IDs or omit sessionId for auto-generation
   ```bash
   # Auto-generate ID
   curl -X POST -H "X-AureTTY-Key: key" \
     -d '{"shell":"bash"}' \
     http://localhost:17850/api/v1/viewers/v1/sessions
   ```

2. **Invalid shell (400)**
   ```json
   {"error": "Unsupported shell 'invalid'"}
   ```

   **Solution:** Use supported shells: `bash`, `zsh`, `fish`, `sh`, `powershell`, `cmd`, `pwsh`

3. **Session limit reached (429)**
   ```json
   {"error": "Maximum concurrent sessions reached"}
   ```

   **Solution:**
   ```bash
   # Increase limits
   --max-concurrent-sessions 64
   --max-sessions-per-viewer 16

   # Or close unused sessions
   curl -X DELETE -H "X-AureTTY-Key: key" \
     http://localhost:17850/api/v1/viewers/v1/sessions/old-session
   ```

### Session Not Found (404)

**Symptom:** Operations on session return 404.

**Possible Causes:**

1. **Session was closed**
   - Check session list: `GET /api/v1/sessions`
   - Session may have exited or been closed

2. **Wrong viewer ID**
   ```bash
   # Session created with viewer-1
   POST /api/v1/viewers/viewer-1/sessions

   # But accessed with viewer-2 (403 Forbidden)
   GET /api/v1/viewers/viewer-2/sessions/session-1
   ```

3. **Session ID typo**
   - Verify session ID is correct
   - Session IDs are case-sensitive

### Session Hangs or No Output

**Symptom:** Session created but no output received.

**Solutions:**

1. **Check session state**
   ```bash
   curl -H "X-AureTTY-Key: key" \
     http://localhost:17850/api/v1/viewers/v1/sessions/session-1
   ```

2. **Send input to trigger output**
   ```bash
   curl -X POST -H "X-AureTTY-Key: key" \
     -d '{"text":"echo test\n","sequence":1}' \
     http://localhost:17850/api/v1/viewers/v1/sessions/session-1/inputs
   ```

3. **Check SSE connection**
   ```bash
   # SSE must be connected before session starts
   curl -N -H "X-AureTTY-Key: key" \
     http://localhost:17850/api/v1/viewers/v1/events
   ```

## Platform-Specific Issues

### Linux: "script: command not found"

**Symptom:** Session creation fails with script command error.

**Solution:**
```bash
# Debian/Ubuntu
sudo apt-get install util-linux

# RHEL/CentOS/Fedora
sudo yum install util-linux

# Arch Linux
sudo pacman -S util-linux
```

### Linux: Permission Denied for Credential Switching

**Symptom:** Session creation with userName fails.

**Solution:**
```bash
# Ensure sudo is installed
sudo apt-get install sudo

# Configure passwordless sudo for AureTTY user (if needed)
echo "auretty ALL=(ALL) NOPASSWD: ALL" | sudo tee /etc/sudoers.d/auretty
```

### Windows: ConPTY Not Supported

**Symptom:** Session creation fails with ConPTY error.

**Solution:**
- Update to Windows 10 1809+ or Windows Server 2019+
- Check Windows version: `winver`
- Install latest Windows updates

### Windows: PowerShell Execution Policy

**Symptom:** PowerShell sessions fail to start.

**Solution:**
```powershell
# Check current policy
Get-ExecutionPolicy

# Set policy (as Administrator)
Set-ExecutionPolicy RemoteSigned -Scope CurrentUser
```

## Performance Issues

### High CPU Usage

**Symptom:** AureTTY process consuming high CPU.

**Solutions:**

1. **Check active sessions**
   ```bash
   curl -H "X-AureTTY-Key: key" http://localhost:17850/api/v1/sessions | jq length
   ```

2. **Reduce session limits**
   ```bash
   --max-concurrent-sessions 16
   --max-sessions-per-viewer 4
   ```

3. **Check for runaway processes**
   ```bash
   # Linux
   ps aux | grep AureTTY

   # Windows
   tasklist | findstr AureTTY
   ```

### High Memory Usage

**Symptom:** Memory usage grows over time.

**Solutions:**

1. **Reduce buffer sizes**
   ```bash
   --replay-buffer-capacity 2048
   --max-pending-input-chunks 4096
   --sse-subscription-buffer-capacity 1024
   ```

2. **Close inactive sessions**
   ```bash
   # Implement session timeout in your application
   # Close sessions after inactivity period
   ```

3. **Monitor memory**
   ```bash
   # Linux
   ps aux | grep AureTTY

   # Windows
   taskmgr
   ```

### Events Dropped

**Symptom:** Receiving "dropped" events in SSE/WebSocket stream.

**Solutions:**

1. **Increase buffer capacity**
   ```bash
   --sse-subscription-buffer-capacity 4096
   ```

2. **Optimize client processing**
   ```javascript
   // Bad: Slow synchronous processing
   ws.onmessage = (event) => {
     const msg = JSON.parse(event.data);
     processSlowly(msg);  // Blocks event loop
   };

   // Good: Fast async processing
   ws.onmessage = (event) => {
     const msg = JSON.parse(event.data);
     queueForProcessing(msg);  // Non-blocking
   };
   ```

3. **Use MessagePack protocol**
   ```javascript
   // Reduces serialization overhead
   const ws = new WebSocket('ws://localhost:17850/api/v1/viewers/v1/sessions/ws?protocol=msgpack');
   ```

4. **Reduce output frequency**
   ```bash
   # In terminal, reduce verbose output
   # Use --quiet flags where possible
   ```

### Slow Response Times

**Symptom:** API requests take too long.

**Solutions:**

1. **Check system load**
   ```bash
   # Linux
   top
   htop

   # Windows
   taskmgr
   ```

2. **Use WebSocket instead of HTTP**
   ```javascript
   // WebSocket has lower latency than HTTP polling
   const ws = new WebSocket('ws://localhost:17850/api/v1/viewers/v1/sessions/ws');
   ```

3. **Enable NativeAOT**
   ```bash
   # Faster startup and lower memory
   dotnet publish -p:PublishAot=true
   ```

## WebSocket Issues

### WebSocket Connection Drops

**Symptom:** WebSocket disconnects frequently.

**Solutions:**

1. **Implement ping/pong**
   ```javascript
   setInterval(() => {
     ws.send(JSON.stringify({
       type: 'request',
       id: 'ping',
       method: 'terminal.ping'
     }));
   }, 30000);  // Every 30 seconds
   ```

2. **Check network stability**
   ```bash
   # Test connection
   ping -c 100 your-server
   ```

3. **Implement reconnection**
   ```javascript
   ws.onclose = () => {
     setTimeout(() => reconnect(), 1000);
   };
   ```

### MessagePack Deserialization Errors

**Symptom:** Cannot decode MessagePack messages.

**Solutions:**

1. **Set binary type**
   ```javascript
   ws.binaryType = 'arraybuffer';
   ```

2. **Use correct decoder**
   ```javascript
   import msgpack from 'msgpack-lite';

   ws.onmessage = (event) => {
     const msg = msgpack.decode(new Uint8Array(event.data));
   };
   ```

3. **Check protocol parameter**
   ```javascript
   // Correct
   const ws = new WebSocket('ws://localhost:17850/api/v1/viewers/v1/sessions/ws?protocol=msgpack');

   // Wrong
   const ws = new WebSocket('ws://localhost:17850/api/v1/viewers/v1/sessions/ws?protocol=json');
   ```

### Multiplexing Not Working

**Symptom:** Events not received for specific sessions.

**Solutions:**

1. **Use correct endpoint**
   ```javascript
   // Multiplexed endpoint
   const ws = new WebSocket('ws://localhost:17850/api/v1/viewers/v1/sessions/ws');
   ```

2. **Start sessions explicitly**
   ```javascript
   // Must call terminal.start or terminal.resume
   ws.send(JSON.stringify({
     type: 'request',
     method: 'terminal.start',
     payload: { viewerId: 'v1', request: { sessionId: 's1', shell: 'bash' } }
   }));
   ```

3. **Check session ID in events**
   ```javascript
   ws.onmessage = (event) => {
     const msg = JSON.parse(event.data);
     if (msg.type === 'event') {
       console.log('Session:', msg.payload.event.sessionId);
     }
   };
   ```

## Input Issues

### Input Not Processed

**Symptom:** Input sent but no response from terminal.

**Solutions:**

1. **Check input diagnostics**
   ```bash
   curl -H "X-AureTTY-Key: key" \
     http://localhost:17850/api/v1/viewers/v1/sessions/s1/input-diagnostics
   ```

2. **Verify sequence numbers**
   ```javascript
   let sequence = 1;

   // Increment for each input
   await sendInput('echo test\n', sequence++);
   await sendInput('ls\n', sequence++);
   ```

3. **Check session state**
   ```bash
   # Session must be in "running" state
   curl -H "X-AureTTY-Key: key" \
     http://localhost:17850/api/v1/viewers/v1/sessions/s1
   ```

4. **Add newline**
   ```javascript
   // Wrong: no newline
   sendInput('echo test', 1);

   // Correct: with newline
   sendInput('echo test\n', 1);
   ```

### Input Queue Full (429)

**Symptom:** Input requests return 429 Too Many Requests.

**Solutions:**

1. **Increase queue size**
   ```bash
   --max-pending-input-chunks 16384
   ```

2. **Slow down input rate**
   ```javascript
   // Add delay between inputs
   await sendInput('cmd1\n', 1);
   await sleep(100);
   await sendInput('cmd2\n', 2);
   ```

3. **Check input diagnostics**
   ```bash
   curl -H "X-AureTTY-Key: key" \
     http://localhost:17850/api/v1/viewers/v1/sessions/s1/input-diagnostics
   ```

## Logging and Debugging

### Enable Debug Logging

```bash
# Set log level
export AURETTY_LOG_LEVEL=Debug

# Or use Serilog configuration
export Serilog__MinimumLevel__Default=Debug

dotnet run --project src/AureTTY/AureTTY.csproj
```

### View Logs

```bash
# Linux (systemd)
journalctl -u auretty -f

# Docker
docker logs -f auretty-container

# File logging (if configured)
tail -f /var/log/auretty/auretty.log
```

### Common Log Messages

**"Session limit reached"**
- Increase `--max-concurrent-sessions` or `--max-sessions-per-viewer`

**"Input queue full"**
- Increase `--max-pending-input-chunks`
- Slow down input rate

**"Events dropped"**
- Increase `--sse-subscription-buffer-capacity`
- Optimize client event processing

**"Process exited with code X"**
- Check shell configuration
- Verify working directory exists
- Check user permissions

## Getting Help

If you're still experiencing issues:

1. **Check GitHub Issues**
   - Search existing issues: https://github.com/vitkuz573/AureTTY/issues
   - Create new issue with:
     - AureTTY version
     - Platform (OS, .NET version)
     - Configuration (CLI args, env vars)
     - Error messages and logs
     - Steps to reproduce

2. **Enable Debug Logging**
   ```bash
   export AURETTY_LOG_LEVEL=Debug
   ```

3. **Collect Diagnostics**
   ```bash
   # Service health
   curl -H "X-AureTTY-Key: key" http://localhost:17850/api/v1/health

   # Active sessions
   curl -H "X-AureTTY-Key: key" http://localhost:17850/api/v1/sessions

   # System info
   dotnet --info
   uname -a  # Linux
   systeminfo  # Windows
   ```

4. **Minimal Reproduction**
   - Create minimal example that reproduces the issue
   - Include all relevant code and configuration
   - Test with latest version

## See Also

- [Getting Started Guide](GETTING_STARTED.md)
- [REST API Reference](api/REST_API.md)
- [WebSocket API Reference](api/WEBSOCKET_API.md)
- [GitHub Issues](https://github.com/vitkuz573/AureTTY/issues)

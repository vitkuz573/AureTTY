# Python Client Example

Complete Python client for AureTTY with support for HTTP REST API and WebSocket.

## Installation

```bash
pip install requests websocket-client msgpack
```

## HTTP REST Client

```python
import requests
import json
from typing import Optional, Dict, Any, List

class AureTTYClient:
    """HTTP REST client for AureTTY."""

    def __init__(self, base_url: str, api_key: str):
        """
        Initialize the client.

        Args:
            base_url: Base URL (e.g., http://localhost:17850/api/v1)
            api_key: API key for authentication
        """
        self.base_url = base_url.rstrip('/')
        self.api_key = api_key
        self.session = requests.Session()
        self.session.headers.update({'X-AureTTY-Key': api_key})

    def health(self) -> Dict[str, Any]:
        """Get service health."""
        response = self.session.get(f'{self.base_url}/health')
        response.raise_for_status()
        return response.json()

    def list_sessions(self) -> List[Dict[str, Any]]:
        """List all sessions."""
        response = self.session.get(f'{self.base_url}/sessions')
        response.raise_for_status()
        return response.json()

    def list_viewer_sessions(self, viewer_id: str) -> List[Dict[str, Any]]:
        """List sessions for a specific viewer."""
        response = self.session.get(f'{self.base_url}/viewers/{viewer_id}/sessions')
        response.raise_for_status()
        return response.json()

    def create_session(
        self,
        viewer_id: str,
        shell: str = 'bash',
        session_id: Optional[str] = None,
        columns: int = 80,
        rows: int = 24,
        working_directory: Optional[str] = None
    ) -> Dict[str, Any]:
        """
        Create a new terminal session.

        Args:
            viewer_id: Viewer identifier
            shell: Shell to launch (bash, zsh, fish, sh, powershell, cmd, pwsh)
            session_id: Optional session ID (auto-generated if omitted)
            columns: Terminal width
            rows: Terminal height
            working_directory: Initial working directory

        Returns:
            Session handle
        """
        payload = {
            'shell': shell,
            'columns': columns,
            'rows': rows
        }

        if session_id:
            payload['sessionId'] = session_id

        if working_directory:
            payload['workingDirectory'] = working_directory

        response = self.session.post(
            f'{self.base_url}/viewers/{viewer_id}/sessions',
            json=payload
        )
        response.raise_for_status()
        return response.json()

    def get_session(self, viewer_id: str, session_id: str) -> Dict[str, Any]:
        """Get session details."""
        response = self.session.get(
            f'{self.base_url}/viewers/{viewer_id}/sessions/{session_id}'
        )
        response.raise_for_status()
        return response.json()

    def attach_session(
        self,
        viewer_id: str,
        session_id: str,
        last_received_sequence_number: Optional[int] = None,
        columns: Optional[int] = None,
        rows: Optional[int] = None
    ) -> Dict[str, Any]:
        """
        Attach to existing session with optional replay.

        Args:
            viewer_id: Viewer identifier
            session_id: Session identifier
            last_received_sequence_number: Last received event sequence number
            columns: Resize terminal width
            rows: Resize terminal height

        Returns:
            Session handle with replay info
        """
        payload = {}

        if last_received_sequence_number is not None:
            payload['lastReceivedSequenceNumber'] = last_received_sequence_number

        if columns is not None:
            payload['columns'] = columns

        if rows is not None:
            payload['rows'] = rows

        response = self.session.post(
            f'{self.base_url}/viewers/{viewer_id}/sessions/{session_id}/attachments',
            json=payload
        )
        response.raise_for_status()
        return response.json()

    def send_input(
        self,
        viewer_id: str,
        session_id: str,
        text: str,
        sequence: int
    ) -> None:
        """
        Send input to terminal session.

        Args:
            viewer_id: Viewer identifier
            session_id: Session identifier
            text: Input text
            sequence: Sequence number for ordering
        """
        response = self.session.post(
            f'{self.base_url}/viewers/{viewer_id}/sessions/{session_id}/inputs',
            json={'text': text, 'sequence': sequence}
        )
        response.raise_for_status()

    def get_input_diagnostics(
        self,
        viewer_id: str,
        session_id: str
    ) -> Dict[str, Any]:
        """Get input queue diagnostics."""
        response = self.session.get(
            f'{self.base_url}/viewers/{viewer_id}/sessions/{session_id}/input-diagnostics'
        )
        response.raise_for_status()
        return response.json()

    def resize_terminal(
        self,
        viewer_id: str,
        session_id: str,
        columns: int,
        rows: int
    ) -> None:
        """
        Resize terminal window.

        Args:
            viewer_id: Viewer identifier
            session_id: Session identifier
            columns: New width
            rows: New height
        """
        response = self.session.put(
            f'{self.base_url}/viewers/{viewer_id}/sessions/{session_id}/terminal-size',
            json={'columns': columns, 'rows': rows}
        )
        response.raise_for_status()

    def send_signal(
        self,
        viewer_id: str,
        session_id: str,
        signal: str
    ) -> None:
        """
        Send signal to terminal process.

        Args:
            viewer_id: Viewer identifier
            session_id: Session identifier
            signal: Signal name (SIGINT, SIGTERM, SIGKILL, SIGHUP, SIGQUIT)
        """
        response = self.session.post(
            f'{self.base_url}/viewers/{viewer_id}/sessions/{session_id}/signals',
            json={'signal': signal}
        )
        response.raise_for_status()

    def close_session(self, viewer_id: str, session_id: str) -> None:
        """Close a terminal session."""
        response = self.session.delete(
            f'{self.base_url}/viewers/{viewer_id}/sessions/{session_id}'
        )
        response.raise_for_status()

    def close_viewer_sessions(self, viewer_id: str) -> None:
        """Close all sessions for a viewer."""
        response = self.session.delete(
            f'{self.base_url}/viewers/{viewer_id}/sessions'
        )
        response.raise_for_status()

    def close_all_sessions(self) -> None:
        """Close all sessions."""
        response = self.session.delete(f'{self.base_url}/sessions')
        response.raise_for_status()

    def stream_events(self, viewer_id: str):
        """
        Stream events via SSE.

        Yields:
            Event dictionaries

        Example:
            for event in client.stream_events('my-viewer'):
                if event['eventType'] == 'output':
                    print(event['text'], end='')
        """
        response = self.session.get(
            f'{self.base_url}/viewers/{viewer_id}/events',
            stream=True
        )
        response.raise_for_status()

        for line in response.iter_lines():
            if line.startswith(b'data: '):
                yield json.loads(line[6:])


# Example usage
if __name__ == '__main__':
    client = AureTTYClient('http://localhost:17850/api/v1', 'your-api-key')

    # Check health
    health = client.health()
    print('Service health:', health)

    # Create session
    session = client.create_session('my-viewer', shell='bash')
    print(f"Created session: {session['sessionId']}")

    # Send input
    client.send_input('my-viewer', session['sessionId'], 'echo hello\n', 1)
    client.send_input('my-viewer', session['sessionId'], 'ls -la\n', 2)

    # Stream events (in another thread/process)
    # for event in client.stream_events('my-viewer'):
    #     if event['eventType'] == 'output':
    #         print(event['text'], end='')

    # Close session
    client.close_session('my-viewer', session['sessionId'])
```

## WebSocket Client

```python
import json
import time
import threading
from typing import Optional, Callable, Dict, Any
from websocket import WebSocketApp

class AureTTYWebSocket:
    """WebSocket client for AureTTY."""

    def __init__(
        self,
        url: str,
        viewer_id: str,
        api_key: str,
        protocol: str = 'json'
    ):
        """
        Initialize WebSocket client.

        Args:
            url: WebSocket URL (ws://localhost:17850/api/v1)
            viewer_id: Viewer identifier
            api_key: API key for authentication
            protocol: Protocol (json or msgpack)
        """
        self.url = url.rstrip('/')
        self.viewer_id = viewer_id
        self.api_key = api_key
        self.protocol = protocol
        self.ws = None
        self.request_id = 0
        self.pending_requests = {}
        self.event_handlers = {}
        self.connected = False

    def connect(self) -> None:
        """Connect to WebSocket."""
        ws_url = f"{self.url}/viewers/{self.viewer_id}/ws?protocol={self.protocol}&api_key={self.api_key}"

        self.ws = WebSocketApp(
            ws_url,
            on_open=self._on_open,
            on_message=self._on_message,
            on_error=self._on_error,
            on_close=self._on_close
        )

        # Run in background thread
        thread = threading.Thread(target=self.ws.run_forever, daemon=True)
        thread.start()

        # Wait for connection
        timeout = 5
        start = time.time()
        while not self.connected and time.time() - start < timeout:
            time.sleep(0.1)

        if not self.connected:
            raise TimeoutError('Connection timeout')

    def _on_open(self, ws):
        """Handle connection open."""
        self.connected = True
        print('Connected')

    def _on_message(self, ws, message):
        """Handle incoming message."""
        msg = json.loads(message)

        if msg['type'] in ('response', 'error'):
            # Handle response
            request_id = msg.get('id')
            if request_id in self.pending_requests:
                callback = self.pending_requests.pop(request_id)
                if msg['type'] == 'response':
                    callback(None, msg.get('payload'))
                else:
                    callback(msg.get('error'), None)

        elif msg['type'] == 'event':
            # Handle event
            event = msg['payload']['event']
            session_id = event['sessionId']

            if session_id in self.event_handlers:
                self.event_handlers[session_id](event)

    def _on_error(self, ws, error):
        """Handle error."""
        print(f'Error: {error}')

    def _on_close(self, ws, close_status_code, close_msg):
        """Handle connection close."""
        self.connected = False
        print('Disconnected')

    def _request(
        self,
        method: str,
        payload: Optional[Dict[str, Any]] = None,
        timeout: float = 30.0
    ) -> Any:
        """
        Send request and wait for response.

        Args:
            method: IPC method name
            payload: Request payload
            timeout: Request timeout in seconds

        Returns:
            Response payload

        Raises:
            TimeoutError: If request times out
            RuntimeError: If request fails
        """
        request_id = f'req-{self.request_id}'
        self.request_id += 1

        message = {
            'type': 'request',
            'id': request_id,
            'method': method
        }

        if payload:
            message['payload'] = payload

        # Send request
        self.ws.send(json.dumps(message))

        # Wait for response
        result = {'error': None, 'payload': None, 'done': False}

        def callback(error, payload):
            result['error'] = error
            result['payload'] = payload
            result['done'] = True

        self.pending_requests[request_id] = callback

        # Wait with timeout
        start = time.time()
        while not result['done'] and time.time() - start < timeout:
            time.sleep(0.01)

        if not result['done']:
            self.pending_requests.pop(request_id, None)
            raise TimeoutError(f'Request {method} timed out')

        if result['error']:
            raise RuntimeError(f'Request {method} failed: {result["error"]}')

        return result['payload']

    def start_session(
        self,
        session_id: str,
        shell: str = 'bash',
        columns: int = 80,
        rows: int = 24,
        event_handler: Optional[Callable] = None
    ) -> Dict[str, Any]:
        """
        Start a terminal session.

        Args:
            session_id: Session identifier
            shell: Shell to launch
            columns: Terminal width
            rows: Terminal height
            event_handler: Callback for events

        Returns:
            Session handle
        """
        if event_handler:
            self.event_handlers[session_id] = event_handler

        return self._request('terminal.start', {
            'viewerId': self.viewer_id,
            'request': {
                'sessionId': session_id,
                'shell': shell,
                'columns': columns,
                'rows': rows
            }
        })

    def send_input(
        self,
        session_id: str,
        text: str,
        sequence: int
    ) -> None:
        """Send input to session."""
        self._request('terminal.input', {
            'viewerId': self.viewer_id,
            'request': {
                'sessionId': session_id,
                'text': text,
                'sequence': sequence
            }
        })

    def resize_terminal(
        self,
        session_id: str,
        columns: int,
        rows: int
    ) -> None:
        """Resize terminal."""
        self._request('terminal.resize', {
            'viewerId': self.viewer_id,
            'request': {
                'sessionId': session_id,
                'columns': columns,
                'rows': rows
            }
        })

    def close_session(self, session_id: str) -> None:
        """Close session."""
        self._request('terminal.close', {
            'viewerId': self.viewer_id,
            'sessionId': session_id
        })

        self.event_handlers.pop(session_id, None)

    def disconnect(self) -> None:
        """Disconnect WebSocket."""
        if self.ws:
            self.ws.close()


# Example usage
if __name__ == '__main__':
    def handle_event(event):
        """Handle terminal events."""
        event_type = event['eventType']

        if event_type == 'started':
            print('Session started')
        elif event_type == 'output':
            print(event['text'], end='', flush=True)
        elif event_type == 'exited':
            print(f"\nProcess exited with code {event.get('exitCode')}")
        elif event_type == 'closed':
            print('Session closed')

    # Connect
    ws = AureTTYWebSocket(
        'ws://localhost:17850/api/v1',
        'my-viewer',
        'your-api-key'
    )
    ws.connect()

    # Start session
    session = ws.start_session('session-1', 'bash', event_handler=handle_event)
    print(f"Session started: {session['sessionId']}")

    # Send commands
    ws.send_input('session-1', 'echo "Hello from Python!"\n', 1)
    ws.send_input('session-1', 'ls -la\n', 2)
    ws.send_input('session-1', 'exit\n', 3)

    # Wait for output
    time.sleep(2)

    # Close
    ws.close_session('session-1')
    ws.disconnect()
```

## Interactive Terminal

```python
import sys
import tty
import termios
import threading
from auretty_client import AureTTYWebSocket

class InteractiveTerminal:
    """Interactive terminal using AureTTY."""

    def __init__(self, ws: AureTTYWebSocket, session_id: str):
        self.ws = ws
        self.session_id = session_id
        self.sequence = 0
        self.running = False

    def start(self):
        """Start interactive terminal."""
        self.running = True

        # Start session with event handler
        self.ws.start_session(
            self.session_id,
            'bash',
            event_handler=self._handle_event
        )

        # Save terminal settings
        old_settings = termios.tcgetattr(sys.stdin)

        try:
            # Set terminal to raw mode
            tty.setraw(sys.stdin)

            # Start input thread
            input_thread = threading.Thread(target=self._read_input, daemon=True)
            input_thread.start()

            # Wait for exit
            while self.running:
                threading.Event().wait(0.1)

        finally:
            # Restore terminal settings
            termios.tcsetattr(sys.stdin, termios.TCSADRAIN, old_settings)

    def _handle_event(self, event):
        """Handle terminal events."""
        if event['eventType'] == 'output':
            sys.stdout.write(event['text'])
            sys.stdout.flush()
        elif event['eventType'] == 'exited':
            print(f"\r\nProcess exited with code {event.get('exitCode')}")
            self.running = False

    def _read_input(self):
        """Read input from stdin and send to terminal."""
        while self.running:
            try:
                char = sys.stdin.read(1)
                if char:
                    self.sequence += 1
                    self.ws.send_input(self.session_id, char, self.sequence)
            except Exception as e:
                print(f"\r\nInput error: {e}")
                break


# Example usage
if __name__ == '__main__':
    ws = AureTTYWebSocket(
        'ws://localhost:17850/api/v1',
        'my-viewer',
        'your-api-key'
    )
    ws.connect()

    terminal = InteractiveTerminal(ws, 'interactive-session')

    try:
        terminal.start()
    except KeyboardInterrupt:
        print('\r\nInterrupted')
    finally:
        ws.close_session('interactive-session')
        ws.disconnect()
```

## Async Client (asyncio)

```python
import asyncio
import json
from typing import Optional, Callable, Dict, Any
import websockets

class AsyncAureTTYClient:
    """Async WebSocket client for AureTTY."""

    def __init__(
        self,
        url: str,
        viewer_id: str,
        api_key: str,
        protocol: str = 'json'
    ):
        self.url = url.rstrip('/')
        self.viewer_id = viewer_id
        self.api_key = api_key
        self.protocol = protocol
        self.ws = None
        self.request_id = 0
        self.pending_requests = {}
        self.event_handlers = {}

    async def connect(self):
        """Connect to WebSocket."""
        ws_url = f"{self.url}/viewers/{self.viewer_id}/ws?protocol={self.protocol}&api_key={self.api_key}"
        self.ws = await websockets.connect(ws_url)

        # Start message handler
        asyncio.create_task(self._message_handler())

    async def _message_handler(self):
        """Handle incoming messages."""
        async for message in self.ws:
            msg = json.loads(message)

            if msg['type'] in ('response', 'error'):
                request_id = msg.get('id')
                if request_id in self.pending_requests:
                    future = self.pending_requests.pop(request_id)
                    if msg['type'] == 'response':
                        future.set_result(msg.get('payload'))
                    else:
                        future.set_exception(RuntimeError(msg.get('error')))

            elif msg['type'] == 'event':
                event = msg['payload']['event']
                session_id = event['sessionId']

                if session_id in self.event_handlers:
                    handler = self.event_handlers[session_id]
                    if asyncio.iscoroutinefunction(handler):
                        await handler(event)
                    else:
                        handler(event)

    async def _request(
        self,
        method: str,
        payload: Optional[Dict[str, Any]] = None,
        timeout: float = 30.0
    ) -> Any:
        """Send request and wait for response."""
        request_id = f'req-{self.request_id}'
        self.request_id += 1

        message = {
            'type': 'request',
            'id': request_id,
            'method': method
        }

        if payload:
            message['payload'] = payload

        # Create future for response
        future = asyncio.Future()
        self.pending_requests[request_id] = future

        # Send request
        await self.ws.send(json.dumps(message))

        # Wait for response with timeout
        try:
            return await asyncio.wait_for(future, timeout)
        except asyncio.TimeoutError:
            self.pending_requests.pop(request_id, None)
            raise TimeoutError(f'Request {method} timed out')

    async def start_session(
        self,
        session_id: str,
        shell: str = 'bash',
        event_handler: Optional[Callable] = None
    ) -> Dict[str, Any]:
        """Start terminal session."""
        if event_handler:
            self.event_handlers[session_id] = event_handler

        return await self._request('terminal.start', {
            'viewerId': self.viewer_id,
            'request': {
                'sessionId': session_id,
                'shell': shell
            }
        })

    async def send_input(
        self,
        session_id: str,
        text: str,
        sequence: int
    ):
        """Send input to session."""
        await self._request('terminal.input', {
            'viewerId': self.viewer_id,
            'request': {
                'sessionId': session_id,
                'text': text,
                'sequence': sequence
            }
        })

    async def close_session(self, session_id: str):
        """Close session."""
        await self._request('terminal.close', {
            'viewerId': self.viewer_id,
            'sessionId': session_id
        })

        self.event_handlers.pop(session_id, None)

    async def disconnect(self):
        """Disconnect WebSocket."""
        if self.ws:
            await self.ws.close()


# Example usage
async def main():
    client = AsyncAureTTYClient(
        'ws://localhost:17850/api/v1',
        'my-viewer',
        'your-api-key'
    )

    await client.connect()

    # Event handler
    async def handle_event(event):
        if event['eventType'] == 'output':
            print(event['text'], end='', flush=True)

    # Start session
    session = await client.start_session('session-1', 'bash', handle_event)
    print(f"Session started: {session['sessionId']}")

    # Send commands
    await client.send_input('session-1', 'echo hello\n', 1)
    await client.send_input('session-1', 'ls\n', 2)

    # Wait for output
    await asyncio.sleep(2)

    # Close
    await client.close_session('session-1')
    await client.disconnect()


if __name__ == '__main__':
    asyncio.run(main())
```

## See Also

- [REST API Reference](../../api/REST_API.md)
- [WebSocket API Reference](../../api/WEBSOCKET_API.md)
- [Getting Started Guide](../../GETTING_STARTED.md)

using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using AureTTY.Api;
using AureTTY.Contracts.Abstractions;
using AureTTY.Contracts.DTOs;
using AureTTY.Contracts.Enums;
using AureTTY.Contracts.Exceptions;
using AureTTY.Protocol;
using AureTTY.Serialization;
using AureTTY.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace AureTTY.Tests;

public sealed class WebSocketTransportTests
{
    [Fact]
    public async Task WebSocket_WhenHelloHandshakeIsMissing_ReturnsHandshakeError()
    {
        await using var host = await CreateHostAsync();
        var wsClient = host.GetTestServer().CreateWebSocketClient();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var ws = await wsClient.ConnectAsync(
            new Uri("ws://localhost/api/v1/viewers/viewer-ws/sessions/ws"),
            timeout.Token);

        var pingMessage = new TerminalIpcMessage
        {
            Type = TerminalIpcMessageTypes.Request,
            Id = "ping-without-hello",
            Method = TerminalIpcMethods.Ping
        };

        await SendMessageAsync(ws, pingMessage, timeout.Token);
        var response = await ReceiveMessageAsync(ws, timeout.Token);

        Assert.NotNull(response);
        Assert.Equal(TerminalIpcMessageTypes.Error, response.Type);
        Assert.Contains("First WebSocket message must be 'hello'", response.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WebSocket_WhenHelloTokenIsValid_AcceptsAndHandlesPing()
    {
        await using var host = await CreateHostAsync();
        var wsClient = host.GetTestServer().CreateWebSocketClient();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var ws = await wsClient.ConnectAsync(
            new Uri("ws://localhost/api/v1/viewers/viewer-ws/sessions/ws"),
            timeout.Token);
        await SendHelloAsync(ws, "test-api-key", timeout.Token);

        var pingMessage = new TerminalIpcMessage
        {
            Type = TerminalIpcMessageTypes.Request,
            Id = "ping-1",
            Method = TerminalIpcMethods.Ping
        };

        await SendMessageAsync(ws, pingMessage, timeout.Token);
        var response = await ReceiveMessageAsync(ws, timeout.Token);

        Assert.NotNull(response);
        Assert.Equal(TerminalIpcMessageTypes.Response, response.Type);
        Assert.Equal("ping-1", response.Id);
        Assert.Equal(TerminalIpcMethods.Ping, response.Method);

        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, timeout.Token);
    }

    [Fact]
    public async Task WebSocket_WhenHelloTokenIsInvalid_ReturnsUnauthorizedError()
    {
        await using var host = await CreateHostAsync();
        var wsClient = host.GetTestServer().CreateWebSocketClient();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var ws = await wsClient.ConnectAsync(
            new Uri("ws://localhost/api/v1/viewers/viewer-ws/sessions/ws"),
            timeout.Token);

        var helloMessage = new TerminalIpcMessage
        {
            Type = TerminalIpcMessageTypes.Request,
            Id = "hello-invalid",
            Method = TerminalIpcMethods.Hello,
            Payload = new TerminalIpcHelloPayload("invalid-token")
        };

        await SendMessageAsync(ws, helloMessage, timeout.Token);
        var response = await ReceiveMessageAsync(ws, timeout.Token);

        Assert.NotNull(response);
        Assert.Equal(TerminalIpcMessageTypes.Error, response.Type);
        Assert.Contains("token is invalid", response.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WebSocket_WhenStartSessionRequested_ReturnsSessionHandle()
    {
        await using var host = await CreateHostAsync();
        var wsClient = host.GetTestServer().CreateWebSocketClient();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var ws = await wsClient.ConnectAsync(
            new Uri("ws://localhost/api/v1/viewers/viewer-ws/sessions/ws"),
            timeout.Token);
        await SendHelloAsync(ws, "test-api-key", timeout.Token);

        var startPayload = new TerminalIpcStartRequest(
            "viewer-ws",
            new TerminalSessionStartRequest("session-ws-1", Shell.Bash));

        var startMessage = new TerminalIpcMessage
        {
            Type = TerminalIpcMessageTypes.Request,
            Id = "start-1",
            Method = TerminalIpcMethods.Start,
            Payload = startPayload
        };

        await SendMessageAsync(ws, startMessage, timeout.Token);
        var response = await ReceiveMessageAsync(ws, timeout.Token);

        Assert.NotNull(response);
        Assert.Equal(TerminalIpcMessageTypes.Response, response.Type);
        Assert.Equal("start-1", response.Id);
        Assert.Equal(TerminalIpcMethods.Start, response.Method);

        var handle = response.Payload is JsonElement jsonElement
            ? jsonElement.Deserialize(AureTTYJsonSerializerContext.Default.TerminalSessionHandle)
            : response.Payload as TerminalSessionHandle;
        Assert.NotNull(handle);
        Assert.Equal("session-ws-1", handle.SessionId);

        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, timeout.Token);
    }

    [Fact]
    public async Task WebSocket_WhenPayloadViewerDoesNotMatchRoute_ReturnsError()
    {
        await using var host = await CreateHostAsync();
        var wsClient = host.GetTestServer().CreateWebSocketClient();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var ws = await wsClient.ConnectAsync(
            new Uri("ws://localhost/api/v1/viewers/route-viewer/sessions/ws"),
            timeout.Token);
        await SendHelloAsync(ws, "test-api-key", timeout.Token);

        var startPayload = new TerminalIpcStartRequest(
            "payload-viewer",
            new TerminalSessionStartRequest("session-mismatch", Shell.Bash));

        var startMessage = new TerminalIpcMessage
        {
            Type = TerminalIpcMessageTypes.Request,
            Id = "start-mismatch",
            Method = TerminalIpcMethods.Start,
            Payload = startPayload
        };

        await SendMessageAsync(ws, startMessage, timeout.Token);
        var response = await ReceiveMessageAsync(ws, timeout.Token);

        Assert.NotNull(response);
        Assert.Equal(TerminalIpcMessageTypes.Error, response.Type);
        Assert.Contains("Viewer scope mismatch", response.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WebSocket_WhenEventIsPublished_ReceivesEventMessage()
    {
        await using var host = await CreateHostAsync();
        var wsClient = host.GetTestServer().CreateWebSocketClient();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var ws = await wsClient.ConnectAsync(
            new Uri("ws://localhost/api/v1/viewers/viewer-ws/sessions/ws"),
            timeout.Token);
        await SendHelloAsync(ws, "test-api-key", timeout.Token);

        var startMessage = new TerminalIpcMessage
        {
            Type = TerminalIpcMessageTypes.Request,
            Id = "start-event-1",
            Method = TerminalIpcMethods.Start,
            Payload = new TerminalIpcStartRequest(
                "viewer-ws",
                new TerminalSessionStartRequest("session-1", Shell.Bash))
        };
        await SendMessageAsync(ws, startMessage, timeout.Token);
        var startResponse = await ReceiveMessageAsync(ws, timeout.Token);
        Assert.NotNull(startResponse);
        Assert.Equal(TerminalIpcMessageTypes.Response, startResponse.Type);

        var eventPublisher = host.Services.GetRequiredService<WebSocketTerminalSessionEventPublisher>();
        var expectedEvent = new TerminalSessionEvent("session-1", TerminalSessionEventType.Output)
        {
            Text = "hello-from-ws"
        };

        var receiveTask = ReceiveMessageAsync(ws, timeout.Token);

        for (var attempt = 0; attempt < 50 && !receiveTask.IsCompleted; attempt++)
        {
            await eventPublisher.SendTerminalSessionEventAsync("viewer-ws", expectedEvent);
            await Task.Delay(50, timeout.Token);
        }

        var eventMessage = await receiveTask.WaitAsync(timeout.Token);

        Assert.NotNull(eventMessage);
        Assert.Equal(TerminalIpcMessageTypes.Event, eventMessage.Type);
        Assert.Equal(TerminalIpcMethods.SessionEvent, eventMessage.Method);

        var sessionEvent = eventMessage.Payload is JsonElement jsonElement
            ? jsonElement.Deserialize(AureTTYJsonSerializerContext.Default.TerminalIpcSessionEvent)
            : eventMessage.Payload as TerminalIpcSessionEvent;
        Assert.NotNull(sessionEvent);
        Assert.Equal("hello-from-ws", sessionEvent.Event.Text);

        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, timeout.Token);
    }

    [Fact]
    public async Task WebSocket_WhenDisconnected_UnregistersSubscription()
    {
        await using var host = await CreateHostAsync();
        var wsClient = host.GetTestServer().CreateWebSocketClient();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var ws = await wsClient.ConnectAsync(
            new Uri("ws://localhost/api/v1/viewers/viewer-ws/sessions/ws"),
            timeout.Token);
        await SendHelloAsync(ws, "test-api-key", timeout.Token);

        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, timeout.Token);
        ws.Dispose();

        // Give the handler time to clean up
        await Task.Delay(200, timeout.Token);

        // Publishing should not throw even after disconnect
        var eventPublisher = host.Services.GetRequiredService<WebSocketTerminalSessionEventPublisher>();
        await eventPublisher.SendTerminalSessionEventAsync(
            "viewer-ws",
            new TerminalSessionEvent("session-1", TerminalSessionEventType.Output)
            {
                Text = "after-disconnect"
            });
    }

    private static async Task<WebApplication> CreateHostAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(new TerminalServiceOptions(
            PipeName: "pipe-test",
            PipeToken: "pipe-token",
            EnablePipeApi: false,
            EnableHttpApi: true,
            HttpListenUrl: "http://127.0.0.1:17850",
            ApiKey: "test-api-key"));
        builder.Services.AddSingleton<ITerminalSessionService, InMemoryTerminalSessionService>();
        builder.Services.AddSingleton<WebSocketTerminalSessionEventPublisher>();

        var app = builder.Build();
        app.UseWebSockets();
        app.UseMiddleware<ApiKeyAuthenticationMiddleware>();
        app.MapTerminalHttpEndpoints();

        await app.StartAsync();
        return app;
    }

    private static async Task SendMessageAsync(WebSocket ws, TerminalIpcMessage message, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(message, AureTTYJsonSerializerContext.Default.TerminalIpcMessage);
        await ws.SendAsync(new ArraySegment<byte>(json), WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
    }

    private static async Task SendHelloAsync(WebSocket ws, string token, CancellationToken cancellationToken)
    {
        var helloMessage = new TerminalIpcMessage
        {
            Type = TerminalIpcMessageTypes.Request,
            Id = "hello-1",
            Method = TerminalIpcMethods.Hello,
            Payload = new TerminalIpcHelloPayload(token)
        };

        await SendMessageAsync(ws, helloMessage, cancellationToken);
        var helloResponse = await ReceiveMessageAsync(ws, cancellationToken);
        Assert.NotNull(helloResponse);
        Assert.Equal(TerminalIpcMessageTypes.Response, helloResponse.Type);
        Assert.Equal(TerminalIpcMethods.Hello, helloResponse.Method);
    }

    private static async Task<TerminalIpcMessage?> ReceiveMessageAsync(WebSocket ws, CancellationToken cancellationToken)
    {
        var buffer = new byte[64 * 1024];
        var totalBytes = 0;

        WebSocketReceiveResult result;
        do
        {
            result = await ws.ReceiveAsync(
                new ArraySegment<byte>(buffer, totalBytes, buffer.Length - totalBytes),
                cancellationToken);
            totalBytes += result.Count;
        }
        while (!result.EndOfMessage);

        if (result.MessageType == WebSocketMessageType.Close)
        {
            return null;
        }

        return JsonSerializer.Deserialize(
            buffer.AsSpan(0, totalBytes),
            AureTTYJsonSerializerContext.Default.TerminalIpcMessage);
    }

    private sealed class InMemoryTerminalSessionService : ITerminalSessionService
    {
        private readonly ConcurrentDictionary<string, SessionRecord> _sessions = new(StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlyCollection<TerminalSessionHandle>> GetAllSessionsAsync(CancellationToken cancellationToken = default)
        {
            var result = _sessions.Values
                .Select(static record => record.Handle)
                .ToArray();
            return Task.FromResult<IReadOnlyCollection<TerminalSessionHandle>>(result);
        }

        public Task<IReadOnlyCollection<TerminalSessionHandle>> GetViewerSessionsAsync(string viewerId, CancellationToken cancellationToken = default)
        {
            var result = _sessions.Values
                .Where(record => string.Equals(record.ViewerId, viewerId, StringComparison.Ordinal))
                .Select(static record => record.Handle)
                .ToArray();
            return Task.FromResult<IReadOnlyCollection<TerminalSessionHandle>>(result);
        }

        public Task<TerminalSessionHandle> GetSessionAsync(string viewerId, string sessionId, CancellationToken cancellationToken = default)
        {
            if (!_sessions.TryGetValue(sessionId, out var record))
            {
                throw new TerminalSessionNotFoundException($"Terminal session '{sessionId}' was not found.");
            }

            if (!string.Equals(record.ViewerId, viewerId, StringComparison.Ordinal))
            {
                throw new TerminalSessionForbiddenException("Terminal session belongs to another viewer.");
            }

            return Task.FromResult(record.Handle);
        }

        public Task<TerminalSessionHandle> StartAsync(string viewerId, TerminalSessionStartRequest request, CancellationToken cancellationToken = default)
        {
            var handle = new TerminalSessionHandle(request.SessionId)
            {
                State = TerminalSessionState.Running
            };

            if (!_sessions.TryAdd(request.SessionId, new SessionRecord(viewerId, handle)))
            {
                throw new TerminalSessionConflictException($"Terminal session '{request.SessionId}' already exists.");
            }

            return Task.FromResult(handle);
        }

        public Task<TerminalSessionHandle> ResumeAsync(string viewerId, TerminalSessionResumeRequest request, CancellationToken cancellationToken = default)
        {
            return GetSessionAsync(viewerId, request.SessionId, cancellationToken);
        }

        public Task SendInputAsync(string viewerId, TerminalSessionInputRequest request, CancellationToken cancellationToken = default)
        {
            _ = GetSessionAsync(viewerId, request.SessionId, cancellationToken);
            return Task.CompletedTask;
        }

        public Task<TerminalSessionInputDiagnostics> GetInputDiagnosticsAsync(string viewerId, string sessionId, CancellationToken cancellationToken = default)
        {
            _ = GetSessionAsync(viewerId, sessionId, cancellationToken);

            return Task.FromResult(new TerminalSessionInputDiagnostics(sessionId)
            {
                State = TerminalSessionState.Running,
                ViewerId = viewerId,
                GeneratedAtUtc = DateTimeOffset.UtcNow
            });
        }

        public Task ResizeAsync(string viewerId, TerminalSessionResizeRequest request, CancellationToken cancellationToken = default)
        {
            _ = GetSessionAsync(viewerId, request.SessionId, cancellationToken);
            return Task.CompletedTask;
        }

        public Task SignalAsync(string viewerId, string sessionId, TerminalSessionSignal signal, CancellationToken cancellationToken = default)
        {
            _ = GetSessionAsync(viewerId, sessionId, cancellationToken);
            return Task.CompletedTask;
        }

        public Task CloseAsync(string viewerId, string sessionId, CancellationToken cancellationToken = default)
        {
            _ = GetSessionAsync(viewerId, sessionId, cancellationToken);
            _sessions.TryRemove(sessionId, out _);
            return Task.CompletedTask;
        }

        public Task CloseViewerSessionsAsync(string viewerId, CancellationToken cancellationToken = default)
        {
            var sessionIds = _sessions.Values
                .Where(record => string.Equals(record.ViewerId, viewerId, StringComparison.Ordinal))
                .Select(record => record.Handle.SessionId)
                .ToArray();

            foreach (var sessionId in sessionIds)
            {
                _sessions.TryRemove(sessionId, out _);
            }

            return Task.CompletedTask;
        }

        public Task CloseAllSessionsAsync(CancellationToken cancellationToken = default)
        {
            _sessions.Clear();
            return Task.CompletedTask;
        }

        private sealed record SessionRecord(string ViewerId, TerminalSessionHandle Handle);
    }
}

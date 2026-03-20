using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;
using AureTTY.Api;
using AureTTY.Contracts.Abstractions;
using AureTTY.Contracts.DTOs;
using AureTTY.Contracts.Enums;
using AureTTY.Contracts.Exceptions;
using AureTTY.Protocol;
using AureTTY.Serialization;
using AureTTY.Services;
using MessagePack;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace AureTTY.Tests;

public sealed class WebSocketMessagePackTests
{
    [Fact]
    public async Task WebSocket_WhenMessagePackProtocol_HandlesPingRequest()
    {
        await using var host = await CreateHostAsync();
        var wsClient = host.GetTestServer().CreateWebSocketClient();
        wsClient.ConfigureRequest = request =>
        {
            request.Headers[TerminalServiceOptions.ApiKeyHeaderName] = "test-api-key";
        };

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var ws = await wsClient.ConnectAsync(
            new Uri("ws://localhost/api/v1/viewers/viewer-msgpack/ws?protocol=msgpack"),
            timeout.Token);

        var pingMessage = new TerminalIpcMessage
        {
            Type = TerminalIpcMessageTypes.Request,
            Id = "ping-msgpack",
            Method = TerminalIpcMethods.Ping
        };

        await SendMessagePackAsync(ws, pingMessage, timeout.Token);
        var response = await ReceiveMessagePackAsync(ws, timeout.Token);

        Assert.NotNull(response);
        Assert.Equal(TerminalIpcMessageTypes.Response, response.Type);
        Assert.Equal("ping-msgpack", response.Id);
        Assert.Equal(TerminalIpcMethods.Ping, response.Method);

        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, timeout.Token);
    }

    [Fact]
    public async Task WebSocket_WhenMessagePackProtocol_StartsSession()
    {
        await using var host = await CreateHostAsync();
        var wsClient = host.GetTestServer().CreateWebSocketClient();
        wsClient.ConfigureRequest = request =>
        {
            request.Headers[TerminalServiceOptions.ApiKeyHeaderName] = "test-api-key";
        };

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var ws = await wsClient.ConnectAsync(
            new Uri("ws://localhost/api/v1/viewers/viewer-msgpack/ws?protocol=messagepack"),
            timeout.Token);

        var startPayload = new TerminalIpcStartRequest(
            "viewer-msgpack",
            new TerminalSessionStartRequest("session-msgpack-1", Shell.Bash));

        var startMessage = new TerminalIpcMessage
        {
            Type = TerminalIpcMessageTypes.Request,
            Id = "start-msgpack",
            Method = TerminalIpcMethods.Start,
            Payload = startPayload
        };

        await SendMessagePackAsync(ws, startMessage, timeout.Token);
        var response = await ReceiveMessagePackAsync(ws, timeout.Token);

        Assert.NotNull(response);

        if (response.Type == TerminalIpcMessageTypes.Error)
        {
            throw new InvalidOperationException($"Server returned error: {response.Error}");
        }

        Assert.Equal(TerminalIpcMessageTypes.Response, response.Type);
        Assert.Equal("start-msgpack", response.Id);

        if (response.Payload is null)
        {
            throw new InvalidOperationException("Response payload is null");
        }

        TerminalSessionHandle? handle = null;
        if (response.Payload is TerminalSessionHandle typedHandle)
        {
            handle = typedHandle;
        }
        else if (response.Payload is object[] or byte[])
        {
            var bytes = response.Payload is byte[] b ? b : MessagePackSerializer.Serialize(response.Payload, MessagePackSerializerOptions.Standard);
            handle = MessagePackSerializer.Deserialize<TerminalSessionHandle>(bytes, MessagePackSerializerOptions.Standard);
        }

        Assert.NotNull(handle);
        Assert.Equal("session-msgpack-1", handle.SessionId);

        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, timeout.Token);
    }

    [Fact]
    public async Task WebSocket_WhenMessagePackProtocol_ReceivesEvents()
    {
        await using var host = await CreateHostAsync();
        var wsClient = host.GetTestServer().CreateWebSocketClient();
        wsClient.ConfigureRequest = request =>
        {
            request.Headers[TerminalServiceOptions.ApiKeyHeaderName] = "test-api-key";
        };

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var ws = await wsClient.ConnectAsync(
            new Uri("ws://localhost/api/v1/viewers/viewer-msgpack/ws?protocol=msgpack"),
            timeout.Token);

        var eventPublisher = host.Services.GetRequiredService<WebSocketTerminalSessionEventPublisher>();
        var expectedEvent = new TerminalSessionEvent("session-msgpack", TerminalSessionEventType.Output)
        {
            Text = "msgpack-event-test"
        };

        var receiveTask = ReceiveMessagePackAsync(ws, timeout.Token);

        for (var attempt = 0; attempt < 50 && !receiveTask.IsCompleted; attempt++)
        {
            await eventPublisher.SendTerminalSessionEventAsync("viewer-msgpack", expectedEvent);
            await Task.Delay(50, timeout.Token);
        }

        var eventMessage = await receiveTask.WaitAsync(timeout.Token);

        Assert.NotNull(eventMessage);
        Assert.Equal(TerminalIpcMessageTypes.Event, eventMessage.Type);
        Assert.Equal(TerminalIpcMethods.SessionEvent, eventMessage.Method);

        TerminalIpcSessionEvent? sessionEvent = null;
        if (eventMessage.Payload is TerminalIpcSessionEvent typedEvent)
        {
            sessionEvent = typedEvent;
        }
        else if (eventMessage.Payload is object[] or byte[])
        {
            var bytes = eventMessage.Payload is byte[] b ? b : MessagePackSerializer.Serialize(eventMessage.Payload, MessagePackSerializerOptions.Standard);
            sessionEvent = MessagePackSerializer.Deserialize<TerminalIpcSessionEvent>(bytes, MessagePackSerializerOptions.Standard);
        }

        Assert.NotNull(sessionEvent);
        Assert.Equal("msgpack-event-test", sessionEvent.Event.Text);

        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, timeout.Token);
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
        builder.Services.AddSingleton<HttpTerminalSessionEventPublisher>();
        builder.Services.AddSingleton<WebSocketTerminalSessionEventPublisher>();

        var app = builder.Build();
        app.UseWebSockets();
        app.UseMiddleware<ApiKeyAuthenticationMiddleware>();
        app.MapTerminalHttpEndpoints();

        await app.StartAsync();
        return app;
    }

    private static async Task SendMessagePackAsync(WebSocket ws, TerminalIpcMessage message, CancellationToken cancellationToken)
    {
        var data = MessagePackSerializer.Serialize(message, MessagePackSerializerOptions.Standard);
        await ws.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Binary, endOfMessage: true, cancellationToken);
    }

    private static async Task<TerminalIpcMessage?> ReceiveMessagePackAsync(WebSocket ws, CancellationToken cancellationToken)
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

        return MessagePackSerializer.Deserialize<TerminalIpcMessage>(
            buffer.AsSpan(0, totalBytes).ToArray(),
            MessagePackSerializerOptions.Standard);
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

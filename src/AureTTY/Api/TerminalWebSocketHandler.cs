using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AureTTY.Contracts.Abstractions;
using AureTTY.Contracts.DTOs;
using AureTTY.Contracts.Enums;
using AureTTY.Contracts.Exceptions;
using AureTTY.Protocol;
using AureTTY.Serialization;
using AureTTY.Services;
using MessagePack;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AureTTY.Api;

public static class TerminalWebSocketHandler
{
    private const int MaxMessageSize = 64 * 1024;
    private const string SystemSessionId = "__auretty__";

    public static async Task HandleAsync(string viewerId, HttpContext context, bool multiplexing = false)
    {
        var options = context.RequestServices.GetRequiredService<TerminalServiceOptions>();
        var loggerFactory = context.RequestServices.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("AureTTY.WebSocket");

        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        if (string.IsNullOrWhiteSpace(viewerId))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        if (!ApiKeyAuthorization.IsAuthorized(context, options))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var protocol = ParseProtocol(context);
        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        var connectionId = Guid.NewGuid().ToString("N");

        var eventPublisher = context.RequestServices.GetRequiredService<WebSocketTerminalSessionEventPublisher>();
        var sessionService = context.RequestServices.GetRequiredService<ITerminalSessionService>();

        var sessionTracking = multiplexing ? new ConcurrentDictionary<string, string>(StringComparer.Ordinal) : null;
        var eventChannel = multiplexing
            ? eventPublisher.RegisterMultiplexedConnection(connectionId, viewerId)
            : eventPublisher.RegisterConnection(connectionId, viewerId);
        var writeLock = new SemaphoreSlim(1, 1);

        try
        {
            using var connectionCts = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
            var cancellationToken = connectionCts.Token;

            var inboundTask = ProcessInboundAsync(webSocket, sessionService, eventPublisher, connectionId, viewerId, protocol, sessionTracking, writeLock, logger, cancellationToken);
            var outboundTask = ProcessOutboundAsync(webSocket, eventChannel, eventPublisher, connectionId, protocol, writeLock, cancellationToken);

            await Task.WhenAny(inboundTask, outboundTask);
            await connectionCts.CancelAsync();

            await Task.WhenAll(
                SafeAwait(inboundTask),
                SafeAwait(outboundTask));
        }
        finally
        {
            if (sessionTracking is not null)
            {
                foreach (var sessionId in sessionTracking.Keys)
                {
                    eventPublisher.UnregisterSessionSubscription(connectionId, sessionId);
                }
            }

            eventPublisher.UnregisterConnection(connectionId);
            writeLock.Dispose();

            if (webSocket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                try
                {
                    using var closeCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, closeCts.Token);
                }
                catch
                {
                    // Best-effort close.
                }
            }

            webSocket.Dispose();
        }
    }

    private static async Task ProcessInboundAsync(
        WebSocket webSocket,
        ITerminalSessionService sessionService,
        WebSocketTerminalSessionEventPublisher eventPublisher,
        string connectionId,
        string viewerId,
        IpcProtocol protocol,
        ConcurrentDictionary<string, string>? sessionTracking,
        SemaphoreSlim writeLock,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[MaxMessageSize];

        while (!cancellationToken.IsCancellationRequested)
        {
            WebSocketReceiveResult result;
            int totalBytes = 0;

            do
            {
                if (totalBytes >= buffer.Length)
                {
                    await SendErrorAsync(webSocket, writeLock, protocol, null, "Message too large.", cancellationToken);
                    return;
                }

                result = await webSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer, totalBytes, buffer.Length - totalBytes),
                    cancellationToken);
                totalBytes += result.Count;
            }
            while (!result.EndOfMessage);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                return;
            }

            var expectedMessageType = protocol == IpcProtocol.MessagePack
                ? WebSocketMessageType.Binary
                : WebSocketMessageType.Text;

            if (result.MessageType != expectedMessageType)
            {
                continue;
            }

            TerminalIpcMessage? message;
            try
            {
                message = IpcMessageSerializer.Deserialize(buffer.AsSpan(0, totalBytes), protocol);
            }
            catch (Exception)
            {
                await SendErrorAsync(webSocket, writeLock, protocol, null, "Invalid message format.", cancellationToken);
                continue;
            }

            if (message is null || !string.Equals(message.Type, TerminalIpcMessageTypes.Request, StringComparison.Ordinal))
            {
                continue;
            }

            var response = await HandleRequestAsync(message, viewerId, sessionService, eventPublisher, connectionId, sessionTracking, logger);
            await WriteMessageAsync(webSocket, writeLock, protocol, response, cancellationToken);
        }
    }

    private static async Task ProcessOutboundAsync(
        WebSocket webSocket,
        System.Threading.Channels.ChannelReader<TerminalSessionEvent> eventChannel,
        WebSocketTerminalSessionEventPublisher eventPublisher,
        string connectionId,
        IpcProtocol protocol,
        SemaphoreSlim writeLock,
        CancellationToken cancellationToken)
    {
        await foreach (var terminalEvent in eventChannel.ReadAllAsync(cancellationToken))
        {
            var droppedEvents = eventPublisher.ConsumeDroppedEvents(connectionId);
            if (droppedEvents > 0)
            {
                var droppedNotification = new TerminalSessionEvent(SystemSessionId, TerminalSessionEventType.Dropped)
                {
                    State = TerminalSessionState.Running,
                    Error = $"Dropped {droppedEvents} WebSocket event(s) because subscriber is slow."
                };
                await WriteEventAsync(webSocket, writeLock, protocol, "__dropped__", droppedNotification, cancellationToken);
            }

            await WriteEventAsync(webSocket, writeLock, protocol, terminalEvent.SessionId, terminalEvent, cancellationToken);
        }
    }

    private static async Task<TerminalIpcMessage> HandleRequestAsync(
        TerminalIpcMessage message,
        string viewerId,
        ITerminalSessionService sessionService,
        WebSocketTerminalSessionEventPublisher eventPublisher,
        string connectionId,
        ConcurrentDictionary<string, string>? sessionTracking,
        ILogger logger)
    {
        var method = message.Method;
        if (string.IsNullOrWhiteSpace(method))
        {
            return CreateError(message, "Request method is required.");
        }

        try
        {
            switch (method)
            {
                case TerminalIpcMethods.Ping:
                    return CreateResponse(message, new TerminalIpcAck());

                case TerminalIpcMethods.Start:
                    {
                        var payload = RequirePayload<TerminalIpcStartRequest>(message);
                        EnsureViewerScope(viewerId, payload.ViewerId, method);
                        var handle = await sessionService.StartAsync(viewerId, payload.Request);

                        if (sessionTracking is not null)
                        {
                            sessionTracking.TryAdd(handle.SessionId, viewerId);
                            eventPublisher.RegisterSessionSubscription(connectionId, handle.SessionId, viewerId);
                        }

                        return CreateResponse(message, handle);
                    }

                case TerminalIpcMethods.Resume:
                    {
                        var payload = RequirePayload<TerminalIpcResumeRequest>(message);
                        EnsureViewerScope(viewerId, payload.ViewerId, method);
                        var handle = await sessionService.ResumeAsync(viewerId, payload.Request);

                        if (sessionTracking is not null)
                        {
                            sessionTracking.TryAdd(handle.SessionId, viewerId);
                            eventPublisher.RegisterSessionSubscription(connectionId, handle.SessionId, viewerId);
                        }

                        return CreateResponse(message, handle);
                    }

                case TerminalIpcMethods.SendInput:
                    {
                        var payload = RequirePayload<TerminalIpcInputRequest>(message);
                        EnsureViewerScope(viewerId, payload.ViewerId, method);
                        await sessionService.SendInputAsync(viewerId, payload.Request);
                        return CreateResponse(message, new TerminalIpcAck());
                    }

                case TerminalIpcMethods.GetInputDiagnostics:
                    {
                        var payload = RequirePayload<TerminalIpcInputDiagnosticsRequest>(message);
                        EnsureViewerScope(viewerId, payload.ViewerId, method);
                        var diagnostics = await sessionService.GetInputDiagnosticsAsync(viewerId, payload.SessionId);
                        return CreateResponse(message, diagnostics);
                    }

                case TerminalIpcMethods.Resize:
                    {
                        var payload = RequirePayload<TerminalIpcResizeRequest>(message);
                        EnsureViewerScope(viewerId, payload.ViewerId, method);
                        await sessionService.ResizeAsync(viewerId, payload.Request);
                        return CreateResponse(message, new TerminalIpcAck());
                    }

                case TerminalIpcMethods.Signal:
                    {
                        var payload = RequirePayload<TerminalIpcSignalRequest>(message);
                        EnsureViewerScope(viewerId, payload.ViewerId, method);
                        await sessionService.SignalAsync(viewerId, payload.SessionId, payload.Signal);
                        return CreateResponse(message, new TerminalIpcAck());
                    }

                case TerminalIpcMethods.Close:
                    {
                        var payload = RequirePayload<TerminalIpcCloseRequest>(message);
                        EnsureViewerScope(viewerId, payload.ViewerId, method);
                        await sessionService.CloseAsync(viewerId, payload.SessionId);

                        if (sessionTracking is not null)
                        {
                            sessionTracking.TryRemove(payload.SessionId, out _);
                            eventPublisher.UnregisterSessionSubscription(connectionId, payload.SessionId);
                        }

                        return CreateResponse(message, new TerminalIpcAck());
                    }

                case TerminalIpcMethods.CloseViewerSessions:
                    {
                        var payload = RequirePayload<TerminalIpcCloseViewerSessionsRequest>(message);
                        EnsureViewerScope(viewerId, payload.ViewerId, method);
                        await sessionService.CloseViewerSessionsAsync(viewerId);

                        if (sessionTracking is not null)
                        {
                            var sessionsToRemove = sessionTracking
                                .Where(kvp => string.Equals(kvp.Value, viewerId, StringComparison.Ordinal))
                                .Select(kvp => kvp.Key)
                                .ToArray();

                            foreach (var sessionId in sessionsToRemove)
                            {
                                sessionTracking.TryRemove(sessionId, out _);
                                eventPublisher.UnregisterSessionSubscription(connectionId, sessionId);
                            }
                        }

                        return CreateResponse(message, new TerminalIpcAck());
                    }

                case TerminalIpcMethods.CloseAllSessions:
                    {
                        await sessionService.CloseAllSessionsAsync();

                        if (sessionTracking is not null)
                        {
                            foreach (var sessionId in sessionTracking.Keys)
                            {
                                eventPublisher.UnregisterSessionSubscription(connectionId, sessionId);
                            }
                            sessionTracking.Clear();
                        }

                        return CreateResponse(message, new TerminalIpcAck());
                    }

                default:
                    return CreateError(message, $"Unsupported terminal IPC method '{method}'.");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "WebSocket request failed. Method={Method}.", method);
            return CreateError(message, ex.GetBaseException().Message);
        }
    }

    private static void EnsureViewerScope(string routeViewerId, string payloadViewerId, string method)
    {
        if (string.Equals(routeViewerId, payloadViewerId, StringComparison.Ordinal))
        {
            return;
        }

        throw new TerminalSessionForbiddenException(
            $"Viewer scope mismatch for method '{method}'. Route viewer '{routeViewerId}' does not match payload viewer '{payloadViewerId}'.");
    }

    private static IpcProtocol ParseProtocol(HttpContext context)
    {
        if (context.Request.Query.TryGetValue("protocol", out var protocolValues))
        {
            var protocolValue = protocolValues.FirstOrDefault();
            if (string.Equals(protocolValue, "msgpack", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(protocolValue, "messagepack", StringComparison.OrdinalIgnoreCase))
            {
                return IpcProtocol.MessagePack;
            }
        }

        return IpcProtocol.Json;
    }

    private static T RequirePayload<T>(TerminalIpcMessage message)
    {
        if (message.Payload is null)
        {
            throw new InvalidOperationException($"Request payload for '{message.Method}' is missing or invalid.");
        }

        if (message.Payload is T typedPayload)
        {
            return typedPayload;
        }

        if (message.Payload is JsonElement jsonElement)
        {
            var jsonTypeInfo = typeof(T).Name switch
            {
                nameof(TerminalIpcStartRequest) => (JsonTypeInfo<T>)(object)AureTTYJsonSerializerContext.Default.TerminalIpcStartRequest,
                nameof(TerminalIpcResumeRequest) => (JsonTypeInfo<T>)(object)AureTTYJsonSerializerContext.Default.TerminalIpcResumeRequest,
                nameof(TerminalIpcInputRequest) => (JsonTypeInfo<T>)(object)AureTTYJsonSerializerContext.Default.TerminalIpcInputRequest,
                nameof(TerminalIpcInputDiagnosticsRequest) => (JsonTypeInfo<T>)(object)AureTTYJsonSerializerContext.Default.TerminalIpcInputDiagnosticsRequest,
                nameof(TerminalIpcResizeRequest) => (JsonTypeInfo<T>)(object)AureTTYJsonSerializerContext.Default.TerminalIpcResizeRequest,
                nameof(TerminalIpcSignalRequest) => (JsonTypeInfo<T>)(object)AureTTYJsonSerializerContext.Default.TerminalIpcSignalRequest,
                nameof(TerminalIpcCloseRequest) => (JsonTypeInfo<T>)(object)AureTTYJsonSerializerContext.Default.TerminalIpcCloseRequest,
                nameof(TerminalIpcCloseViewerSessionsRequest) => (JsonTypeInfo<T>)(object)AureTTYJsonSerializerContext.Default.TerminalIpcCloseViewerSessionsRequest,
                _ => throw new InvalidOperationException($"Unknown payload type '{typeof(T).Name}'.")
            };

            return jsonElement.Deserialize(jsonTypeInfo)
                   ?? throw new InvalidOperationException($"Request payload for '{message.Method}' is missing or invalid.");
        }

        if (message.Payload is object[] or byte[])
        {
            var bytes = message.Payload is byte[] b ? b : MessagePackSerializer.Serialize(message.Payload, MessagePackSerializerOptions.Standard);
            return MessagePackSerializer.Deserialize<T>(bytes, MessagePackSerializerOptions.Standard)
                   ?? throw new InvalidOperationException($"Request payload for '{message.Method}' is missing or invalid.");
        }

        throw new InvalidOperationException($"Request payload for '{message.Method}' has unexpected type '{message.Payload.GetType().Name}'.");
    }

    private static TerminalIpcMessage CreateResponse<T>(TerminalIpcMessage request, T payload)
    {
        return new TerminalIpcMessage
        {
            Type = TerminalIpcMessageTypes.Response,
            Id = request.Id,
            Method = request.Method,
            Payload = payload
        };
    }

    private static TerminalIpcMessage CreateError(TerminalIpcMessage request, string error)
    {
        return new TerminalIpcMessage
        {
            Type = TerminalIpcMessageTypes.Error,
            Id = request.Id,
            Method = request.Method,
            Error = error
        };
    }

    private static async Task WriteEventAsync(
        WebSocket webSocket,
        SemaphoreSlim writeLock,
        IpcProtocol protocol,
        string viewerId,
        TerminalSessionEvent terminalEvent,
        CancellationToken cancellationToken)
    {
        var eventPayload = new TerminalIpcSessionEvent(viewerId, terminalEvent);
        var message = new TerminalIpcMessage
        {
            Type = TerminalIpcMessageTypes.Event,
            Method = TerminalIpcMethods.SessionEvent,
            Payload = eventPayload
        };

        await WriteMessageAsync(webSocket, writeLock, protocol, message, cancellationToken);
    }

    private static async Task WriteMessageAsync(
        WebSocket webSocket,
        SemaphoreSlim writeLock,
        IpcProtocol protocol,
        TerminalIpcMessage message,
        CancellationToken cancellationToken)
    {
        var data = IpcMessageSerializer.Serialize(message, protocol);
        var messageType = protocol == IpcProtocol.MessagePack
            ? WebSocketMessageType.Binary
            : WebSocketMessageType.Text;

        await writeLock.WaitAsync(cancellationToken);
        try
        {
            await webSocket.SendAsync(
                new ArraySegment<byte>(data),
                messageType,
                endOfMessage: true,
                cancellationToken);
        }
        finally
        {
            writeLock.Release();
        }
    }

    private static async Task SendErrorAsync(
        WebSocket webSocket,
        SemaphoreSlim writeLock,
        IpcProtocol protocol,
        string? id,
        string error,
        CancellationToken cancellationToken)
    {
        var message = new TerminalIpcMessage
        {
            Type = TerminalIpcMessageTypes.Error,
            Id = id,
            Error = error
        };

        await WriteMessageAsync(webSocket, writeLock, protocol, message, cancellationToken);
    }

    private static async Task SafeAwait(Task task)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
            // Expected on disconnect.
        }
    }
}

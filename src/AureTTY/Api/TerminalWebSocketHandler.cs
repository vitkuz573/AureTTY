using System.Buffers;
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

        var protocol = ParseProtocol(context);
        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        var connectionId = Guid.NewGuid().ToString("N");
        var writeLock = new SemaphoreSlim(1, 1);
        var receiveBuffer = ArrayPool<byte>.Shared.Rent(MaxMessageSize);
        System.Threading.Channels.ChannelReader<TerminalSessionEvent> eventChannel;
        ConcurrentDictionary<string, string>? sessionTracking = null;
        var isRegistered = false;

        try
        {
            var authenticated = await AuthenticateConnectionAsync(
                webSocket,
                receiveBuffer,
                protocol,
                options,
                writeLock,
                context.RequestAborted);
            if (!authenticated)
            {
                return;
            }

            var eventPublisher = context.RequestServices.GetRequiredService<WebSocketTerminalSessionEventPublisher>();
            var sessionService = context.RequestServices.GetRequiredService<ITerminalSessionService>();
            sessionTracking = multiplexing ? new ConcurrentDictionary<string, string>(StringComparer.Ordinal) : null;
            eventChannel = multiplexing
                ? eventPublisher.RegisterMultiplexedConnection(connectionId, viewerId)
                : eventPublisher.RegisterConnection(connectionId, viewerId);
            isRegistered = true;

            using var connectionCts = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
            var cancellationToken = connectionCts.Token;

            var inboundTask = ProcessInboundAsync(
                webSocket,
                receiveBuffer,
                sessionService,
                eventPublisher,
                connectionId,
                viewerId,
                protocol,
                sessionTracking,
                writeLock,
                logger,
                cancellationToken);
            var outboundTask = ProcessOutboundAsync(webSocket, eventChannel, eventPublisher, connectionId, protocol, writeLock, cancellationToken);

            await Task.WhenAny(inboundTask, outboundTask);
            await connectionCts.CancelAsync();

            await Task.WhenAll(
                SafeAwait(inboundTask),
                SafeAwait(outboundTask));
        }
        finally
        {
            var eventPublisher = isRegistered || sessionTracking is not null
                ? context.RequestServices.GetRequiredService<WebSocketTerminalSessionEventPublisher>()
                : null;

            if (sessionTracking is not null)
            {
                foreach (var sessionId in sessionTracking.Keys)
                {
                    eventPublisher!.UnregisterSessionSubscription(connectionId, sessionId);
                }
            }

            if (isRegistered)
            {
                eventPublisher!.UnregisterConnection(connectionId);
            }
            writeLock.Dispose();
            ArrayPool<byte>.Shared.Return(receiveBuffer);

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
        byte[] buffer,
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
        while (!cancellationToken.IsCancellationRequested)
        {
            var receiveResult = await ReceiveIpcMessageAsync(webSocket, buffer, protocol, cancellationToken);
            if (receiveResult.CloseRequested)
            {
                return;
            }

            if (receiveResult.Error is not null)
            {
                await SendErrorAsync(webSocket, writeLock, protocol, null, receiveResult.Error, cancellationToken);
                continue;
            }

            var message = receiveResult.Message;
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

    private static async Task<bool> AuthenticateConnectionAsync(
        WebSocket webSocket,
        byte[] buffer,
        IpcProtocol protocol,
        TerminalServiceOptions options,
        SemaphoreSlim writeLock,
        CancellationToken cancellationToken)
    {
        ReceiveMessageResult helloMessageResult;
        using (var helloTimeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            helloTimeoutSource.CancelAfter(options.WebSocketHelloTimeout);

            try
            {
                helloMessageResult = await ReceiveIpcMessageAsync(webSocket, buffer, protocol, helloTimeoutSource.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                await SendErrorAsync(webSocket, writeLock, protocol, null, "WebSocket hello handshake timed out.", cancellationToken);
                await CloseWithPolicyViolationAsync(webSocket, "hello-timeout");
                return false;
            }
        }

        if (helloMessageResult.CloseRequested)
        {
            return false;
        }

        if (helloMessageResult.Error is not null)
        {
            await SendErrorAsync(webSocket, writeLock, protocol, null, helloMessageResult.Error, cancellationToken);
            await CloseWithPolicyViolationAsync(webSocket, "invalid-hello");
            return false;
        }

        var helloMessage = helloMessageResult.Message;
        if (helloMessage is null ||
            !string.Equals(helloMessage.Type, TerminalIpcMessageTypes.Request, StringComparison.Ordinal) ||
            !string.Equals(helloMessage.Method, TerminalIpcMethods.Hello, StringComparison.Ordinal))
        {
            await SendErrorAsync(webSocket, writeLock, protocol, helloMessage?.Id, "First WebSocket message must be 'hello' request.", cancellationToken);
            await CloseWithPolicyViolationAsync(webSocket, "hello-required");
            return false;
        }

        TerminalIpcHelloPayload helloPayload;
        try
        {
            helloPayload = RequirePayload<TerminalIpcHelloPayload>(helloMessage);
        }
        catch (Exception)
        {
            await SendErrorAsync(webSocket, writeLock, protocol, helloMessage.Id, "Hello payload is missing or invalid.", cancellationToken);
            await CloseWithPolicyViolationAsync(webSocket, "invalid-hello");
            return false;
        }

        if (!ApiKeyAuthorization.IsApiKeyValid(helloPayload.Token, options))
        {
            await SendErrorAsync(webSocket, writeLock, protocol, helloMessage.Id, "WebSocket hello token is invalid.", cancellationToken);
            await CloseWithPolicyViolationAsync(webSocket, "unauthorized");
            return false;
        }

        if (helloPayload.ProtocolVersion != 1)
        {
            await SendErrorAsync(webSocket, writeLock, protocol, helloMessage.Id, "Unsupported hello protocol version.", cancellationToken);
            await CloseWithPolicyViolationAsync(webSocket, "unsupported-protocol");
            return false;
        }

        await WriteMessageAsync(webSocket, writeLock, protocol, CreateResponse(helloMessage, new TerminalIpcAck()), cancellationToken);
        return true;
    }

    private static async Task<ReceiveMessageResult> ReceiveIpcMessageAsync(
        WebSocket webSocket,
        byte[] buffer,
        IpcProtocol protocol,
        CancellationToken cancellationToken)
    {
        WebSocketReceiveResult result;
        int totalBytes = 0;

        do
        {
            if (totalBytes >= buffer.Length)
            {
                return ReceiveMessageResult.FromError("Message too large.");
            }

            result = await webSocket.ReceiveAsync(
                new ArraySegment<byte>(buffer, totalBytes, buffer.Length - totalBytes),
                cancellationToken);
            totalBytes += result.Count;
        }
        while (!result.EndOfMessage);

        if (result.MessageType == WebSocketMessageType.Close)
        {
            return ReceiveMessageResult.Close;
        }

        var expectedMessageType = protocol == IpcProtocol.MessagePack
            ? WebSocketMessageType.Binary
            : WebSocketMessageType.Text;
        if (result.MessageType != expectedMessageType)
        {
            return ReceiveMessageResult.FromError("Unexpected WebSocket message type.");
        }

        TerminalIpcMessage? message;
        try
        {
            message = IpcMessageSerializer.Deserialize(buffer.AsMemory(0, totalBytes), protocol);
        }
        catch (Exception)
        {
            return ReceiveMessageResult.FromError("Invalid message format.");
        }

        return new ReceiveMessageResult(message, CloseRequested: false, Error: null);
    }

    private static async Task CloseWithPolicyViolationAsync(WebSocket webSocket, string reason)
    {
        if (webSocket.State is not (WebSocketState.Open or WebSocketState.CloseReceived))
        {
            return;
        }

        try
        {
            using var closeSource = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await webSocket.CloseAsync(WebSocketCloseStatus.PolicyViolation, reason, closeSource.Token);
        }
        catch
        {
            // Best-effort close.
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
                case TerminalIpcMethods.Hello:
                    return CreateError(message, "WebSocket hello handshake is only allowed as the first message.");

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
                nameof(TerminalIpcHelloPayload) => (JsonTypeInfo<T>)(object)AureTTYJsonSerializerContext.Default.TerminalIpcHelloPayload,
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

    private readonly record struct ReceiveMessageResult(TerminalIpcMessage? Message, bool CloseRequested, string? Error)
    {
        public static ReceiveMessageResult Close { get; } = new(null, CloseRequested: true, Error: null);

        public static ReceiveMessageResult FromError(string error) => new(null, CloseRequested: false, error);
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

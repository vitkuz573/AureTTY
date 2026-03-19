using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AureTTY.Contracts.Abstractions;
using AureTTY.Contracts.DTOs;
using AureTTY.Contracts.Enums;
using AureTTY.Protocol;
using AureTTY.Serialization;
using AureTTY.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AureTTY.Api;

public static class TerminalWebSocketHandler
{
    private const int MaxMessageSize = 64 * 1024;
    private const string SystemSessionId = "__auretty__";

    public static async Task HandleAsync(string viewerId, HttpContext context)
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

        if (!IsAuthorized(context, options))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        var connectionId = Guid.NewGuid().ToString("N");

        var eventPublisher = context.RequestServices.GetRequiredService<WebSocketTerminalSessionEventPublisher>();
        var sessionService = context.RequestServices.GetRequiredService<ITerminalSessionService>();

        var eventChannel = eventPublisher.RegisterConnection(connectionId, viewerId);
        var writeLock = new SemaphoreSlim(1, 1);

        try
        {
            using var connectionCts = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
            var cancellationToken = connectionCts.Token;

            var inboundTask = ProcessInboundAsync(webSocket, sessionService, eventPublisher, connectionId, viewerId, writeLock, logger, cancellationToken);
            var outboundTask = ProcessOutboundAsync(webSocket, eventChannel, eventPublisher, connectionId, writeLock, cancellationToken);

            await Task.WhenAny(inboundTask, outboundTask);
            await connectionCts.CancelAsync();

            await Task.WhenAll(
                SafeAwait(inboundTask),
                SafeAwait(outboundTask));
        }
        finally
        {
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
                    await SendErrorAsync(webSocket, writeLock, null, "Message too large.", cancellationToken);
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

            if (result.MessageType != WebSocketMessageType.Text)
            {
                continue;
            }

            TerminalIpcMessage? message;
            try
            {
                message = JsonSerializer.Deserialize(
                    buffer.AsSpan(0, totalBytes),
                    AureTTYJsonSerializerContext.Default.TerminalIpcMessage);
            }
            catch (JsonException)
            {
                await SendErrorAsync(webSocket, writeLock, null, "Invalid JSON.", cancellationToken);
                continue;
            }

            if (message is null || !string.Equals(message.Type, TerminalIpcMessageTypes.Request, StringComparison.Ordinal))
            {
                continue;
            }

            var response = await HandleRequestAsync(message, viewerId, sessionService, logger);
            await WriteMessageAsync(webSocket, writeLock, response, cancellationToken);
        }
    }

    private static async Task ProcessOutboundAsync(
        WebSocket webSocket,
        System.Threading.Channels.ChannelReader<TerminalSessionEvent> eventChannel,
        WebSocketTerminalSessionEventPublisher eventPublisher,
        string connectionId,
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
                await WriteEventAsync(webSocket, writeLock, "__dropped__", droppedNotification, cancellationToken);
            }

            await WriteEventAsync(webSocket, writeLock, terminalEvent.SessionId, terminalEvent, cancellationToken);
        }
    }

    private static async Task<TerminalIpcMessage> HandleRequestAsync(
        TerminalIpcMessage message,
        string viewerId,
        ITerminalSessionService sessionService,
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
                    return CreateResponse(message, new TerminalIpcAck(), AureTTYJsonSerializerContext.Default.TerminalIpcAck);

                case TerminalIpcMethods.Start:
                    {
                        var payload = RequirePayload(message, AureTTYJsonSerializerContext.Default.TerminalIpcStartRequest);
                        var handle = await sessionService.StartAsync(payload.ViewerId, payload.Request);
                        return CreateResponse(message, handle, AureTTYJsonSerializerContext.Default.TerminalSessionHandle);
                    }

                case TerminalIpcMethods.Resume:
                    {
                        var payload = RequirePayload(message, AureTTYJsonSerializerContext.Default.TerminalIpcResumeRequest);
                        var handle = await sessionService.ResumeAsync(payload.ViewerId, payload.Request);
                        return CreateResponse(message, handle, AureTTYJsonSerializerContext.Default.TerminalSessionHandle);
                    }

                case TerminalIpcMethods.SendInput:
                    {
                        var payload = RequirePayload(message, AureTTYJsonSerializerContext.Default.TerminalIpcInputRequest);
                        await sessionService.SendInputAsync(payload.ViewerId, payload.Request);
                        return CreateResponse(message, new TerminalIpcAck(), AureTTYJsonSerializerContext.Default.TerminalIpcAck);
                    }

                case TerminalIpcMethods.GetInputDiagnostics:
                    {
                        var payload = RequirePayload(message, AureTTYJsonSerializerContext.Default.TerminalIpcInputDiagnosticsRequest);
                        var diagnostics = await sessionService.GetInputDiagnosticsAsync(payload.ViewerId, payload.SessionId);
                        return CreateResponse(message, diagnostics, AureTTYJsonSerializerContext.Default.TerminalSessionInputDiagnostics);
                    }

                case TerminalIpcMethods.Resize:
                    {
                        var payload = RequirePayload(message, AureTTYJsonSerializerContext.Default.TerminalIpcResizeRequest);
                        await sessionService.ResizeAsync(payload.ViewerId, payload.Request);
                        return CreateResponse(message, new TerminalIpcAck(), AureTTYJsonSerializerContext.Default.TerminalIpcAck);
                    }

                case TerminalIpcMethods.Signal:
                    {
                        var payload = RequirePayload(message, AureTTYJsonSerializerContext.Default.TerminalIpcSignalRequest);
                        await sessionService.SignalAsync(payload.ViewerId, payload.SessionId, payload.Signal);
                        return CreateResponse(message, new TerminalIpcAck(), AureTTYJsonSerializerContext.Default.TerminalIpcAck);
                    }

                case TerminalIpcMethods.Close:
                    {
                        var payload = RequirePayload(message, AureTTYJsonSerializerContext.Default.TerminalIpcCloseRequest);
                        await sessionService.CloseAsync(payload.ViewerId, payload.SessionId);
                        return CreateResponse(message, new TerminalIpcAck(), AureTTYJsonSerializerContext.Default.TerminalIpcAck);
                    }

                case TerminalIpcMethods.CloseViewerSessions:
                    {
                        var payload = RequirePayload(message, AureTTYJsonSerializerContext.Default.TerminalIpcCloseViewerSessionsRequest);
                        await sessionService.CloseViewerSessionsAsync(payload.ViewerId);
                        return CreateResponse(message, new TerminalIpcAck(), AureTTYJsonSerializerContext.Default.TerminalIpcAck);
                    }

                case TerminalIpcMethods.CloseAllSessions:
                    {
                        await sessionService.CloseAllSessionsAsync();
                        return CreateResponse(message, new TerminalIpcAck(), AureTTYJsonSerializerContext.Default.TerminalIpcAck);
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

    private static bool IsAuthorized(HttpContext context, TerminalServiceOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return false;
        }

        if (context.Request.Headers.TryGetValue(TerminalServiceOptions.ApiKeyHeaderName, out var headerValues))
        {
            foreach (var headerValue in headerValues)
            {
                if (SecureEquals(headerValue, options.ApiKey))
                {
                    return true;
                }
            }
        }

        if (!options.AllowApiKeyQueryParameter)
        {
            return false;
        }

        if (!context.Request.Query.TryGetValue("api_key", out var queryValues))
        {
            return false;
        }

        foreach (var queryValue in queryValues)
        {
            if (SecureEquals(queryValue, options.ApiKey))
            {
                return true;
            }
        }

        return false;
    }

    private static bool SecureEquals(string? candidate, string expected)
    {
        if (candidate is null)
        {
            return false;
        }

        var candidateBytes = Encoding.UTF8.GetBytes(candidate);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        return CryptographicOperations.FixedTimeEquals(candidateBytes, expectedBytes);
    }

    private static T RequirePayload<T>(TerminalIpcMessage message, JsonTypeInfo<T> jsonTypeInfo)
    {
        if (message.Payload is not JsonElement payload)
        {
            throw new InvalidOperationException($"Request payload for '{message.Method}' is missing or invalid.");
        }

        return payload.Deserialize(jsonTypeInfo)
               ?? throw new InvalidOperationException($"Request payload for '{message.Method}' is missing or invalid.");
    }

    private static TerminalIpcMessage CreateResponse<T>(TerminalIpcMessage request, T payload, JsonTypeInfo<T> jsonTypeInfo)
    {
        return new TerminalIpcMessage
        {
            Type = TerminalIpcMessageTypes.Response,
            Id = request.Id,
            Method = request.Method,
            Payload = JsonSerializer.SerializeToElement(payload, jsonTypeInfo)
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
        string viewerId,
        TerminalSessionEvent terminalEvent,
        CancellationToken cancellationToken)
    {
        var eventPayload = new TerminalIpcSessionEvent(viewerId, terminalEvent);
        var message = new TerminalIpcMessage
        {
            Type = TerminalIpcMessageTypes.Event,
            Method = TerminalIpcMethods.SessionEvent,
            Payload = JsonSerializer.SerializeToElement(eventPayload, AureTTYJsonSerializerContext.Default.TerminalIpcSessionEvent)
        };

        await WriteMessageAsync(webSocket, writeLock, message, cancellationToken);
    }

    private static async Task WriteMessageAsync(
        WebSocket webSocket,
        SemaphoreSlim writeLock,
        TerminalIpcMessage message,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(message, AureTTYJsonSerializerContext.Default.TerminalIpcMessage);

        await writeLock.WaitAsync(cancellationToken);
        try
        {
            await webSocket.SendAsync(
                new ArraySegment<byte>(json),
                WebSocketMessageType.Text,
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

        await WriteMessageAsync(webSocket, writeLock, message, cancellationToken);
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

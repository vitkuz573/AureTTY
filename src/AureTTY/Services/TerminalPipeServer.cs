using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using AureTTY.Contracts.Abstractions;
using AureTTY.Protocol;
using AureTTY.Serialization;

namespace AureTTY.Services;

public sealed class TerminalPipeServer(
    TerminalServiceOptions options,
    ITerminalSessionService terminalSessionService,
    PipeTerminalSessionEventPublisher eventPublisher,
    ILogger<TerminalPipeServer> logger) : BackgroundService
{
    private readonly TerminalServiceOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly ITerminalSessionService _terminalSessionService = terminalSessionService ?? throw new ArgumentNullException(nameof(terminalSessionService));
    private readonly PipeTerminalSessionEventPublisher _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
    private readonly ILogger<TerminalPipeServer> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private NamedPipeServerStream? _pipe;
    private StreamReader? _reader;
    private StreamWriter? _writer;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await AcceptClientConnectionAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown.
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Terminal service pipe loop failed.");
            }
            finally
            {
                _eventPublisher.SetSink(null);
                await CloseAllSessionsSafeAsync(stoppingToken);
                await DisposeTransportAsync();
            }
        }
    }

    private async Task AcceptClientConnectionAsync(CancellationToken cancellationToken)
    {
        _pipe = new NamedPipeServerStream(
            _options.PipeName,
            PipeDirection.InOut,
            maxNumberOfServerInstances: 1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);

        _logger.LogInformation("Waiting for host IPC connection on pipe {PipeName}.", _options.PipeName);
        await _pipe.WaitForConnectionAsync(cancellationToken);
        _logger.LogInformation("Host IPC connected on pipe {PipeName}.", _options.PipeName);

        _reader = new StreamReader(_pipe, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        _writer = new StreamWriter(_pipe, new UTF8Encoding(false), leaveOpen: true)
        {
            AutoFlush = true,
            NewLine = "\n"
        };

        await ReceiveAndValidateHostHelloAsync(cancellationToken);
        await SendHelloAsync(cancellationToken);

        _eventPublisher.SetSink(SendEventAsync);

        await ProcessHostRequestsAsync(cancellationToken);
        _logger.LogInformation("Host IPC disconnected from pipe {PipeName}.", _options.PipeName);
    }

    private async Task ProcessHostRequestsAsync(CancellationToken cancellationToken)
    {
        if (_reader is null)
        {
            throw new InvalidOperationException("Terminal IPC reader is not initialized.");
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await _reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            TerminalIpcMessage? message;
            try
            {
                message = JsonSerializer.Deserialize(line, AureTTYJsonSerializerContext.Default.TerminalIpcMessage);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse terminal IPC message.");
                continue;
            }

            if (message is null || !string.Equals(message.Type, TerminalIpcMessageTypes.Request, StringComparison.Ordinal))
            {
                continue;
            }

            var response = await HandleRequestAsync(message, cancellationToken);
            await WriteMessageAsync(response, cancellationToken);
        }
    }

    private async Task SendHelloAsync(CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.SerializeToElement(
            new TerminalIpcHelloPayload(_options.PipeToken),
            AureTTYJsonSerializerContext.Default.TerminalIpcHelloPayload);

        await WriteMessageAsync(new TerminalIpcMessage
        {
            Type = TerminalIpcMessageTypes.Hello,
            Method = TerminalIpcMethods.Hello,
            Payload = payload
        }, cancellationToken);
    }

    private async Task ReceiveAndValidateHostHelloAsync(CancellationToken cancellationToken)
    {
        if (_reader is null)
        {
            throw new InvalidOperationException("Terminal IPC reader is not initialized.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));

        var line = await _reader.ReadLineAsync(timeout.Token)
                   ?? throw new InvalidOperationException("Host closed IPC stream before hello handshake.");

        var message = JsonSerializer.Deserialize(line, AureTTYJsonSerializerContext.Default.TerminalIpcMessage)
                      ?? throw new InvalidOperationException("Host hello message is invalid.");

        if (!string.Equals(message.Type, TerminalIpcMessageTypes.Hello, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Expected hello message from host.");
        }

        var payload = DeserializePayload(message, AureTTYJsonSerializerContext.Default.TerminalIpcHelloPayload)
                      ?? throw new InvalidOperationException("Host hello payload is missing.");

        if (!string.Equals(payload.Token, _options.PipeToken, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("Host hello token mismatch.");
        }
    }

    private async Task<TerminalIpcMessage> HandleRequestAsync(TerminalIpcMessage message, CancellationToken cancellationToken)
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
                        var handle = await _terminalSessionService.StartAsync(payload.ViewerId, payload.Request, cancellationToken);
                        return CreateResponse(message, handle, AureTTYJsonSerializerContext.Default.TerminalSessionHandle);
                    }

                case TerminalIpcMethods.Resume:
                    {
                        var payload = RequirePayload(message, AureTTYJsonSerializerContext.Default.TerminalIpcResumeRequest);
                        var handle = await _terminalSessionService.ResumeAsync(payload.ViewerId, payload.Request, cancellationToken);
                        return CreateResponse(message, handle, AureTTYJsonSerializerContext.Default.TerminalSessionHandle);
                    }

                case TerminalIpcMethods.SendInput:
                    {
                        var payload = RequirePayload(message, AureTTYJsonSerializerContext.Default.TerminalIpcInputRequest);
                        await _terminalSessionService.SendInputAsync(payload.ViewerId, payload.Request, cancellationToken);
                        return CreateResponse(message, new TerminalIpcAck(), AureTTYJsonSerializerContext.Default.TerminalIpcAck);
                    }

                case TerminalIpcMethods.GetInputDiagnostics:
                    {
                        var payload = RequirePayload(message, AureTTYJsonSerializerContext.Default.TerminalIpcInputDiagnosticsRequest);
                        var diagnostics = await _terminalSessionService.GetInputDiagnosticsAsync(payload.ViewerId, payload.SessionId, cancellationToken);
                        return CreateResponse(message, diagnostics, AureTTYJsonSerializerContext.Default.TerminalSessionInputDiagnostics);
                    }

                case TerminalIpcMethods.Resize:
                    {
                        var payload = RequirePayload(message, AureTTYJsonSerializerContext.Default.TerminalIpcResizeRequest);
                        await _terminalSessionService.ResizeAsync(payload.ViewerId, payload.Request, cancellationToken);
                        return CreateResponse(message, new TerminalIpcAck(), AureTTYJsonSerializerContext.Default.TerminalIpcAck);
                    }

                case TerminalIpcMethods.Signal:
                    {
                        var payload = RequirePayload(message, AureTTYJsonSerializerContext.Default.TerminalIpcSignalRequest);
                        await _terminalSessionService.SignalAsync(payload.ViewerId, payload.SessionId, payload.Signal, cancellationToken);
                        return CreateResponse(message, new TerminalIpcAck(), AureTTYJsonSerializerContext.Default.TerminalIpcAck);
                    }

                case TerminalIpcMethods.Close:
                    {
                        var payload = RequirePayload(message, AureTTYJsonSerializerContext.Default.TerminalIpcCloseRequest);
                        await _terminalSessionService.CloseAsync(payload.ViewerId, payload.SessionId, cancellationToken);
                        return CreateResponse(message, new TerminalIpcAck(), AureTTYJsonSerializerContext.Default.TerminalIpcAck);
                    }

                case TerminalIpcMethods.CloseViewerSessions:
                    {
                        var payload = RequirePayload(message, AureTTYJsonSerializerContext.Default.TerminalIpcCloseViewerSessionsRequest);
                        await _terminalSessionService.CloseViewerSessionsAsync(payload.ViewerId, cancellationToken);
                        return CreateResponse(message, new TerminalIpcAck(), AureTTYJsonSerializerContext.Default.TerminalIpcAck);
                    }

                case TerminalIpcMethods.CloseAllSessions:
                    {
                        await _terminalSessionService.CloseAllSessionsAsync(cancellationToken);
                        return CreateResponse(message, new TerminalIpcAck(), AureTTYJsonSerializerContext.Default.TerminalIpcAck);
                    }

                default:
                    return CreateError(message, $"Unsupported terminal IPC method '{method}'.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Terminal IPC request failed. Method={Method}.", method);
            return CreateError(message, ex.GetBaseException().Message);
        }
    }

    private static T RequirePayload<T>(TerminalIpcMessage message, JsonTypeInfo<T> jsonTypeInfo)
    {
        var payload = DeserializePayload(message, jsonTypeInfo);
        return payload ?? throw new InvalidOperationException($"Request payload for '{message.Method}' is missing or invalid.");
    }

    private static T? DeserializePayload<T>(TerminalIpcMessage message, JsonTypeInfo<T> jsonTypeInfo)
    {
        if (message.Payload is not JsonElement payload)
        {
            return default;
        }

        return payload.Deserialize(jsonTypeInfo);
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

    private Task SendEventAsync(TerminalIpcSessionEvent terminalEvent, CancellationToken cancellationToken)
    {
        return WriteMessageAsync(new TerminalIpcMessage
        {
            Type = TerminalIpcMessageTypes.Event,
            Method = TerminalIpcMethods.SessionEvent,
            Payload = JsonSerializer.SerializeToElement(terminalEvent, AureTTYJsonSerializerContext.Default.TerminalIpcSessionEvent)
        }, cancellationToken);
    }

    private async Task WriteMessageAsync(TerminalIpcMessage message, CancellationToken cancellationToken)
    {
        if (_writer is null)
        {
            throw new InvalidOperationException("Terminal IPC writer is not initialized.");
        }

        var line = JsonSerializer.Serialize(message, AureTTYJsonSerializerContext.Default.TerminalIpcMessage);

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await _writer.WriteLineAsync(line);
            await _writer.FlushAsync(cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task DisposeTransportAsync()
    {
        try
        {
            _reader?.Dispose();
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            _logger.LogDebug(ex, "Terminal IPC reader disposal failed.");
        }
        _reader = null;

        if (_writer is not null)
        {
            try
            {
                await _writer.DisposeAsync();
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException)
            {
                _logger.LogDebug(ex, "Terminal IPC writer disposal failed.");
            }

            _writer = null;
        }

        if (_pipe is not null)
        {
            try
            {
                await _pipe.DisposeAsync();
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException)
            {
                _logger.LogDebug(ex, "Terminal IPC pipe disposal failed.");
            }

            _pipe = null;
        }
    }

    private async Task CloseAllSessionsSafeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _terminalSessionService.CloseAllSessionsAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to close terminal sessions after host IPC disconnect.");
        }
    }
}

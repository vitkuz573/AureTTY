using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using AureTTY.Contracts.Abstractions;
using AureTTY.Contracts.DTOs;
using AureTTY.Contracts.Enums;
using AureTTY.Protocol;
using AureTTY.Services;

namespace AureTTY.Tests;

public sealed class TerminalPipeServerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task StartAsync_WhenPingRequestReceived_ReturnsAckResponse()
    {
        var pipeName = $"auretty-terminal-tests-{Guid.NewGuid():N}";
        var token = Guid.NewGuid().ToString("N");
        var terminalSessionService = new Mock<ITerminalSessionService>(MockBehavior.Strict);
        terminalSessionService
            .Setup(service => service.CloseAllSessionsAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var eventPublisher = new PipeTerminalSessionEventPublisher();
        using var sut = new TerminalPipeServer(
            new TerminalServiceOptions(pipeName, token),
            terminalSessionService.Object,
            eventPublisher,
            NullLogger<TerminalPipeServer>.Instance);

        using var hostPipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await sut.StartAsync(timeout.Token);
        await hostPipe.ConnectAsync(timeout.Token);

        using var reader = CreateReader(hostPipe);
        await using var writer = CreateWriter(hostPipe);

        await CompleteHandshakeAsync(reader, writer, token, timeout.Token);

        var requestId = Guid.NewGuid().ToString("N");
        await WriteMessageAsync(writer, new TerminalIpcMessage
        {
            Type = TerminalIpcMessageTypes.Request,
            Id = requestId,
            Method = TerminalIpcMethods.Ping,
            Payload = JsonSerializer.SerializeToElement(new TerminalIpcAck(), JsonOptions)
        }, timeout.Token);

        var response = await ReadMessageAsync(reader, timeout.Token);
        Assert.Equal(TerminalIpcMessageTypes.Response, response.Type);
        Assert.Equal(requestId, response.Id);
        Assert.Equal(TerminalIpcMethods.Ping, response.Method);

        var ack = response.Payload?.Deserialize<TerminalIpcAck>(JsonOptions);
        Assert.NotNull(ack);
        Assert.True(ack.Success);

        await sut.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_WhenStartRequestReceived_ForwardsToTerminalSessionService()
    {
        var pipeName = $"auretty-terminal-tests-{Guid.NewGuid():N}";
        var token = Guid.NewGuid().ToString("N");
        var expectedHandle = new TerminalSessionHandle("session-42")
        {
            State = TerminalSessionState.Running,
            ProcessId = 4242
        };

        var terminalSessionService = new Mock<ITerminalSessionService>(MockBehavior.Strict);
        terminalSessionService
            .Setup(service => service.StartAsync(
                "viewer-42",
                It.IsAny<TerminalSessionStartRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedHandle);
        terminalSessionService
            .Setup(service => service.CloseAllSessionsAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var eventPublisher = new PipeTerminalSessionEventPublisher();
        using var sut = new TerminalPipeServer(
            new TerminalServiceOptions(pipeName, token),
            terminalSessionService.Object,
            eventPublisher,
            NullLogger<TerminalPipeServer>.Instance);

        using var hostPipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await sut.StartAsync(timeout.Token);
        await hostPipe.ConnectAsync(timeout.Token);

        using var reader = CreateReader(hostPipe);
        await using var writer = CreateWriter(hostPipe);

        await CompleteHandshakeAsync(reader, writer, token, timeout.Token);

        var requestId = Guid.NewGuid().ToString("N");
        await WriteMessageAsync(writer, new TerminalIpcMessage
        {
            Type = TerminalIpcMessageTypes.Request,
            Id = requestId,
            Method = TerminalIpcMethods.Start,
            Payload = JsonSerializer.SerializeToElement(
                new TerminalIpcStartRequest("viewer-42", new TerminalSessionStartRequest("session-42", Shell.Pwsh)),
                JsonOptions)
        }, timeout.Token);

        var response = await ReadMessageAsync(reader, timeout.Token);
        var responseHandle = response.Payload?.Deserialize<TerminalSessionHandle>(JsonOptions);

        Assert.Equal(TerminalIpcMessageTypes.Response, response.Type);
        Assert.Equal(requestId, response.Id);
        Assert.NotNull(responseHandle);
        Assert.Equal("session-42", responseHandle.SessionId);
        Assert.Equal(TerminalSessionState.Running, responseHandle.State);
        Assert.Equal(4242, responseHandle.ProcessId);

        await sut.StopAsync(CancellationToken.None);
        terminalSessionService.VerifyAll();
    }

    [Fact]
    public async Task StartAsync_WhenTerminalEventPublished_WritesEventToPipe()
    {
        var pipeName = $"auretty-terminal-tests-{Guid.NewGuid():N}";
        var token = Guid.NewGuid().ToString("N");
        var terminalSessionService = new Mock<ITerminalSessionService>(MockBehavior.Strict);
        terminalSessionService
            .Setup(service => service.CloseAllSessionsAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var eventPublisher = new PipeTerminalSessionEventPublisher();
        using var sut = new TerminalPipeServer(
            new TerminalServiceOptions(pipeName, token),
            terminalSessionService.Object,
            eventPublisher,
            NullLogger<TerminalPipeServer>.Instance);

        using var hostPipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await sut.StartAsync(timeout.Token);
        await hostPipe.ConnectAsync(timeout.Token);

        using var reader = CreateReader(hostPipe);
        await using var writer = CreateWriter(hostPipe);

        await CompleteHandshakeAsync(reader, writer, token, timeout.Token);

        var pingId = Guid.NewGuid().ToString("N");
        await WriteMessageAsync(writer, new TerminalIpcMessage
        {
            Type = TerminalIpcMessageTypes.Request,
            Id = pingId,
            Method = TerminalIpcMethods.Ping,
            Payload = JsonSerializer.SerializeToElement(new TerminalIpcAck(), JsonOptions)
        }, timeout.Token);
        _ = await ReadMessageAsync(reader, timeout.Token);

        var terminalEvent = new TerminalSessionEvent("session-event", TerminalSessionEventType.Output)
        {
            Text = "terminal output"
        };

        var readEventTask = ReadMessageAsync(reader, timeout.Token);
        await eventPublisher.SendTerminalSessionEventAsync("viewer-event", terminalEvent);
        var message = await readEventTask;
        var payload = message.Payload?.Deserialize<TerminalIpcSessionEvent>(JsonOptions);

        Assert.Equal(TerminalIpcMessageTypes.Event, message.Type);
        Assert.Equal(TerminalIpcMethods.SessionEvent, message.Method);
        Assert.NotNull(payload);
        Assert.Equal("viewer-event", payload.ViewerId);
        Assert.Equal("session-event", payload.Event.SessionId);
        Assert.Equal(TerminalSessionEventType.Output, payload.Event.EventType);
        Assert.Equal("terminal output", payload.Event.Text);

        await sut.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_WhenCloseAllSessionsRequestReceived_ForwardsToTerminalSessionService()
    {
        var pipeName = $"auretty-terminal-tests-{Guid.NewGuid():N}";
        var token = Guid.NewGuid().ToString("N");

        var terminalSessionService = new Mock<ITerminalSessionService>(MockBehavior.Strict);
        terminalSessionService
            .Setup(service => service.CloseAllSessionsAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var eventPublisher = new PipeTerminalSessionEventPublisher();
        using var sut = new TerminalPipeServer(
            new TerminalServiceOptions(pipeName, token),
            terminalSessionService.Object,
            eventPublisher,
            NullLogger<TerminalPipeServer>.Instance);

        using var hostPipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await sut.StartAsync(timeout.Token);
        await hostPipe.ConnectAsync(timeout.Token);

        using var reader = CreateReader(hostPipe);
        await using var writer = CreateWriter(hostPipe);
        await CompleteHandshakeAsync(reader, writer, token, timeout.Token);

        var requestId = Guid.NewGuid().ToString("N");
        await WriteMessageAsync(writer, new TerminalIpcMessage
        {
            Type = TerminalIpcMessageTypes.Request,
            Id = requestId,
            Method = TerminalIpcMethods.CloseAllSessions,
            Payload = JsonSerializer.SerializeToElement(new TerminalIpcAck(), JsonOptions)
        }, timeout.Token);

        var response = await ReadMessageAsync(reader, timeout.Token);
        Assert.Equal(TerminalIpcMessageTypes.Response, response.Type);
        Assert.Equal(requestId, response.Id);
        Assert.Equal(TerminalIpcMethods.CloseAllSessions, response.Method);

        await sut.StopAsync(CancellationToken.None);
        terminalSessionService.VerifyAll();
    }

    [Fact]
    public async Task StartAsync_WhenMethodIsMissing_ReturnsErrorResponse()
    {
        var pipeName = $"auretty-terminal-tests-{Guid.NewGuid():N}";
        var token = Guid.NewGuid().ToString("N");
        var terminalSessionService = new Mock<ITerminalSessionService>(MockBehavior.Strict);
        terminalSessionService
            .Setup(service => service.CloseAllSessionsAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var eventPublisher = new PipeTerminalSessionEventPublisher();
        using var sut = new TerminalPipeServer(
            new TerminalServiceOptions(pipeName, token),
            terminalSessionService.Object,
            eventPublisher,
            NullLogger<TerminalPipeServer>.Instance);

        using var hostPipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await sut.StartAsync(timeout.Token);
        await hostPipe.ConnectAsync(timeout.Token);

        using var reader = CreateReader(hostPipe);
        await using var writer = CreateWriter(hostPipe);
        await CompleteHandshakeAsync(reader, writer, token, timeout.Token);

        var requestId = Guid.NewGuid().ToString("N");
        await WriteMessageAsync(writer, new TerminalIpcMessage
        {
            Type = TerminalIpcMessageTypes.Request,
            Id = requestId,
            Method = null,
            Payload = JsonSerializer.SerializeToElement(new TerminalIpcAck(), JsonOptions)
        }, timeout.Token);

        var response = await ReadMessageAsync(reader, timeout.Token);
        Assert.Equal(TerminalIpcMessageTypes.Error, response.Type);
        Assert.Equal(requestId, response.Id);
        Assert.Equal("Request method is required.", response.Error);

        await sut.StopAsync(CancellationToken.None);
        terminalSessionService.VerifyAll();
    }

    [Fact]
    public async Task StartAsync_WhenMethodIsUnsupported_ReturnsErrorResponse()
    {
        var pipeName = $"auretty-terminal-tests-{Guid.NewGuid():N}";
        var token = Guid.NewGuid().ToString("N");
        var terminalSessionService = new Mock<ITerminalSessionService>(MockBehavior.Strict);
        terminalSessionService
            .Setup(service => service.CloseAllSessionsAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var eventPublisher = new PipeTerminalSessionEventPublisher();
        using var sut = new TerminalPipeServer(
            new TerminalServiceOptions(pipeName, token),
            terminalSessionService.Object,
            eventPublisher,
            NullLogger<TerminalPipeServer>.Instance);

        using var hostPipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await sut.StartAsync(timeout.Token);
        await hostPipe.ConnectAsync(timeout.Token);

        using var reader = CreateReader(hostPipe);
        await using var writer = CreateWriter(hostPipe);
        await CompleteHandshakeAsync(reader, writer, token, timeout.Token);

        var requestId = Guid.NewGuid().ToString("N");
        await WriteMessageAsync(writer, new TerminalIpcMessage
        {
            Type = TerminalIpcMessageTypes.Request,
            Id = requestId,
            Method = "terminal.unsupported",
            Payload = JsonSerializer.SerializeToElement(new TerminalIpcAck(), JsonOptions)
        }, timeout.Token);

        var response = await ReadMessageAsync(reader, timeout.Token);
        Assert.Equal(TerminalIpcMessageTypes.Error, response.Type);
        Assert.Equal(requestId, response.Id);
        Assert.Equal("Unsupported terminal IPC method 'terminal.unsupported'.", response.Error);

        await sut.StopAsync(CancellationToken.None);
        terminalSessionService.VerifyAll();
    }

    private static StreamReader CreateReader(Stream stream)
    {
        return new StreamReader(stream, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: false, leaveOpen: true);
    }

    private static StreamWriter CreateWriter(Stream stream)
    {
        return new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true)
        {
            AutoFlush = true,
            NewLine = "\n"
        };
    }

    private static async Task CompleteHandshakeAsync(StreamReader reader, StreamWriter writer, string token, CancellationToken cancellationToken)
    {
        await WriteMessageAsync(writer, new TerminalIpcMessage
        {
            Type = TerminalIpcMessageTypes.Hello,
            Method = TerminalIpcMethods.Hello,
            Payload = JsonSerializer.SerializeToElement(new TerminalIpcHelloPayload(token), JsonOptions)
        }, cancellationToken);

        var serviceHello = await ReadMessageAsync(reader, cancellationToken);
        Assert.Equal(TerminalIpcMessageTypes.Hello, serviceHello.Type);
        Assert.Equal(TerminalIpcMethods.Hello, serviceHello.Method);

        var payload = serviceHello.Payload?.Deserialize<TerminalIpcHelloPayload>(JsonOptions);
        Assert.NotNull(payload);
        Assert.Equal(token, payload.Token);
    }

    private static async Task<TerminalIpcMessage> ReadMessageAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var line = await reader.ReadLineAsync(cancellationToken);
        Assert.False(string.IsNullOrWhiteSpace(line));

        var message = JsonSerializer.Deserialize<TerminalIpcMessage>(line!, JsonOptions);
        Assert.NotNull(message);
        return message;
    }

    private static async Task WriteMessageAsync(StreamWriter writer, TerminalIpcMessage message, CancellationToken cancellationToken)
    {
        var line = JsonSerializer.Serialize(message, JsonOptions);
        await writer.WriteLineAsync(line);
        await writer.FlushAsync(cancellationToken);
    }
}

using AureTTY.Contracts.Abstractions;
using AureTTY.Contracts.DTOs;

namespace AureTTY.Services;

public sealed class CompositeTerminalSessionEventPublisher(
    PipeTerminalSessionEventPublisher pipePublisher,
    WebSocketTerminalSessionEventPublisher webSocketPublisher) : ITerminalSessionEventPublisher
{
    private readonly PipeTerminalSessionEventPublisher _pipePublisher = pipePublisher ?? throw new ArgumentNullException(nameof(pipePublisher));
    private readonly WebSocketTerminalSessionEventPublisher _webSocketPublisher = webSocketPublisher ?? throw new ArgumentNullException(nameof(webSocketPublisher));

    public async Task SendTerminalSessionEventAsync(string viewerId, TerminalSessionEvent terminalSessionEvent)
    {
        await _pipePublisher.SendTerminalSessionEventAsync(viewerId, terminalSessionEvent);
        await _webSocketPublisher.SendTerminalSessionEventAsync(viewerId, terminalSessionEvent);
    }
}

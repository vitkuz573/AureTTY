using AureTTY.Contracts.Abstractions;
using AureTTY.Contracts.DTOs;

namespace AureTTY.Services;

public sealed class CompositeTerminalSessionEventPublisher(
    PipeTerminalSessionEventPublisher pipePublisher,
    HttpTerminalSessionEventPublisher httpPublisher) : ITerminalSessionEventPublisher
{
    private readonly PipeTerminalSessionEventPublisher _pipePublisher = pipePublisher ?? throw new ArgumentNullException(nameof(pipePublisher));
    private readonly HttpTerminalSessionEventPublisher _httpPublisher = httpPublisher ?? throw new ArgumentNullException(nameof(httpPublisher));

    public async Task SendTerminalSessionEventAsync(string viewerId, TerminalSessionEvent terminalSessionEvent)
    {
        await _pipePublisher.SendTerminalSessionEventAsync(viewerId, terminalSessionEvent);
        await _httpPublisher.SendTerminalSessionEventAsync(viewerId, terminalSessionEvent);
    }
}

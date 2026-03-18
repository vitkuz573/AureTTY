using AureTTY.Contracts.DTOs;

namespace AureTTY.Contracts.Abstractions;

public interface ITerminalSessionEventPublisher
{
    Task SendTerminalSessionEventAsync(string viewerId, TerminalSessionEvent terminalSessionEvent);
}

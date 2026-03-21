namespace AureTTY.Api.Models;

public sealed class TerminalHealthResponse
{
    public required string Status { get; init; }

    public required string ApiVersion { get; init; }

    public required string[] Transports { get; init; }

    public int WebSocketHelloTimeoutSeconds { get; init; }

    public int MaxConcurrentSessions { get; init; }

    public int MaxSessionsPerViewer { get; init; }

    public int SessionIdleTimeoutSeconds { get; init; }

    public int SessionHardLifetimeSeconds { get; init; }
}

namespace AureTTY.Api.Models;

public sealed class TerminalHealthResponse
{
    public required string Status { get; init; }

    public required string ApiVersion { get; init; }

    public required string[] Transports { get; init; }
}

using System.Text.Json;

namespace AureTTY.Protocol;

public sealed class TerminalIpcMessage
{
    public required string Type { get; init; }

    public string? Id { get; init; }

    public string? Method { get; init; }

    public JsonElement? Payload { get; init; }

    public string? Error { get; init; }
}

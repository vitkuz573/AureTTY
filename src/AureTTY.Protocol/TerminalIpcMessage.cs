using MessagePack;

namespace AureTTY.Protocol;

[MessagePackObject]
public sealed class TerminalIpcMessage
{
    [Key(0)]
    public required string Type { get; init; }

    [Key(1)]
    public string? Id { get; init; }

    [Key(2)]
    public string? Method { get; init; }

    [Key(3)]
    public object? Payload { get; init; }

    [Key(4)]
    public string? Error { get; init; }
}

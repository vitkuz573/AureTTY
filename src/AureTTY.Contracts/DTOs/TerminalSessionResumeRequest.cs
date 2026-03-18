namespace AureTTY.Contracts.DTOs;

[MessagePackObject]
public sealed class TerminalSessionResumeRequest(string sessionId)
{
    [Key(0)]
    public string SessionId { get; } = sessionId;

    [Key(1)]
    public long? LastReceivedSequenceNumber { get; init; }

    [Key(2)]
    public int? Columns { get; init; }

    [Key(3)]
    public int? Rows { get; init; }
}

namespace AureTTY.Contracts.DTOs;

[MessagePackObject]
public sealed class TerminalSessionInputRequest(string sessionId, string text, long sequence)
{
    [Key(0)]
    public string SessionId { get; } = sessionId;

    [Key(1)]
    public string Text { get; } = text;

    [Key(2)]
    public long Sequence { get; } = sequence;
}

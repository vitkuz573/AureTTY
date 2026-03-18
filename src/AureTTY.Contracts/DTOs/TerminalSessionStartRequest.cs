using AureTTY.Contracts.Enums;

namespace AureTTY.Contracts.DTOs;

[MessagePackObject]
public sealed class TerminalSessionStartRequest(string sessionId, Shell shell)
{
    [Key(0)]
    public string SessionId { get; } = sessionId;

    [Key(1)]
    public Shell Shell { get; } = shell;

    [Key(2)]
    public ExecutionRunContext RunContext { get; set; } = ExecutionRunContext.InteractiveUser;

    [Key(3)]
    public string? UserName { get; init; }

    [Key(4)]
    public string? Domain { get; init; }

    [Key(5)]
    public string? Password { get; init; }

    [Key(6)]
    public bool LoadUserProfile { get; set; } = true;

    [Key(7)]
    public string? WorkingDirectory { get; init; }

    [Key(8)]
    public int? Columns { get; init; }

    [Key(9)]
    public int? Rows { get; init; }
}

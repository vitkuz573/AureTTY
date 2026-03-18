using AureTTY.Contracts.Enums;
using AureTTY.Execution.Abstractions;

namespace AureTTY.Linux.Models;

public struct NativeProcessOptions : INativeProcessOptions
{
    public NativeProcessOptions()
    {
    }

    public ExecutionRunContext RunContext { get; set; } = ExecutionRunContext.InteractiveUser;

    public string? UserName { get; set; }

    public string? Domain { get; set; }

    public string? Password { get; set; }

    public bool LoadUserProfile { get; set; } = true;

    public bool UsePseudoTerminal { get; set; } = false;

    public bool RequirePseudoTerminal { get; set; } = false;

    public int? PseudoTerminalColumns { get; set; }

    public int? PseudoTerminalRows { get; set; }
}

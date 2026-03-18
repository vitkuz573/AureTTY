namespace AureTTY.Execution.Abstractions;

public sealed class ProcessRuntimeOptions
{
    public bool UsePseudoTerminal { get; init; }

    public bool RequirePseudoTerminal { get; init; }

    public int? Columns { get; init; }

    public int? Rows { get; init; }
}

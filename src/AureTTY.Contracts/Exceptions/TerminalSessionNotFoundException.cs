namespace AureTTY.Contracts.Exceptions;

public sealed class TerminalSessionNotFoundException : Exception
{
    public TerminalSessionNotFoundException(string message)
        : base(message)
    {
    }
}

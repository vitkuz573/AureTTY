namespace AureTTY.Contracts.Exceptions;

public sealed class TerminalSessionConflictException : Exception
{
    public TerminalSessionConflictException(string message)
        : base(message)
    {
    }
}

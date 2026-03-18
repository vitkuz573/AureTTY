namespace AureTTY.Contracts.Exceptions;

public sealed class TerminalSessionForbiddenException : Exception
{
    public TerminalSessionForbiddenException(string message)
        : base(message)
    {
    }
}

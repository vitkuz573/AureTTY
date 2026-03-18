namespace AureTTY.Contracts.Exceptions;

public sealed class TerminalSessionValidationException : Exception
{
    public TerminalSessionValidationException(string message)
        : base(message)
    {
    }
}

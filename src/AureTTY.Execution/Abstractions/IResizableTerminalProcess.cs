namespace AureTTY.Execution.Abstractions;

public interface IResizableTerminalProcess
{
    Task ResizeTerminalAsync(int columns, int rows, CancellationToken cancellationToken = default);
}

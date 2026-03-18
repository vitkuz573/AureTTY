namespace AureTTY.Execution.Abstractions;

public interface INativeProcessFactory
{
    IProcess Create(INativeProcessOptions options);
}

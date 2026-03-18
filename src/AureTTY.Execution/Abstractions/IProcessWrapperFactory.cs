using System.Diagnostics;

namespace AureTTY.Execution.Abstractions;

public interface IProcessWrapperFactory
{
    IProcess Create();

    IProcess Create(Process process);
}

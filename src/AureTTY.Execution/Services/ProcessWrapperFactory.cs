using System.Diagnostics;
using AureTTY.Execution.Abstractions;

namespace AureTTY.Execution.Services;

public class ProcessWrapperFactory(ICommandLineProvider commandLineProvider) : IProcessWrapperFactory
{
    public IProcess Create()
    {
#pragma warning disable CA2000
        var process = new Process();
#pragma warning restore CA2000

        return new ProcessWrapper(process, commandLineProvider);
    }

    public IProcess Create(Process process)
    {
        return new ProcessWrapper(process, commandLineProvider);
    }
}

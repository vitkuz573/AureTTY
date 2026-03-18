using System.Diagnostics;
using AureTTY.Execution.Abstractions;

namespace AureTTY.Execution.Services;

public class ProcessService(IProcessWrapperFactory processWrapperFactory) : IProcessService
{
    public IProcess[] GetProcesses()
    {
        var processes = Process.GetProcesses();

        return [.. processes.Select(processWrapperFactory.Create)];
    }

    public IProcess GetCurrentProcess()
    {
        var process = Process.GetCurrentProcess();

        return processWrapperFactory.Create(process);
    }

    public IProcess GetProcessById(int processId)
    {
        var process = Process.GetProcessById(processId);

        return processWrapperFactory.Create(process);
    }

    public IProcess[] GetProcessesByName(string processName)
    {
        var processes = Process.GetProcessesByName(processName);

        return [.. processes.Select(processWrapperFactory.Create)];
    }
}

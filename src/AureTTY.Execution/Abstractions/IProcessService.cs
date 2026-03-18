namespace AureTTY.Execution.Abstractions;

public interface IProcessService
{
    IProcess[] GetProcesses();

    IProcess GetCurrentProcess();

    IProcess GetProcessById(int processId);

    IProcess[] GetProcessesByName(string processName);
}

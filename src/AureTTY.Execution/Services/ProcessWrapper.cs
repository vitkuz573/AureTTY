using System.Diagnostics;
using AureTTY.Execution.Abstractions;

namespace AureTTY.Execution.Services;

public class ProcessWrapper(Process process, ICommandLineProvider commandLineProvider) : IProcess
{
    private bool _disposed;

    public int Id => process.Id;

    public int ExitCode => process.ExitCode;

    public int SessionId => process.SessionId;

    public StreamWriter StandardInput => process.StandardInput;

    public StreamReader StandardOutput => process.StandardOutput;

    public StreamReader StandardError => process.StandardError;

    public ProcessModule? MainModule => process.MainModule;

    public string ProcessName => process.ProcessName;

    public long WorkingSet64 => process.WorkingSet64;

    public DateTime StartTime => process.StartTime;

    public bool HasExited
    {
        get
        {
            try
            {
                return process.HasExited;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }

    public Task StartAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        process.StartInfo = startInfo;
        process.Start();
        return Task.CompletedTask;
    }

    public void Kill(bool entireProcessTree = false)
    {
        process.Kill(entireProcessTree);
    }

    public async Task<string[]> GetCommandLineAsync()
    {
        return await commandLineProvider.GetCommandLineAsync(this);
    }

    public bool WaitForExit(uint millisecondsTimeout)
    {
        return process.WaitForExit((int)Math.Min(millisecondsTimeout, int.MaxValue));
    }

    public Task WaitForExitAsync(CancellationToken cancellationToken = default)
    {
        return process.WaitForExitAsync(cancellationToken);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            process.Dispose();
        }

        _disposed = true;
    }
}

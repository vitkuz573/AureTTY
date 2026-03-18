using System.IO.Abstractions;
using AureTTY.Execution.Abstractions;
using AureTTY.Windows.Abstractions;
using AureTTY.Windows.Models;

namespace AureTTY.Windows.Services;

public class NativeProcessFactory(ISessionService sessionService, ICommandLineProvider commandLineProvider, IProcessService processService, IFileSystem fileSystem) : INativeProcessFactory
{
    public IProcess Create(INativeProcessOptions options)
    {
        if (options is not NativeProcessOptions nativeOptions)
        {
            throw new ArgumentException("Invalid process options for Windows platform.", nameof(options));
        }

        return new NativeProcess(nativeOptions, sessionService, commandLineProvider, processService, fileSystem);
    }
}

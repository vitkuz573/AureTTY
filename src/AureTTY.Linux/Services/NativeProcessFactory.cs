using AureTTY.Execution.Abstractions;
using AureTTY.Linux.Models;

namespace AureTTY.Linux.Services;

public sealed class NativeProcessFactory(IProcessWrapperFactory processWrapperFactory) : INativeProcessFactory
{
    private readonly IProcessWrapperFactory _processWrapperFactory = processWrapperFactory ?? throw new ArgumentNullException(nameof(processWrapperFactory));

    public IProcess Create(INativeProcessOptions options)
    {
        if (options is not NativeProcessOptions nativeOptions)
        {
            throw new ArgumentException("Invalid process options for Linux platform.", nameof(options));
        }

        return new NativeProcess(nativeOptions, _processWrapperFactory);
    }
}

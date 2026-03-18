#if AURETTY_LINUX_BACKEND
using AureTTY.Execution.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace AureTTY.Services;

public static partial class ServiceCollectionExtensions
{
    static partial void AddPlatformProcessBackend(IServiceCollection services)
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("This AureTTY build targets Linux runtime only.");
        }

        services.AddSingleton<ICommandLineProvider, AureTTY.Linux.Services.CommandLineProvider>();
        services.AddSingleton<INativeProcessFactory, AureTTY.Linux.Services.NativeProcessFactory>();
        services.AddSingleton<INativeProcessOptionsProvider, AureTTY.Linux.Services.NativeProcessOptionsProvider>();
        services.AddSingleton<IScriptProcessFactory, AureTTY.Linux.Services.ScriptProcessFactory>();
    }
}
#endif

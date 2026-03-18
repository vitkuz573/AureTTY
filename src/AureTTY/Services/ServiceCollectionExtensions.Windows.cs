#if AURETTY_WINDOWS_BACKEND
using System.Runtime.Versioning;
using AureTTY.Execution.Abstractions;
using AureTTY.Windows.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace AureTTY.Services;

public static partial class ServiceCollectionExtensions
{
    [SupportedOSPlatform("windows6.0.6000")]
    static partial void AddPlatformProcessBackend(IServiceCollection services)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(6, 0, 6000))
        {
            if (OperatingSystem.IsWindows())
            {
                throw new PlatformNotSupportedException("AureTTY Windows backend requires Windows Vista / Server 2008 or newer.");
            }

            throw new PlatformNotSupportedException("This AureTTY build targets Windows runtime only.");
        }

        services.AddSingleton<ISessionService, AureTTY.Windows.Services.SessionService>();
        services.AddSingleton<ICommandLineProvider, AureTTY.Windows.Services.CommandLineProvider>();
        services.AddSingleton<INativeProcessFactory, AureTTY.Windows.Services.NativeProcessFactory>();
        services.AddSingleton<INativeProcessOptionsProvider, AureTTY.Windows.Services.NativeProcessOptionsProvider>();
        services.AddSingleton<IScriptProcessFactory, AureTTY.Windows.Services.ScriptProcessFactory>();
    }
}
#endif

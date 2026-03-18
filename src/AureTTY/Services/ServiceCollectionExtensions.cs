using System.IO.Abstractions;
using System.Runtime.Versioning;
using AureTTY.Contracts.Abstractions;
using AureTTY.Core.Services;
using AureTTY.Execution.Abstractions;
using AureTTY.Execution.Services;
using AureTTY.Windows.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace AureTTY.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAureTTYTerminalServices(this IServiceCollection services, TerminalServiceOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        services.AddSingleton(options);
        services.AddSingleton<IFileSystem, FileSystem>();
        services.AddTransient<IProcessWrapperFactory, ProcessWrapperFactory>();
        services.AddSingleton<IProcessService, ProcessService>();

        if (OperatingSystem.IsLinux())
        {
            AddLinuxProcessBackend(services);
        }
        else if (OperatingSystem.IsWindowsVersionAtLeast(6, 0, 6000))
        {
            AddWindowsProcessBackend(services);
        }
        else if (OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("AureTTY Windows backend requires Windows Vista / Server 2008 or newer.");
        }
        else
        {
            throw new PlatformNotSupportedException("AureTTY process backend is supported only on Linux and Windows.");
        }
        services.AddSingleton<PipeTerminalSessionEventPublisher>();
        services.AddSingleton<HttpTerminalSessionEventPublisher>();
        services.AddSingleton<ITerminalSessionEventPublisher, CompositeTerminalSessionEventPublisher>();
        services.AddSingleton<ITerminalSessionService, TerminalSessionService>();
        if (options.EnablePipeApi)
        {
            services.AddHostedService<TerminalPipeServer>();
        }

        return services;
    }

    private static void AddLinuxProcessBackend(IServiceCollection services)
    {
        services.AddSingleton<ICommandLineProvider, AureTTY.Linux.Services.CommandLineProvider>();
        services.AddSingleton<INativeProcessFactory, AureTTY.Linux.Services.NativeProcessFactory>();
        services.AddSingleton<INativeProcessOptionsProvider, AureTTY.Linux.Services.NativeProcessOptionsProvider>();
        services.AddSingleton<IScriptProcessFactory, AureTTY.Linux.Services.ScriptProcessFactory>();
    }

    [SupportedOSPlatform("windows6.0.6000")]
    private static void AddWindowsProcessBackend(IServiceCollection services)
    {
        services.AddSingleton<ISessionService, AureTTY.Windows.Services.SessionService>();
        services.AddSingleton<ICommandLineProvider, AureTTY.Windows.Services.CommandLineProvider>();
        services.AddSingleton<INativeProcessFactory, AureTTY.Windows.Services.NativeProcessFactory>();
        services.AddSingleton<INativeProcessOptionsProvider, AureTTY.Windows.Services.NativeProcessOptionsProvider>();
        services.AddSingleton<IScriptProcessFactory, AureTTY.Windows.Services.ScriptProcessFactory>();
    }
}

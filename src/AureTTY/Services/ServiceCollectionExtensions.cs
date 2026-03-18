using System.IO.Abstractions;
using AureTTY.Contracts.Abstractions;
using AureTTY.Core.Services;
using AureTTY.Execution.Abstractions;
using AureTTY.Execution.Services;
using AureTTY.Linux.Services;
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
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("Current AureTTY build contains Linux process backend only.");
        }

        services.AddSingleton<ICommandLineProvider, CommandLineProvider>();
        services.AddSingleton<INativeProcessFactory, NativeProcessFactory>();
        services.AddSingleton<INativeProcessOptionsProvider, NativeProcessOptionsProvider>();
        services.AddSingleton<IScriptProcessFactory, ScriptProcessFactory>();
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
}

using System.IO.Abstractions;
using AureTTY.Contracts.Configuration;
using AureTTY.Contracts.Abstractions;
using AureTTY.Core.Services;
using AureTTY.Execution.Abstractions;
using AureTTY.Execution.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AureTTY.Services;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection AddAureTTYTerminalServices(this IServiceCollection services, TerminalServiceOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        if (options.SseSubscriptionBufferCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.SseSubscriptionBufferCapacity,
                "SSE subscription buffer capacity must be greater than zero.");
        }

        var validatedLimits = (options.RuntimeLimits ?? TerminalRuntimeLimits.Default).Validate();
        options = options with
        {
            RuntimeLimits = validatedLimits
        };

        services.AddSingleton(options);
        services.AddSingleton(validatedLimits);
        services.AddSingleton<IFileSystem, FileSystem>();
        services.AddTransient<IProcessWrapperFactory, ProcessWrapperFactory>();
        services.AddSingleton<IProcessService, ProcessService>();
        AddPlatformProcessBackend(services);

        services.AddSingleton<TerminalMetrics>();
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

    static partial void AddPlatformProcessBackend(IServiceCollection services);
}

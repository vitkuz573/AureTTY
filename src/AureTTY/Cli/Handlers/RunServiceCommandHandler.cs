using System.CommandLine;
using AureTTY.Api;
using AureTTY.Serialization;
using AureTTY.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AureTTY.Cli.Handlers;

public sealed class RunServiceCommandHandler
{
    public async Task<int> ExecuteAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(parseResult);

        if (!CliArguments.TryCreate(parseResult, out var cliArguments, out var parseError) || cliArguments is null)
        {
            using var loggerFactory = LoggerFactory.Create(static logging =>
            {
                logging.ClearProviders();
                logging.AddSimpleConsole();
            });
            var logger = loggerFactory.CreateLogger("AureTTY.Startup");
            logger.LogError(
                "Invalid terminal service startup arguments: {ParseError}",
                parseError ?? "Invalid terminal service startup arguments.");
            return 2;
        }

        var options = cliArguments.ToTerminalServiceOptions();
        var openApiApplicationName = parseResult.GetValue(CliOptions.ApplicationName);
        var isOpenApiDocumentGeneration = !string.IsNullOrWhiteSpace(openApiApplicationName);
        if (isOpenApiDocumentGeneration)
        {
            options = options with
            {
                EnablePipeApi = false,
                EnableHttpApi = true,
                HttpListenUrl = TerminalServiceOptions.DefaultHttpListenUrl
            };
        }

        if (!options.EnableHttpApi)
        {
            var hostBuilder = Host.CreateApplicationBuilder();
#if AURETTY_WINDOWS_BACKEND
            if (OperatingSystem.IsWindows())
            {
                hostBuilder.Services.AddWindowsService();
            }
#endif

            hostBuilder.Services.AddAureTTYTerminalServices(options);

            var host = hostBuilder.Build();
            await host.RunAsync(cancellationToken);
            return 0;
        }

        var webBuilder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            ApplicationName = ResolveApplicationName(openApiApplicationName)
        });
#if AURETTY_WINDOWS_BACKEND
        if (OperatingSystem.IsWindows())
        {
            webBuilder.Host.UseWindowsService();
        }
#endif

        webBuilder.WebHost.UseUrls(options.HttpListenUrl);
        webBuilder.Services.AddAureTTYTerminalServices(options);
#if AURETTY_NATIVEAOT
        webBuilder.Services.ConfigureHttpJsonOptions(static options =>
        {
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, AureTTYJsonSerializerContext.Default);
        });
#else
        webBuilder.Services.AddControllers();
        webBuilder.Services.AddEndpointsApiExplorer();
        webBuilder.Services.AddOpenApi(TerminalServiceOptions.ApiVersion);
#endif

        var app = webBuilder.Build();
        app.UseMiddleware<ApiKeyAuthenticationMiddleware>();
#if AURETTY_NATIVEAOT
        app.MapTerminalHttpEndpoints();
#else
        app.MapOpenApi();
        app.MapControllers();
#endif

        await app.RunAsync(cancellationToken);
        return 0;
    }

    private static string ResolveApplicationName(string? configuredApplicationName)
    {
        if (!string.IsNullOrWhiteSpace(configuredApplicationName))
        {
            return configuredApplicationName.Trim();
        }

        return typeof(RunServiceCommandHandler).Assembly.GetName().Name ?? "AureTTY";
    }
}

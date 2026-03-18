using System.CommandLine;
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
        if (!options.EnableHttpApi)
        {
            var hostBuilder = Host.CreateApplicationBuilder();
            hostBuilder.Services.AddWindowsService();
            hostBuilder.Services.AddAureTTYTerminalServices(options);

            var host = hostBuilder.Build();
            await host.RunAsync(cancellationToken);
            return 0;
        }

        var webBuilder = WebApplication.CreateSlimBuilder();
        webBuilder.Host.UseWindowsService();
        webBuilder.WebHost.UseUrls(options.HttpListenUrl);
        webBuilder.Services.AddAureTTYTerminalServices(options);
        webBuilder.Services.AddOpenApi(TerminalServiceOptions.ApiVersion);

        var app = webBuilder.Build();
        app.MapOpenApi();
        app.MapAureTTYHttpApi();

        await app.RunAsync(cancellationToken);
        return 0;
    }
}

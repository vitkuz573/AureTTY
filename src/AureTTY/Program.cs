using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using AureTTY.Services;

if (!TerminalServiceOptions.TryParse(args, out var options, out var parseError) || options is null)
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

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService();
builder.Services.AddAureTTYTerminalServices(options);

var host = builder.Build();
await host.RunAsync();
return 0;

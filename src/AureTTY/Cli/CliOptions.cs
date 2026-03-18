using System.CommandLine;
using AureTTY.Services;

namespace AureTTY.Cli;

public static class CliOptions
{
    public static readonly Option<string?> PipeName = CreateString(
        "--pipe-name",
        "Named pipe endpoint used by local IPC clients.");

    public static readonly Option<string?> PipeToken = CreateString(
        "--pipe-token",
        "Authentication token for pipe clients.");

    public static readonly Option<string[]> Transports = CreateStringList(
        "--transport",
        "Enabled transport(s): pipe, http.",
        "-t");

    public static readonly Option<string?> HttpListenUrl = CreateString(
        "--http-listen-url",
        "HTTP listen URL (absolute http:// or https://).");

    public static readonly Option<string?> ApiKey = CreateString(
        "--api-key",
        "HTTP API key sent via X-AureTTY-Key header.");

    public static readonly Option<string?> ApplicationName = new("--applicationName")
    {
        Hidden = true
    };

    static CliOptions()
    {
        PipeName.DefaultValueFactory = _ => Environment.GetEnvironmentVariable(CliArguments.PipeNameEnvironmentVariable);
        PipeToken.DefaultValueFactory = _ => Environment.GetEnvironmentVariable(CliArguments.PipeTokenEnvironmentVariable);
        HttpListenUrl.DefaultValueFactory = _ => Environment.GetEnvironmentVariable(CliArguments.HttpListenUrlEnvironmentVariable);
        ApiKey.DefaultValueFactory = _ => Environment.GetEnvironmentVariable(CliArguments.ApiKeyEnvironmentVariable);
        Transports.DefaultValueFactory = _ => CliArguments.GetTransportDefaultsFromEnvironment();

        Transports.Validators.Add(result =>
        {
            var values = result.GetValueOrDefault<string[]>();
            if (CliArguments.TryNormalizeTransports(values, out _, out var error))
            {
                return;
            }

            result.AddError(error ?? "Invalid --transport value.");
        });

        HttpListenUrl.Validators.Add(result =>
        {
            var value = result.GetValueOrDefault<string?>();
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (CliArguments.IsValidHttpListenUrl(value))
            {
                return;
            }

            result.AddError($"Invalid --http-listen-url value '{value}'. Use absolute http:// or https:// URL.");
        });
    }

    public static void AddTo(Command command)
    {
        ArgumentNullException.ThrowIfNull(command);

        command.Add(PipeName);
        command.Add(PipeToken);
        command.Add(Transports);
        command.Add(HttpListenUrl);
        command.Add(ApiKey);
        command.Add(ApplicationName);
    }

    private static Option<string?> CreateString(string name, string description, params string[] aliases)
    {
        var option = aliases.Length == 0
            ? new Option<string?>(name)
            : new Option<string?>(name, aliases);
        option.Description = description;
        return option;
    }

    private static Option<string[]> CreateStringList(string name, string description, params string[] aliases)
    {
        var option = aliases.Length == 0
            ? new Option<string[]>(name)
            : new Option<string[]>(name, aliases);
        option.Description = description;
        option.Arity = ArgumentArity.OneOrMore;
        option.AllowMultipleArgumentsPerToken = true;
        option.DefaultValueFactory = _ => [];
        return option;
    }
}

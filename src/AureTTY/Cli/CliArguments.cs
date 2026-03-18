using System.CommandLine;
using AureTTY.Protocol;
using AureTTY.Services;

namespace AureTTY.Cli;

public sealed record CliArguments(
    string PipeName,
    string PipeToken,
    bool EnablePipeApi,
    bool EnableHttpApi,
    string HttpListenUrl,
    string ApiKey)
{
    public const string PipeNameEnvironmentVariable = "AURETTY_PIPE_NAME";
    public const string PipeTokenEnvironmentVariable = "AURETTY_PIPE_TOKEN";
    public const string ApiKeyEnvironmentVariable = "AURETTY_API_KEY";
    public const string HttpListenUrlEnvironmentVariable = "AURETTY_HTTP_LISTEN_URL";
    public const string TransportsEnvironmentVariable = "AURETTY_TRANSPORTS";

    private const string PipeTransport = "pipe";
    private const string HttpTransport = "http";

    private static readonly string[] DefaultTransportNames = [PipeTransport, HttpTransport];

    public static bool TryCreate(ParseResult parseResult, out CliArguments? arguments, out string? error)
    {
        ArgumentNullException.ThrowIfNull(parseResult);

        arguments = null;
        if (!TryNormalizeTransports(parseResult.GetValue(CliOptions.Transports), out var transports, out error))
        {
            return false;
        }

        var enablePipeApi = transports.Contains(PipeTransport);
        var enableHttpApi = transports.Contains(HttpTransport);
        if (!enablePipeApi && !enableHttpApi)
        {
            error = "At least one transport must be enabled. Supported values: pipe, http.";
            return false;
        }

        var pipeName = parseResult.GetValue(CliOptions.PipeName);
        var pipeToken = parseResult.GetValue(CliOptions.PipeToken);
        var httpListenUrl = parseResult.GetValue(CliOptions.HttpListenUrl);
        var apiKey = parseResult.GetValue(CliOptions.ApiKey);

        pipeName = string.IsNullOrWhiteSpace(pipeName) ? TerminalIpcDefaults.PipeName : pipeName.Trim();
        pipeToken = string.IsNullOrWhiteSpace(pipeToken) ? TerminalIpcDefaults.PipeToken : pipeToken.Trim();
        httpListenUrl = string.IsNullOrWhiteSpace(httpListenUrl) ? TerminalServiceOptions.DefaultHttpListenUrl : httpListenUrl.Trim();
        apiKey = string.IsNullOrWhiteSpace(apiKey) ? pipeToken : apiKey.Trim();

        if (enableHttpApi && !IsValidHttpListenUrl(httpListenUrl))
        {
            error = $"Invalid --http-listen-url value '{httpListenUrl}'. Use absolute http:// or https:// URL.";
            return false;
        }

        arguments = new CliArguments(
            PipeName: pipeName,
            PipeToken: pipeToken,
            EnablePipeApi: enablePipeApi,
            EnableHttpApi: enableHttpApi,
            HttpListenUrl: httpListenUrl,
            ApiKey: apiKey);
        return true;
    }

    public TerminalServiceOptions ToTerminalServiceOptions()
    {
        return new TerminalServiceOptions(
            PipeName,
            PipeToken,
            EnablePipeApi,
            EnableHttpApi,
            HttpListenUrl,
            ApiKey);
    }

    internal static string[] GetTransportDefaultsFromEnvironment()
    {
        var configured = Environment.GetEnvironmentVariable(TransportsEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(configured))
        {
            return DefaultTransportNames;
        }

        var values = configured
            .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return values.Length == 0 ? DefaultTransportNames : values;
    }

    internal static bool TryNormalizeTransports(
        IEnumerable<string>? values,
        out HashSet<string> normalizedTransports,
        out string? error)
    {
        normalizedTransports = new HashSet<string>(StringComparer.Ordinal);
        error = null;

        if (values is null)
        {
            error = "At least one --transport value is required.";
            return false;
        }

        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var normalized = value.Trim().ToLowerInvariant();
            if (normalized is not PipeTransport and not HttpTransport)
            {
                error = $"Unsupported --transport value '{value}'. Supported values: pipe, http.";
                return false;
            }

            normalizedTransports.Add(normalized);
        }

        if (normalizedTransports.Count == 0)
        {
            error = "At least one --transport value is required.";
            return false;
        }

        return true;
    }

    internal static bool IsValidHttpListenUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return string.Equals(uri.Scheme, "http", StringComparison.OrdinalIgnoreCase)
               || string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase);
    }
}

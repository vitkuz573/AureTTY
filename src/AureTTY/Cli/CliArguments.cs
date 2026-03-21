using System.CommandLine;
using AureTTY.Contracts.Configuration;
using AureTTY.Protocol;
using AureTTY.Services;

namespace AureTTY.Cli;

public sealed record CliArguments(
    string PipeName,
    string PipeToken,
    bool EnablePipeApi,
    bool EnableHttpApi,
    string HttpListenUrl,
    string ApiKey,
    TerminalRuntimeLimits RuntimeLimits,
    int WebSocketSubscriptionBufferCapacity,
    int WebSocketHelloTimeoutSeconds)
{
    public const string PipeNameEnvironmentVariable = "AURETTY_PIPE_NAME";
    public const string PipeTokenEnvironmentVariable = "AURETTY_PIPE_TOKEN";
    public const string ApiKeyEnvironmentVariable = "AURETTY_API_KEY";
    public const string HttpListenUrlEnvironmentVariable = "AURETTY_HTTP_LISTEN_URL";
    public const string TransportsEnvironmentVariable = "AURETTY_TRANSPORTS";
    public const string MaxConcurrentSessionsEnvironmentVariable = "AURETTY_MAX_CONCURRENT_SESSIONS";
    public const string MaxSessionsPerViewerEnvironmentVariable = "AURETTY_MAX_SESSIONS_PER_VIEWER";
    public const string ReplayBufferCapacityEnvironmentVariable = "AURETTY_REPLAY_BUFFER_CAPACITY";
    public const string MaxPendingInputChunksEnvironmentVariable = "AURETTY_MAX_PENDING_INPUT_CHUNKS";
    public const string SessionIdleTimeoutSecondsEnvironmentVariable = "AURETTY_SESSION_IDLE_TIMEOUT_SECONDS";
    public const string SessionHardLifetimeSecondsEnvironmentVariable = "AURETTY_SESSION_HARD_LIFETIME_SECONDS";
    public const string WebSocketSubscriptionBufferCapacityEnvironmentVariable = "AURETTY_WS_SUBSCRIPTION_BUFFER_CAPACITY";
    public const string WebSocketHelloTimeoutSecondsEnvironmentVariable = "AURETTY_WS_HELLO_TIMEOUT_SECONDS";

    private const string PipeTransport = "pipe";
    private const string HttpTransport = "http";

    private static readonly string[] DefaultTransportNames = [PipeTransport, HttpTransport];

    public static bool TryCreate(
        ParseResult parseResult,
        bool allowMissingSecrets,
        out CliArguments? arguments,
        out string? error)
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
        var maxConcurrentSessions = parseResult.GetValue(CliOptions.MaxConcurrentSessions);
        var maxSessionsPerViewer = parseResult.GetValue(CliOptions.MaxSessionsPerViewer);
        var replayBufferCapacity = parseResult.GetValue(CliOptions.ReplayBufferCapacity);
        var maxPendingInputChunks = parseResult.GetValue(CliOptions.MaxPendingInputChunks);
        var sessionIdleTimeoutSeconds = parseResult.GetValue(CliOptions.SessionIdleTimeoutSeconds);
        var sessionHardLifetimeSeconds = parseResult.GetValue(CliOptions.SessionHardLifetimeSeconds);
        var webSocketSubscriptionBufferCapacity = parseResult.GetValue(CliOptions.WebSocketSubscriptionBufferCapacity);
        var webSocketHelloTimeoutSeconds = parseResult.GetValue(CliOptions.WebSocketHelloTimeoutSeconds);

        pipeName = string.IsNullOrWhiteSpace(pipeName) ? TerminalIpcDefaults.PipeName : pipeName.Trim();
        pipeToken = string.IsNullOrWhiteSpace(pipeToken) ? string.Empty : pipeToken.Trim();
        httpListenUrl = string.IsNullOrWhiteSpace(httpListenUrl) ? TerminalServiceOptions.DefaultHttpListenUrl : httpListenUrl.Trim();
        apiKey = string.IsNullOrWhiteSpace(apiKey) ? string.Empty : apiKey.Trim();

        if (enableHttpApi && !IsValidHttpListenUrl(httpListenUrl))
        {
            error = $"Invalid --http-listen-url value '{httpListenUrl}'. Use absolute http:// or https:// URL.";
            return false;
        }

        if (!allowMissingSecrets && enablePipeApi && string.IsNullOrWhiteSpace(pipeToken))
        {
            error = "Pipe transport is enabled, but --pipe-token (or AURETTY_PIPE_TOKEN) is missing.";
            return false;
        }

        if (!allowMissingSecrets && enableHttpApi && string.IsNullOrWhiteSpace(apiKey))
        {
            error = "HTTP transport is enabled, but --api-key (or AURETTY_API_KEY) is missing.";
            return false;
        }

        var runtimeLimits = new TerminalRuntimeLimits(
            maxConcurrentSessions,
            maxSessionsPerViewer,
            replayBufferCapacity,
            maxPendingInputChunks,
            sessionIdleTimeoutSeconds,
            sessionHardLifetimeSeconds);

        try
        {
            runtimeLimits = runtimeLimits.Validate();
        }
        catch (ArgumentOutOfRangeException ex)
        {
            error = ex.Message;
            return false;
        }

        arguments = new CliArguments(
            PipeName: pipeName,
            PipeToken: pipeToken,
            EnablePipeApi: enablePipeApi,
            EnableHttpApi: enableHttpApi,
            HttpListenUrl: httpListenUrl,
            ApiKey: apiKey,
            RuntimeLimits: runtimeLimits,
            WebSocketSubscriptionBufferCapacity: webSocketSubscriptionBufferCapacity,
            WebSocketHelloTimeoutSeconds: webSocketHelloTimeoutSeconds);
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
            ApiKey)
        {
            RuntimeLimits = RuntimeLimits,
            WebSocketSubscriptionBufferCapacity = WebSocketSubscriptionBufferCapacity,
            WebSocketHelloTimeout = TimeSpan.FromSeconds(WebSocketHelloTimeoutSeconds)
        };
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

    internal static int GetDefaultIntFromEnvironment(string envName, int fallback)
    {
        var configured = Environment.GetEnvironmentVariable(envName);
        if (string.IsNullOrWhiteSpace(configured))
        {
            return fallback;
        }

        return int.TryParse(configured.Trim(), out var parsed)
            ? parsed
            : fallback;
    }

    internal static bool GetDefaultBoolFromEnvironment(string envName, bool fallback)
    {
        var configured = Environment.GetEnvironmentVariable(envName);
        if (string.IsNullOrWhiteSpace(configured))
        {
            return fallback;
        }

        return string.Equals(configured, "1", StringComparison.OrdinalIgnoreCase)
               || string.Equals(configured, "true", StringComparison.OrdinalIgnoreCase)
               || string.Equals(configured, "yes", StringComparison.OrdinalIgnoreCase)
               || string.Equals(configured, "on", StringComparison.OrdinalIgnoreCase);
    }
}

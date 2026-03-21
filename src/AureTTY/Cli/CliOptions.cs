using System.CommandLine;
using System.CommandLine.Parsing;
using AureTTY.Contracts.Configuration;
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

    public static readonly Option<int> MaxConcurrentSessions = CreateInt(
        "--max-concurrent-sessions",
        "Global upper bound for active terminal sessions.");

    public static readonly Option<int> MaxSessionsPerViewer = CreateInt(
        "--max-sessions-per-viewer",
        "Per-viewer upper bound for active terminal sessions.");

    public static readonly Option<int> ReplayBufferCapacity = CreateInt(
        "--replay-buffer-capacity",
        "Replay buffer size (terminal output events per session).");

    public static readonly Option<int> MaxPendingInputChunks = CreateInt(
        "--max-pending-input-chunks",
        "Maximum pending out-of-order input chunks per session.");

    public static readonly Option<int> SessionIdleTimeoutSeconds = CreateInt(
        "--session-idle-timeout-seconds",
        "Maximum session idle time before forced close.");

    public static readonly Option<int> SessionHardLifetimeSeconds = CreateInt(
        "--session-hard-lifetime-seconds",
        "Maximum wall-clock session lifetime before forced close.");

    public static readonly Option<int> WebSocketSubscriptionBufferCapacity = CreateInt(
        "--ws-subscription-buffer-capacity",
        "WebSocket buffered events per subscriber.");

    public static readonly Option<int> WebSocketHelloTimeoutSeconds = CreateInt(
        "--ws-hello-timeout-seconds",
        "WebSocket hello handshake timeout.");

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
        MaxConcurrentSessions.DefaultValueFactory = _ => CliArguments.GetDefaultIntFromEnvironment(
            CliArguments.MaxConcurrentSessionsEnvironmentVariable,
            TerminalRuntimeLimits.DefaultMaxConcurrentSessions);
        MaxSessionsPerViewer.DefaultValueFactory = _ => CliArguments.GetDefaultIntFromEnvironment(
            CliArguments.MaxSessionsPerViewerEnvironmentVariable,
            TerminalRuntimeLimits.DefaultMaxSessionsPerViewer);
        ReplayBufferCapacity.DefaultValueFactory = _ => CliArguments.GetDefaultIntFromEnvironment(
            CliArguments.ReplayBufferCapacityEnvironmentVariable,
            TerminalRuntimeLimits.DefaultReplayBufferCapacity);
        MaxPendingInputChunks.DefaultValueFactory = _ => CliArguments.GetDefaultIntFromEnvironment(
            CliArguments.MaxPendingInputChunksEnvironmentVariable,
            TerminalRuntimeLimits.DefaultMaxPendingInputChunks);
        SessionIdleTimeoutSeconds.DefaultValueFactory = _ => CliArguments.GetDefaultIntFromEnvironment(
            CliArguments.SessionIdleTimeoutSecondsEnvironmentVariable,
            TerminalRuntimeLimits.DefaultSessionIdleTimeoutSeconds);
        SessionHardLifetimeSeconds.DefaultValueFactory = _ => CliArguments.GetDefaultIntFromEnvironment(
            CliArguments.SessionHardLifetimeSecondsEnvironmentVariable,
            TerminalRuntimeLimits.DefaultSessionHardLifetimeSeconds);
        WebSocketSubscriptionBufferCapacity.DefaultValueFactory = _ => CliArguments.GetDefaultIntFromEnvironment(
            CliArguments.WebSocketSubscriptionBufferCapacityEnvironmentVariable,
            TerminalServiceOptions.DefaultWebSocketSubscriptionBufferCapacity);
        WebSocketHelloTimeoutSeconds.DefaultValueFactory = _ => CliArguments.GetDefaultIntFromEnvironment(
            CliArguments.WebSocketHelloTimeoutSecondsEnvironmentVariable,
            TerminalServiceOptions.DefaultWebSocketHelloTimeoutSeconds);

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

        MaxConcurrentSessions.Validators.Add(static result => ValidatePositiveInt(result, nameof(MaxConcurrentSessions)));
        MaxSessionsPerViewer.Validators.Add(static result => ValidatePositiveInt(result, nameof(MaxSessionsPerViewer)));
        ReplayBufferCapacity.Validators.Add(static result => ValidatePositiveInt(result, nameof(ReplayBufferCapacity)));
        MaxPendingInputChunks.Validators.Add(static result => ValidatePositiveInt(result, nameof(MaxPendingInputChunks)));
        SessionIdleTimeoutSeconds.Validators.Add(static result => ValidatePositiveInt(result, nameof(SessionIdleTimeoutSeconds)));
        SessionHardLifetimeSeconds.Validators.Add(static result => ValidatePositiveInt(result, nameof(SessionHardLifetimeSeconds)));
        WebSocketSubscriptionBufferCapacity.Validators.Add(static result => ValidatePositiveInt(result, nameof(WebSocketSubscriptionBufferCapacity)));
        WebSocketHelloTimeoutSeconds.Validators.Add(static result => ValidatePositiveInt(result, nameof(WebSocketHelloTimeoutSeconds)));
    }

    public static void AddTo(Command command)
    {
        ArgumentNullException.ThrowIfNull(command);

        command.Add(PipeName);
        command.Add(PipeToken);
        command.Add(Transports);
        command.Add(HttpListenUrl);
        command.Add(ApiKey);
        command.Add(MaxConcurrentSessions);
        command.Add(MaxSessionsPerViewer);
        command.Add(ReplayBufferCapacity);
        command.Add(MaxPendingInputChunks);
        command.Add(SessionIdleTimeoutSeconds);
        command.Add(SessionHardLifetimeSeconds);
        command.Add(WebSocketSubscriptionBufferCapacity);
        command.Add(WebSocketHelloTimeoutSeconds);
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

    private static Option<int> CreateInt(string name, string description)
    {
        return new Option<int>(name)
        {
            Description = description
        };
    }

    private static void ValidatePositiveInt(OptionResult result, string optionName)
    {
        var value = result.GetValueOrDefault<int>();
        if (value > 0)
        {
            return;
        }

        result.AddError($"{optionName} must be greater than zero.");
    }
}

using AureTTY.Contracts.Configuration;

namespace AureTTY.Services;

public sealed record TerminalServiceOptions(
    string PipeName,
    string PipeToken,
    bool EnablePipeApi,
    bool EnableHttpApi,
    string HttpListenUrl,
    string ApiKey)
{
    public const string ApiKeyHeaderName = "X-AureTTY-Key";
    public const string DefaultHttpListenUrl = "http://127.0.0.1:17850";
    public const string ApiVersion = "v1";
    public const int DefaultSseSubscriptionBufferCapacity = 2048;

    public TerminalRuntimeLimits RuntimeLimits { get; init; } = TerminalRuntimeLimits.Default;

    public int SseSubscriptionBufferCapacity { get; init; } = DefaultSseSubscriptionBufferCapacity;

    public bool AllowApiKeyQueryParameter { get; init; }

    public TerminalServiceOptions(string pipeName, string pipeToken)
        : this(
            pipeName,
            pipeToken,
            EnablePipeApi: true,
            EnableHttpApi: true,
            HttpListenUrl: DefaultHttpListenUrl,
            ApiKey: pipeToken)
    {
    }
}

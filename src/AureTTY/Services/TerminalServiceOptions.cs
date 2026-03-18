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

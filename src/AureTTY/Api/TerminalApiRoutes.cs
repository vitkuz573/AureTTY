namespace AureTTY.Api;

public static class TerminalApiRoutes
{
    public const string ApiBase = "api/" + Services.TerminalServiceOptions.ApiVersion;
    public const string Health = ApiBase + "/health";
    public const string AllSessions = ApiBase + "/sessions";
    public const string ViewerSessions = ApiBase + "/viewers/{viewerId}/sessions";
    public const string ViewerEvents = ApiBase + "/viewers/{viewerId}/events";
    public const string ViewerWebSocket = ApiBase + "/viewers/{viewerId}/ws";
}

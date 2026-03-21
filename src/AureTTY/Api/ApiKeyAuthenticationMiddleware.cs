using AureTTY.Services;
using AureTTY.Api.Models;
using AureTTY.Serialization;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace AureTTY.Api;

public sealed class ApiKeyAuthenticationMiddleware(RequestDelegate next)
{
    private readonly RequestDelegate _next = next ?? throw new ArgumentNullException(nameof(next));

    public async Task InvokeAsync(HttpContext context, TerminalServiceOptions options)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new InvalidOperationException("HTTP API key is not configured. Provide --api-key or AURETTY_API_KEY.");
        }

        if (context.WebSockets.IsWebSocketRequest
            && context.Request.Path.HasValue
            && context.Request.Path.Value.EndsWith("/ws", StringComparison.Ordinal))
        {
            await _next(context);
            return;
        }

        if (ApiKeyAuthorization.IsAuthorized(context, options))
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json; charset=utf-8";
        var payload = JsonSerializer.Serialize(
            new ApiErrorResponse
            {
                Error = "Unauthorized",
                Message = $"Provide API key via '{TerminalServiceOptions.ApiKeyHeaderName}' header."
            },
            AureTTYJsonSerializerContext.Default.ApiErrorResponse);
        await context.Response.WriteAsync(payload, context.RequestAborted);
    }

}

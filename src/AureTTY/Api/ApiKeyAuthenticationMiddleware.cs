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

        if (!context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            await _next(context);
            return;
        }

        if (IsAuthorized(context, options.ApiKey))
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
                Message = $"Provide API key via '{TerminalServiceOptions.ApiKeyHeaderName}' header or 'api_key' query parameter."
            },
            AureTTYJsonSerializerContext.Default.ApiErrorResponse);
        await context.Response.WriteAsync(payload, context.RequestAborted);
    }

    private static bool IsAuthorized(HttpContext context, string expectedApiKey)
    {
        if (context.Request.Headers.TryGetValue(TerminalServiceOptions.ApiKeyHeaderName, out var headerValues))
        {
            foreach (var headerValue in headerValues)
            {
                if (string.Equals(headerValue, expectedApiKey, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        if (context.Request.Query.TryGetValue("api_key", out var queryValues))
        {
            foreach (var queryValue in queryValues)
            {
                if (string.Equals(queryValue, expectedApiKey, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }
}

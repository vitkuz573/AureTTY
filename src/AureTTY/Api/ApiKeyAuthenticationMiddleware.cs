using AureTTY.Services;
using AureTTY.Api.Models;
using AureTTY.Serialization;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

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

        if (IsAuthorized(context, options.ApiKey, options.AllowApiKeyQueryParameter))
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

    private static bool IsAuthorized(HttpContext context, string expectedApiKey, bool allowApiKeyQueryParameter)
    {
        if (context.Request.Headers.TryGetValue(TerminalServiceOptions.ApiKeyHeaderName, out var headerValues))
        {
            foreach (var headerValue in headerValues)
            {
                if (SecureEquals(headerValue, expectedApiKey))
                {
                    return true;
                }
            }
        }

        if (!allowApiKeyQueryParameter)
        {
            return false;
        }

        if (!context.Request.Query.TryGetValue("api_key", out var queryValues))
        {
            return false;
        }

        foreach (var queryValue in queryValues)
        {
            if (SecureEquals(queryValue, expectedApiKey))
            {
                return true;
            }
        }

        return false;
    }

    private static bool SecureEquals(string? candidate, string expected)
    {
        if (candidate is null)
        {
            return false;
        }

        var candidateBytes = Encoding.UTF8.GetBytes(candidate);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        return CryptographicOperations.FixedTimeEquals(candidateBytes, expectedBytes);
    }
}

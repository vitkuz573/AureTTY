using System.Security.Cryptography;
using System.Text;
using AureTTY.Services;
using Microsoft.AspNetCore.Http;

namespace AureTTY.Api;

internal static class ApiKeyAuthorization
{
    public static bool IsAuthorized(HttpContext context, TerminalServiceOptions options)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return false;
        }

        if (context.Request.Headers.TryGetValue(TerminalServiceOptions.ApiKeyHeaderName, out var headerValues))
        {
            foreach (var headerValue in headerValues)
            {
                if (SecureEquals(headerValue, options.ApiKey))
                {
                    return true;
                }
            }
        }

        if (!options.AllowApiKeyQueryParameter)
        {
            return false;
        }

        if (!context.Request.Query.TryGetValue("api_key", out var queryValues))
        {
            return false;
        }

        foreach (var queryValue in queryValues)
        {
            if (SecureEquals(queryValue, options.ApiKey))
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

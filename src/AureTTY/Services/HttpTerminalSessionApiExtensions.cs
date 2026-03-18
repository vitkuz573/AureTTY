using System.Text.Json;
using AureTTY.Contracts.Abstractions;
using AureTTY.Contracts.DTOs;
using AureTTY.Contracts.Enums;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AureTTY.Services;

public static class HttpTerminalSessionApiExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapAureTTYHttpApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var api = endpoints.MapGroup($"/{TerminalServiceOptions.ApiVersion}");

        api.MapGet("/health", (TerminalServiceOptions options) =>
        {
            var transports = new List<string>(2);
            if (options.EnableHttpApi)
            {
                transports.Add("http");
            }

            if (options.EnablePipeApi)
            {
                transports.Add("pipe");
            }

            return Results.Ok(new
            {
                status = "ok",
                apiVersion = TerminalServiceOptions.ApiVersion,
                transports
            });
        });

        api.MapGet("/viewers/{viewerId}/events", StreamViewerEventsAsync);
        api.MapPost("/viewers/{viewerId}/sessions/start", StartSessionAsync);
        api.MapPost("/viewers/{viewerId}/sessions/resume", ResumeSessionAsync);
        api.MapPost("/viewers/{viewerId}/sessions/input", SendInputAsync);
        api.MapGet("/viewers/{viewerId}/sessions/{sessionId}/input-diagnostics", GetInputDiagnosticsAsync);
        api.MapPost("/viewers/{viewerId}/sessions/resize", ResizeSessionAsync);
        api.MapPost("/viewers/{viewerId}/sessions/{sessionId}/signal/{signal}", SignalSessionAsync);
        api.MapDelete("/viewers/{viewerId}/sessions/{sessionId}", CloseSessionAsync);
        api.MapDelete("/viewers/{viewerId}/sessions", CloseViewerSessionsAsync);
        api.MapDelete("/sessions", CloseAllSessionsAsync);

        return endpoints;
    }

    private static async Task<IResult> StartSessionAsync(
        string viewerId,
        TerminalSessionStartRequest request,
        ITerminalSessionService terminalSessionService,
        TerminalServiceOptions options,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorized(context, options))
        {
            return Results.Unauthorized();
        }

        try
        {
            var handle = await terminalSessionService.StartAsync(viewerId, request, cancellationToken);
            return Results.Ok(handle);
        }
        catch (Exception ex)
        {
            return MapTerminalFailure(ex);
        }
    }

    private static async Task<IResult> ResumeSessionAsync(
        string viewerId,
        TerminalSessionResumeRequest request,
        ITerminalSessionService terminalSessionService,
        TerminalServiceOptions options,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorized(context, options))
        {
            return Results.Unauthorized();
        }

        try
        {
            var handle = await terminalSessionService.ResumeAsync(viewerId, request, cancellationToken);
            return Results.Ok(handle);
        }
        catch (Exception ex)
        {
            return MapTerminalFailure(ex);
        }
    }

    private static async Task<IResult> SendInputAsync(
        string viewerId,
        TerminalSessionInputRequest request,
        ITerminalSessionService terminalSessionService,
        TerminalServiceOptions options,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorized(context, options))
        {
            return Results.Unauthorized();
        }

        try
        {
            await terminalSessionService.SendInputAsync(viewerId, request, cancellationToken);
            return Results.Ok(new { success = true });
        }
        catch (Exception ex)
        {
            return MapTerminalFailure(ex);
        }
    }

    private static async Task<IResult> GetInputDiagnosticsAsync(
        string viewerId,
        string sessionId,
        ITerminalSessionService terminalSessionService,
        TerminalServiceOptions options,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorized(context, options))
        {
            return Results.Unauthorized();
        }

        try
        {
            var diagnostics = await terminalSessionService.GetInputDiagnosticsAsync(viewerId, sessionId, cancellationToken);
            return Results.Ok(diagnostics);
        }
        catch (Exception ex)
        {
            return MapTerminalFailure(ex);
        }
    }

    private static async Task<IResult> ResizeSessionAsync(
        string viewerId,
        TerminalSessionResizeRequest request,
        ITerminalSessionService terminalSessionService,
        TerminalServiceOptions options,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorized(context, options))
        {
            return Results.Unauthorized();
        }

        try
        {
            await terminalSessionService.ResizeAsync(viewerId, request, cancellationToken);
            return Results.Ok(new { success = true });
        }
        catch (Exception ex)
        {
            return MapTerminalFailure(ex);
        }
    }

    private static async Task<IResult> SignalSessionAsync(
        string viewerId,
        string sessionId,
        TerminalSessionSignal signal,
        ITerminalSessionService terminalSessionService,
        TerminalServiceOptions options,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorized(context, options))
        {
            return Results.Unauthorized();
        }

        try
        {
            await terminalSessionService.SignalAsync(viewerId, sessionId, signal, cancellationToken);
            return Results.Ok(new { success = true });
        }
        catch (Exception ex)
        {
            return MapTerminalFailure(ex);
        }
    }

    private static async Task<IResult> CloseSessionAsync(
        string viewerId,
        string sessionId,
        ITerminalSessionService terminalSessionService,
        TerminalServiceOptions options,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorized(context, options))
        {
            return Results.Unauthorized();
        }

        try
        {
            await terminalSessionService.CloseAsync(viewerId, sessionId, cancellationToken);
            return Results.Ok(new { success = true });
        }
        catch (Exception ex)
        {
            return MapTerminalFailure(ex);
        }
    }

    private static async Task<IResult> CloseViewerSessionsAsync(
        string viewerId,
        ITerminalSessionService terminalSessionService,
        TerminalServiceOptions options,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorized(context, options))
        {
            return Results.Unauthorized();
        }

        try
        {
            await terminalSessionService.CloseViewerSessionsAsync(viewerId, cancellationToken);
            return Results.Ok(new { success = true });
        }
        catch (Exception ex)
        {
            return MapTerminalFailure(ex);
        }
    }

    private static async Task<IResult> CloseAllSessionsAsync(
        ITerminalSessionService terminalSessionService,
        TerminalServiceOptions options,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorized(context, options))
        {
            return Results.Unauthorized();
        }

        try
        {
            await terminalSessionService.CloseAllSessionsAsync(cancellationToken);
            return Results.Ok(new { success = true });
        }
        catch (Exception ex)
        {
            return MapTerminalFailure(ex);
        }
    }

    private static async Task StreamViewerEventsAsync(
        string viewerId,
        HttpContext context,
        HttpTerminalSessionEventPublisher publisher,
        TerminalServiceOptions options,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorized(context, options))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers.Connection = "keep-alive";

        await context.Response.WriteAsync(": connected\n\n", cancellationToken);
        await context.Response.Body.FlushAsync(cancellationToken);

        await foreach (var terminalEvent in publisher.StreamViewerEventsAsync(viewerId, cancellationToken))
        {
            var payload = JsonSerializer.Serialize(terminalEvent, JsonOptions);
            await context.Response.WriteAsync($"event: terminal.session\n", cancellationToken);
            await context.Response.WriteAsync($"data: {payload}\n\n", cancellationToken);
            await context.Response.Body.FlushAsync(cancellationToken);
        }
    }

    private static bool IsAuthorized(HttpContext context, TerminalServiceOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return true;
        }

        if (context.Request.Headers.TryGetValue(TerminalServiceOptions.ApiKeyHeaderName, out var apiKeyHeaderValues))
        {
            foreach (var apiKeyHeaderValue in apiKeyHeaderValues)
            {
                if (string.Equals(apiKeyHeaderValue, options.ApiKey, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        if (context.Request.Query.TryGetValue("api_key", out var queryValues))
        {
            foreach (var queryValue in queryValues)
            {
                if (string.Equals(queryValue, options.ApiKey, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static IResult MapTerminalFailure(Exception exception)
    {
        var root = exception.GetBaseException();

        return root switch
        {
            ArgumentException => Results.BadRequest(new { error = root.Message }),
            InvalidOperationException => Results.BadRequest(new { error = root.Message }),
            UnauthorizedAccessException => Results.Unauthorized(),
            _ => Results.Problem(
                title: "Terminal operation failed.",
                detail: root.Message,
                statusCode: StatusCodes.Status500InternalServerError)
        };
    }
}

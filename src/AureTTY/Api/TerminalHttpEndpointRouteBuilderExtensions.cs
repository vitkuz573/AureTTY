using System.Text.Json;
using AureTTY.Api.Models;
using AureTTY.Contracts.Abstractions;
using AureTTY.Contracts.DTOs;
using AureTTY.Serialization;
using AureTTY.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AureTTY.Api;

public static class TerminalHttpEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapTerminalHttpEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"/{TerminalApiRoutes.Health}",
            (TerminalServiceOptions options) =>
            {
                var transports = new List<string>(3);
                if (options.EnableHttpApi)
                {
                    transports.Add("http");
                    transports.Add("ws");
                }

                if (options.EnablePipeApi)
                {
                    transports.Add("pipe");
                }

                return Results.Json(
                    new TerminalHealthResponse
                    {
                        Status = "ok",
                        ApiVersion = TerminalServiceOptions.ApiVersion,
                        Transports = [.. transports],
                        AllowApiKeyQueryParameter = options.AllowApiKeyQueryParameter,
                        MaxConcurrentSessions = options.RuntimeLimits.MaxConcurrentSessions,
                        MaxSessionsPerViewer = options.RuntimeLimits.MaxSessionsPerViewer
                    },
                    AureTTYJsonSerializerContext.Default.TerminalHealthResponse);
            });

        app.MapGet($"/{TerminalApiRoutes.AllSessions}",
            async Task<IResult> (ITerminalSessionService terminalSessionService, CancellationToken cancellationToken) =>
            {
                try
                {
                    var sessions = await terminalSessionService.GetAllSessionsAsync(cancellationToken);
                    return Results.Json(
                        sessions.ToArray(),
                        AureTTYJsonSerializerContext.Default.TerminalSessionHandleArray);
                }
                catch (Exception ex)
                {
                    return TerminalApiProblemMapper.Map(ex);
                }
            });

        app.MapDelete($"/{TerminalApiRoutes.AllSessions}",
            async Task<IResult> (ITerminalSessionService terminalSessionService, CancellationToken cancellationToken) =>
            {
                try
                {
                    await terminalSessionService.CloseAllSessionsAsync(cancellationToken);
                    return Results.NoContent();
                }
                catch (Exception ex)
                {
                    return TerminalApiProblemMapper.Map(ex);
                }
            });

        app.MapGet($"/{TerminalApiRoutes.ViewerEvents}",
            async Task (string viewerId, HttpContext context, HttpTerminalSessionEventPublisher eventPublisher, CancellationToken cancellationToken) =>
            {
                if (string.IsNullOrWhiteSpace(viewerId))
                {
                    await Results.Json(
                        new ApiErrorResponse
                        {
                            Error = "ViewerId is required."
                        },
                        AureTTYJsonSerializerContext.Default.ApiErrorResponse,
                        statusCode: StatusCodes.Status400BadRequest).ExecuteAsync(context);
                    return;
                }

                context.Response.StatusCode = StatusCodes.Status200OK;
                context.Response.ContentType = "text/event-stream";
                context.Response.Headers.CacheControl = "no-cache";
                context.Response.Headers.Connection = "keep-alive";

                await context.Response.WriteAsync(": connected\n\n", cancellationToken);
                await context.Response.Body.FlushAsync(cancellationToken);

                await foreach (var terminalEvent in eventPublisher.StreamViewerEventsAsync(viewerId, cancellationToken))
                {
                    var payload = JsonSerializer.Serialize(
                        terminalEvent,
                        AureTTYJsonSerializerContext.Default.TerminalSessionEvent);
                    await context.Response.WriteAsync("event: terminal.session\n", cancellationToken);
                    await context.Response.WriteAsync($"data: {payload}\n\n", cancellationToken);
                    await context.Response.Body.FlushAsync(cancellationToken);
                }
            });

        app.MapGet($"/{TerminalApiRoutes.ViewerSessions}",
            async Task<IResult> (string viewerId, ITerminalSessionService terminalSessionService, CancellationToken cancellationToken) =>
            {
                try
                {
                    var sessions = await terminalSessionService.GetViewerSessionsAsync(viewerId, cancellationToken);
                    return Results.Json(
                        sessions.ToArray(),
                        AureTTYJsonSerializerContext.Default.TerminalSessionHandleArray);
                }
                catch (Exception ex)
                {
                    return TerminalApiProblemMapper.Map(ex);
                }
            });

        app.MapGet($"/{TerminalApiRoutes.ViewerSessions}/{{sessionId}}",
            async Task<IResult> (string viewerId, string sessionId, ITerminalSessionService terminalSessionService, CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await terminalSessionService.GetSessionAsync(viewerId, sessionId, cancellationToken);
                    return Results.Json(session, AureTTYJsonSerializerContext.Default.TerminalSessionHandle);
                }
                catch (Exception ex)
                {
                    return TerminalApiProblemMapper.Map(ex);
                }
            });

        app.MapPost($"/{TerminalApiRoutes.ViewerSessions}",
            async Task<IResult> (string viewerId, CreateTerminalSessionRequest request, ITerminalSessionService terminalSessionService, CancellationToken cancellationToken) =>
            {
                try
                {
                    var sessionId = string.IsNullOrWhiteSpace(request.SessionId)
                        ? Guid.NewGuid().ToString("N")
                        : request.SessionId.Trim();
                    var startRequest = new TerminalSessionStartRequest(sessionId, request.Shell)
                    {
                        RunContext = request.RunContext,
                        UserName = request.UserName,
                        Domain = request.Domain,
                        Password = request.Password,
                        LoadUserProfile = request.LoadUserProfile,
                        WorkingDirectory = request.WorkingDirectory,
                        Columns = request.Columns,
                        Rows = request.Rows
                    };

                    var handle = await terminalSessionService.StartAsync(viewerId, startRequest, cancellationToken);
                    var location = $"/{TerminalApiRoutes.ApiBase}/viewers/{Uri.EscapeDataString(viewerId)}/sessions/{Uri.EscapeDataString(handle.SessionId)}";
                    return Results.Created(location, handle);
                }
                catch (Exception ex)
                {
                    return TerminalApiProblemMapper.Map(ex);
                }
            });

        app.MapPost($"/{TerminalApiRoutes.ViewerSessions}/{{sessionId}}/attachments",
            async Task<IResult> (string viewerId, string sessionId, AttachTerminalSessionRequest request, ITerminalSessionService terminalSessionService, CancellationToken cancellationToken) =>
            {
                try
                {
                    var resumeRequest = new TerminalSessionResumeRequest(sessionId)
                    {
                        LastReceivedSequenceNumber = request.LastReceivedSequenceNumber,
                        Columns = request.Columns,
                        Rows = request.Rows
                    };
                    var handle = await terminalSessionService.ResumeAsync(viewerId, resumeRequest, cancellationToken);
                    return Results.Json(handle, AureTTYJsonSerializerContext.Default.TerminalSessionHandle);
                }
                catch (Exception ex)
                {
                    return TerminalApiProblemMapper.Map(ex);
                }
            });

        app.MapPost($"/{TerminalApiRoutes.ViewerSessions}/{{sessionId}}/inputs",
            async Task<IResult> (string viewerId, string sessionId, CreateTerminalInputRequest request, ITerminalSessionService terminalSessionService, CancellationToken cancellationToken) =>
            {
                try
                {
                    var inputRequest = new TerminalSessionInputRequest(sessionId, request.Text, request.Sequence);
                    await terminalSessionService.SendInputAsync(viewerId, inputRequest, cancellationToken);
                    return Results.Accepted();
                }
                catch (Exception ex)
                {
                    return TerminalApiProblemMapper.Map(ex);
                }
            });

        app.MapGet($"/{TerminalApiRoutes.ViewerSessions}/{{sessionId}}/input-diagnostics",
            async Task<IResult> (string viewerId, string sessionId, ITerminalSessionService terminalSessionService, CancellationToken cancellationToken) =>
            {
                try
                {
                    var diagnostics = await terminalSessionService.GetInputDiagnosticsAsync(viewerId, sessionId, cancellationToken);
                    return Results.Json(diagnostics, AureTTYJsonSerializerContext.Default.TerminalSessionInputDiagnostics);
                }
                catch (Exception ex)
                {
                    return TerminalApiProblemMapper.Map(ex);
                }
            });

        app.MapPut($"/{TerminalApiRoutes.ViewerSessions}/{{sessionId}}/terminal-size",
            async Task<IResult> (string viewerId, string sessionId, UpdateTerminalSizeRequest request, ITerminalSessionService terminalSessionService, CancellationToken cancellationToken) =>
            {
                try
                {
                    var resizeRequest = new TerminalSessionResizeRequest(sessionId, request.Columns, request.Rows);
                    await terminalSessionService.ResizeAsync(viewerId, resizeRequest, cancellationToken);
                    return Results.NoContent();
                }
                catch (Exception ex)
                {
                    return TerminalApiProblemMapper.Map(ex);
                }
            });

        app.MapPost($"/{TerminalApiRoutes.ViewerSessions}/{{sessionId}}/signals",
            async Task<IResult> (string viewerId, string sessionId, CreateTerminalSignalRequest request, ITerminalSessionService terminalSessionService, CancellationToken cancellationToken) =>
            {
                try
                {
                    await terminalSessionService.SignalAsync(viewerId, sessionId, request.Signal, cancellationToken);
                    return Results.Accepted();
                }
                catch (Exception ex)
                {
                    return TerminalApiProblemMapper.Map(ex);
                }
            });

        app.MapDelete($"/{TerminalApiRoutes.ViewerSessions}/{{sessionId}}",
            async Task<IResult> (string viewerId, string sessionId, ITerminalSessionService terminalSessionService, CancellationToken cancellationToken) =>
            {
                try
                {
                    await terminalSessionService.CloseAsync(viewerId, sessionId, cancellationToken);
                    return Results.NoContent();
                }
                catch (Exception ex)
                {
                    return TerminalApiProblemMapper.Map(ex);
                }
            });

        app.MapDelete($"/{TerminalApiRoutes.ViewerSessions}",
            async Task<IResult> (string viewerId, ITerminalSessionService terminalSessionService, CancellationToken cancellationToken) =>
            {
                try
                {
                    await terminalSessionService.CloseViewerSessionsAsync(viewerId, cancellationToken);
                    return Results.NoContent();
                }
                catch (Exception ex)
                {
                    return TerminalApiProblemMapper.Map(ex);
                }
            });

        app.MapGet($"/{TerminalApiRoutes.ViewerWebSocket}",
            (string viewerId, HttpContext context) => TerminalWebSocketHandler.HandleAsync(viewerId, context));

        return app;
    }
}

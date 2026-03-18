using System.Text.Json;
using AureTTY.Api;
using AureTTY.Services;
using Microsoft.AspNetCore.Mvc;

namespace AureTTY.Controllers;

[ApiController]
[Route(TerminalApiRoutes.ViewerEvents)]
public sealed class TerminalViewerEventsController(HttpTerminalSessionEventPublisher eventPublisher) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpTerminalSessionEventPublisher _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));

    [HttpGet]
    [Produces("text/event-stream")]
    public async Task GetAsync(string viewerId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(viewerId))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await Response.WriteAsJsonAsync(new { error = "ViewerId is required." }, cancellationToken);
            return;
        }

        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        await Response.WriteAsync(": connected\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);

        await foreach (var terminalEvent in _eventPublisher.StreamViewerEventsAsync(viewerId, cancellationToken))
        {
            var payload = JsonSerializer.Serialize(terminalEvent, JsonOptions);
            await Response.WriteAsync("event: terminal.session\n", cancellationToken);
            await Response.WriteAsync($"data: {payload}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }
}

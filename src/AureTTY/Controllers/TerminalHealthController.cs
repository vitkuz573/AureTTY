using AureTTY.Api;
using AureTTY.Api.Models;
using AureTTY.Services;
using Microsoft.AspNetCore.Mvc;

namespace AureTTY.Controllers;

[ApiController]
[Route(TerminalApiRoutes.Health)]
public sealed class TerminalHealthController(TerminalServiceOptions options) : ControllerBase
{
    private readonly TerminalServiceOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    [HttpGet]
    [ProducesResponseType(typeof(TerminalHealthResponse), StatusCodes.Status200OK)]
    public ActionResult<TerminalHealthResponse> Get()
    {
        var transports = new List<string>(2);
        if (_options.EnableHttpApi)
        {
            transports.Add("http");
        }

        if (_options.EnablePipeApi)
        {
            transports.Add("pipe");
        }

        return Ok(new TerminalHealthResponse
        {
            Status = "ok",
            ApiVersion = TerminalServiceOptions.ApiVersion,
            Transports = [.. transports]
        });
    }
}

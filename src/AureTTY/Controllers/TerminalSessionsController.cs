using AureTTY.Api;
using AureTTY.Contracts.Abstractions;
using AureTTY.Contracts.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace AureTTY.Controllers;

[ApiController]
[Route(TerminalApiRoutes.AllSessions)]
public sealed class TerminalSessionsController(ITerminalSessionService terminalSessionService) : ControllerBase
{
    private readonly ITerminalSessionService _terminalSessionService = terminalSessionService ?? throw new ArgumentNullException(nameof(terminalSessionService));

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<TerminalSessionHandle>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<TerminalSessionHandle>>> GetAllSessionsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var sessions = await _terminalSessionService.GetAllSessionsAsync(cancellationToken);
            return Ok(sessions);
        }
        catch (Exception ex)
        {
            return TerminalApiProblemMapper.Map(this, ex);
        }
    }

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteAllSessionsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _terminalSessionService.CloseAllSessionsAsync(cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            return TerminalApiProblemMapper.Map(this, ex);
        }
    }
}

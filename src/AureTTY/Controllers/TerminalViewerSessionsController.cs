using AureTTY.Api;
using AureTTY.Api.Models;
using AureTTY.Contracts.Abstractions;
using AureTTY.Contracts.DTOs;
using AureTTY.Contracts.Enums;
using Microsoft.AspNetCore.Mvc;

namespace AureTTY.Controllers;

[ApiController]
[Route(TerminalApiRoutes.ViewerSessions)]
public sealed class TerminalViewerSessionsController(ITerminalSessionService terminalSessionService) : ControllerBase
{
    private readonly ITerminalSessionService _terminalSessionService = terminalSessionService ?? throw new ArgumentNullException(nameof(terminalSessionService));

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<TerminalSessionHandle>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<TerminalSessionHandle>>> GetViewerSessionsAsync(
        string viewerId,
        CancellationToken cancellationToken)
    {
        try
        {
            var sessions = await _terminalSessionService.GetViewerSessionsAsync(viewerId, cancellationToken);
            return Ok(sessions);
        }
        catch (Exception ex)
        {
            return TerminalApiProblemMapper.Map(this, ex);
        }
    }

    [HttpGet("{sessionId}")]
    [ProducesResponseType(typeof(TerminalSessionHandle), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TerminalSessionHandle>> GetSessionAsync(
        string viewerId,
        string sessionId,
        CancellationToken cancellationToken)
    {
        try
        {
            var session = await _terminalSessionService.GetSessionAsync(viewerId, sessionId, cancellationToken);
            return Ok(session);
        }
        catch (Exception ex)
        {
            return TerminalApiProblemMapper.Map(this, ex);
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(TerminalSessionHandle), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TerminalSessionHandle>> CreateSessionAsync(
        string viewerId,
        [FromBody] CreateTerminalSessionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var sessionId = string.IsNullOrWhiteSpace(request.SessionId)
                ? Guid.NewGuid().ToString("N")
                : request.SessionId.Trim();
            var shell = request.Shell ?? ResolveDefaultShell();
            var startRequest = new TerminalSessionStartRequest(sessionId, shell)
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

            var handle = await _terminalSessionService.StartAsync(viewerId, startRequest, cancellationToken);
            var location = $"/{TerminalApiRoutes.ApiBase}/viewers/{Uri.EscapeDataString(viewerId)}/sessions/{Uri.EscapeDataString(handle.SessionId)}";
            return Created(location, handle);
        }
        catch (Exception ex)
        {
            return TerminalApiProblemMapper.Map(this, ex);
        }
    }

    [HttpPost("{sessionId}/attachments")]
    [ProducesResponseType(typeof(TerminalSessionHandle), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TerminalSessionHandle>> AttachSessionAsync(
        string viewerId,
        string sessionId,
        [FromBody] AttachTerminalSessionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var resumeRequest = new TerminalSessionResumeRequest(sessionId)
            {
                LastReceivedSequenceNumber = request.LastReceivedSequenceNumber,
                Columns = request.Columns,
                Rows = request.Rows
            };
            var handle = await _terminalSessionService.ResumeAsync(viewerId, resumeRequest, cancellationToken);
            return Ok(handle);
        }
        catch (Exception ex)
        {
            return TerminalApiProblemMapper.Map(this, ex);
        }
    }

    [HttpPost("{sessionId}/inputs")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateInputAsync(
        string viewerId,
        string sessionId,
        [FromBody] CreateTerminalInputRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var inputRequest = new TerminalSessionInputRequest(sessionId, request.Text, request.Sequence);
            await _terminalSessionService.SendInputAsync(viewerId, inputRequest, cancellationToken);
            return Accepted();
        }
        catch (Exception ex)
        {
            return TerminalApiProblemMapper.Map(this, ex);
        }
    }

    [HttpGet("{sessionId}/input-diagnostics")]
    [ProducesResponseType(typeof(TerminalSessionInputDiagnostics), StatusCodes.Status200OK)]
    public async Task<ActionResult<TerminalSessionInputDiagnostics>> GetInputDiagnosticsAsync(
        string viewerId,
        string sessionId,
        CancellationToken cancellationToken)
    {
        try
        {
            var diagnostics = await _terminalSessionService.GetInputDiagnosticsAsync(viewerId, sessionId, cancellationToken);
            return Ok(diagnostics);
        }
        catch (Exception ex)
        {
            return TerminalApiProblemMapper.Map(this, ex);
        }
    }

    [HttpPut("{sessionId}/terminal-size")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdateTerminalSizeAsync(
        string viewerId,
        string sessionId,
        [FromBody] UpdateTerminalSizeRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var resizeRequest = new TerminalSessionResizeRequest(sessionId, request.Columns, request.Rows);
            await _terminalSessionService.ResizeAsync(viewerId, resizeRequest, cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            return TerminalApiProblemMapper.Map(this, ex);
        }
    }

    [HttpPost("{sessionId}/signals")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> CreateSignalAsync(
        string viewerId,
        string sessionId,
        [FromBody] CreateTerminalSignalRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _terminalSessionService.SignalAsync(viewerId, sessionId, request.Signal, cancellationToken);
            return Accepted();
        }
        catch (Exception ex)
        {
            return TerminalApiProblemMapper.Map(this, ex);
        }
    }

    [HttpDelete("{sessionId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteSessionAsync(
        string viewerId,
        string sessionId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _terminalSessionService.CloseAsync(viewerId, sessionId, cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            return TerminalApiProblemMapper.Map(this, ex);
        }
    }

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteViewerSessionsAsync(string viewerId, CancellationToken cancellationToken)
    {
        try
        {
            await _terminalSessionService.CloseViewerSessionsAsync(viewerId, cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            return TerminalApiProblemMapper.Map(this, ex);
        }
    }

    private static Shell ResolveDefaultShell()
    {
        return OperatingSystem.IsWindows()
            ? Shell.Pwsh
            : Shell.Sh;
    }
}

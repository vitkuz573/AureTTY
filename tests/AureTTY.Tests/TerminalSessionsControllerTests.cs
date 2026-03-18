using System.Text.Json;
using AureTTY.Contracts.Abstractions;
using AureTTY.Contracts.DTOs;
using AureTTY.Contracts.Exceptions;
using AureTTY.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace AureTTY.Tests;

public sealed class TerminalSessionsControllerTests
{
    [Fact]
    public void Constructor_WhenServiceNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new TerminalSessionsController(null!));
    }

    [Fact]
    public async Task GetAllSessionsAsync_WhenSuccessful_ReturnsOkWithSessions()
    {
        var expected = new[]
        {
            new TerminalSessionHandle("session-1"),
            new TerminalSessionHandle("session-2")
        };

        var terminalSessionService = new Mock<ITerminalSessionService>(MockBehavior.Strict);
        terminalSessionService
            .Setup(service => service.GetAllSessionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = new TerminalSessionsController(terminalSessionService.Object);

        var result = await controller.GetAllSessionsAsync(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsAssignableFrom<IReadOnlyCollection<TerminalSessionHandle>>(ok.Value);
        Assert.Equal(2, payload.Count);
        terminalSessionService.VerifyAll();
    }

    [Fact]
    public async Task GetAllSessionsAsync_WhenConflictExceptionThrown_ReturnsConflict()
    {
        var terminalSessionService = new Mock<ITerminalSessionService>(MockBehavior.Strict);
        terminalSessionService
            .Setup(service => service.GetAllSessionsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TerminalSessionConflictException("session limit"));

        var controller = new TerminalSessionsController(terminalSessionService.Object);

        var result = await controller.GetAllSessionsAsync(CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
        Assert.Equal("session limit", ExtractErrorMessage(conflict.Value));
        terminalSessionService.VerifyAll();
    }

    [Fact]
    public async Task DeleteAllSessionsAsync_WhenSuccessful_ReturnsNoContent()
    {
        var terminalSessionService = new Mock<ITerminalSessionService>(MockBehavior.Strict);
        terminalSessionService
            .Setup(service => service.CloseAllSessionsAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = new TerminalSessionsController(terminalSessionService.Object);

        var result = await controller.DeleteAllSessionsAsync(CancellationToken.None);

        _ = Assert.IsType<NoContentResult>(result);
        terminalSessionService.VerifyAll();
    }

    [Fact]
    public async Task DeleteAllSessionsAsync_WhenUnauthorizedAccessThrown_ReturnsUnauthorized()
    {
        var terminalSessionService = new Mock<ITerminalSessionService>(MockBehavior.Strict);
        terminalSessionService
            .Setup(service => service.CloseAllSessionsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("forbidden"));

        var controller = new TerminalSessionsController(terminalSessionService.Object);

        var result = await controller.DeleteAllSessionsAsync(CancellationToken.None);

        _ = Assert.IsType<UnauthorizedResult>(result);
        terminalSessionService.VerifyAll();
    }

    private static string? ExtractErrorMessage(object? value)
    {
        if (value is null)
        {
            return null;
        }

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(value));
        return document.RootElement.TryGetProperty("error", out var errorProperty)
            ? errorProperty.GetString()
            : null;
    }
}

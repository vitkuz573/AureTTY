using AureTTY.Contracts.DTOs;
using AureTTY.Contracts.Enums;
using AureTTY.Protocol;
using AureTTY.Services;

namespace AureTTY.Tests;

public sealed class PipeTerminalSessionEventPublisherTests
{
    [Fact]
    public async Task SendTerminalSessionEventAsync_WhenSinkIsMissing_CompletesWithoutThrowing()
    {
        var sut = new PipeTerminalSessionEventPublisher();
        var terminalEvent = new TerminalSessionEvent("session-1", TerminalSessionEventType.Output)
        {
            Text = "hello"
        };

        await sut.SendTerminalSessionEventAsync("viewer-1", terminalEvent);
    }

    [Fact]
    public async Task SendTerminalSessionEventAsync_WhenSinkIsConfigured_ForwardsPayload()
    {
        var sut = new PipeTerminalSessionEventPublisher();
        TerminalIpcSessionEvent? captured = null;

        sut.SetSink((payload, _) =>
        {
            captured = payload;
            return Task.CompletedTask;
        });

        var terminalEvent = new TerminalSessionEvent("session-2", TerminalSessionEventType.Started);
        await sut.SendTerminalSessionEventAsync("viewer-2", terminalEvent);

        Assert.NotNull(captured);
        Assert.Equal("viewer-2", captured.ViewerId);
        Assert.Same(terminalEvent, captured.Event);
    }
}

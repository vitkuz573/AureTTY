using AureTTY.Contracts.DTOs;
using AureTTY.Contracts.Enums;
using AureTTY.Services;

namespace AureTTY.Tests;

public sealed class WebSocketTerminalSessionEventPublisherTests
{
    [Fact]
    public async Task SendTerminalSessionEventAsync_WhenMatchingViewer_DeliversEvent()
    {
        var sut = new WebSocketTerminalSessionEventPublisher();
        var connectionId = "conn-1";
        var reader = sut.RegisterConnection(connectionId, "viewer-1");

        var expectedEvent = new TerminalSessionEvent("session-1", TerminalSessionEventType.Output)
        {
            Text = "hello"
        };

        await sut.SendTerminalSessionEventAsync("viewer-1", expectedEvent);

        Assert.True(reader.TryRead(out var actualEvent));
        Assert.Same(expectedEvent, actualEvent);

        sut.UnregisterConnection(connectionId);
    }

    [Fact]
    public async Task SendTerminalSessionEventAsync_WhenNonMatchingViewer_DoesNotDeliverEvent()
    {
        var sut = new WebSocketTerminalSessionEventPublisher();
        var connectionId = "conn-1";
        var reader = sut.RegisterConnection(connectionId, "viewer-1");

        await sut.SendTerminalSessionEventAsync(
            "viewer-2",
            new TerminalSessionEvent("session-2", TerminalSessionEventType.Output)
            {
                Text = "should-be-ignored"
            });

        Assert.False(reader.TryRead(out _));

        sut.UnregisterConnection(connectionId);
    }

    [Fact]
    public async Task SendTerminalSessionEventAsync_WhenSlowSubscriber_RecordsDroppedEvents()
    {
        var options = new TerminalServiceOptions(
            PipeName: "pipe-test",
            PipeToken: "pipe-token",
            EnablePipeApi: false,
            EnableHttpApi: true,
            HttpListenUrl: "http://127.0.0.1:17850",
            ApiKey: "api-key")
        {
            SseSubscriptionBufferCapacity = 1
        };
        var metrics = new AureTTY.Core.Services.TerminalMetrics();
        var sut = new WebSocketTerminalSessionEventPublisher(options, metrics);

        var connectionId = "conn-slow";
        var reader = sut.RegisterConnection(connectionId, "viewer-1");

        // Fill the buffer
        await sut.SendTerminalSessionEventAsync(
            "viewer-1",
            new TerminalSessionEvent("session-1", TerminalSessionEventType.Output)
            {
                Text = "event-1"
            });

        // Read the first event to confirm delivery
        Assert.True(reader.TryRead(out var firstEvent));
        Assert.Equal("event-1", firstEvent!.Text);

        // Now fill again and overflow
        await sut.SendTerminalSessionEventAsync(
            "viewer-1",
            new TerminalSessionEvent("session-1", TerminalSessionEventType.Output)
            {
                Text = "event-fill"
            });

        // This should overflow since buffer is 1 and we haven't read
        for (var i = 0; i < 5; i++)
        {
            await sut.SendTerminalSessionEventAsync(
                "viewer-1",
                new TerminalSessionEvent("session-1", TerminalSessionEventType.Output)
                {
                    Text = $"event-overflow-{i}"
                });
        }

        var dropped = sut.ConsumeDroppedEvents(connectionId);
        Assert.True(dropped > 0);

        sut.UnregisterConnection(connectionId);
    }

    [Fact]
    public void UnregisterConnection_WhenCalled_CompletesChannel()
    {
        var sut = new WebSocketTerminalSessionEventPublisher();
        var connectionId = "conn-1";
        var reader = sut.RegisterConnection(connectionId, "viewer-1");

        sut.UnregisterConnection(connectionId);

        Assert.True(reader.Completion.IsCompleted);
    }
}

using AureTTY.Contracts.DTOs;
using AureTTY.Contracts.Enums;
using AureTTY.Services;

namespace AureTTY.Tests;

public sealed class HttpTerminalSessionEventPublisherTests
{
    [Fact]
    public async Task StreamViewerEventsAsync_WhenMatchingViewerEventIsPublished_ReturnsEvent()
    {
        var sut = new HttpTerminalSessionEventPublisher();
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var readTask = Task.Run(async () =>
        {
            await foreach (var terminalEvent in sut.StreamViewerEventsAsync("viewer-1", cancellationTokenSource.Token))
            {
                return terminalEvent;
            }

            return null;
        }, cancellationTokenSource.Token);

        var expectedEvent = new TerminalSessionEvent("session-1", TerminalSessionEventType.Output)
        {
            Text = "hello"
        };

        for (var attempt = 0; attempt < 20 && !readTask.IsCompleted; attempt++)
        {
            await sut.SendTerminalSessionEventAsync("viewer-1", expectedEvent);
            await Task.Delay(25, cancellationTokenSource.Token);
        }

        var actualEvent = await readTask.WaitAsync(cancellationTokenSource.Token);

        Assert.NotNull(actualEvent);
        Assert.Same(expectedEvent, actualEvent);
    }

    [Fact]
    public async Task StreamViewerEventsAsync_WhenOtherViewerEventIsPublished_IgnoresEvent()
    {
        var sut = new HttpTerminalSessionEventPublisher();
        using var cancellationTokenSource = new CancellationTokenSource();
        var receivedEvent = new TaskCompletionSource<TerminalSessionEvent>(TaskCreationOptions.RunContinuationsAsynchronously);

        var streamTask = Task.Run(async () =>
        {
            await foreach (var terminalEvent in sut.StreamViewerEventsAsync("viewer-1", cancellationTokenSource.Token))
            {
                receivedEvent.TrySetResult(terminalEvent);
                break;
            }
        }, cancellationTokenSource.Token);

        await sut.SendTerminalSessionEventAsync(
            "viewer-2",
            new TerminalSessionEvent("session-2", TerminalSessionEventType.Output)
            {
                Text = "should-be-ignored"
            });

        await Task.Delay(100);
        Assert.False(receivedEvent.Task.IsCompleted);

        cancellationTokenSource.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await streamTask);
    }
}

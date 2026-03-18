using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using AureTTY.Contracts.Abstractions;
using AureTTY.Contracts.DTOs;

namespace AureTTY.Services;

public sealed class HttpTerminalSessionEventPublisher : ITerminalSessionEventPublisher
{
    private const int SubscriptionBufferCapacity = 2048;
    private readonly ConcurrentDictionary<string, EventSubscription> _subscriptions = new(StringComparer.Ordinal);

    public Task SendTerminalSessionEventAsync(string viewerId, TerminalSessionEvent terminalSessionEvent)
    {
        foreach (var subscription in _subscriptions.Values)
        {
            if (!string.Equals(subscription.ViewerId, viewerId, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(subscription.ViewerId, "*", StringComparison.Ordinal))
            {
                continue;
            }

            _ = subscription.Events.Writer.TryWrite(terminalSessionEvent);
        }

        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<TerminalSessionEvent> StreamViewerEventsAsync(
        string viewerId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(viewerId);

        var subscriptionId = Guid.NewGuid().ToString("N");
        var channel = Channel.CreateBounded<TerminalSessionEvent>(new BoundedChannelOptions(SubscriptionBufferCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });
        var subscription = new EventSubscription(viewerId, channel);

        if (!_subscriptions.TryAdd(subscriptionId, subscription))
        {
            throw new InvalidOperationException("Failed to create HTTP event subscription.");
        }

        try
        {
            await foreach (var terminalEvent in channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return terminalEvent;
            }
        }
        finally
        {
            if (_subscriptions.TryRemove(subscriptionId, out var removed))
            {
                removed.Events.Writer.TryComplete();
            }
        }
    }

    private sealed record EventSubscription(
        string ViewerId,
        Channel<TerminalSessionEvent> Events);
}

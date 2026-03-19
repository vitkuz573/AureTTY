using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using AureTTY.Contracts.Abstractions;
using AureTTY.Contracts.DTOs;
using AureTTY.Contracts.Enums;
using AureTTY.Core.Services;

namespace AureTTY.Services;

public sealed class HttpTerminalSessionEventPublisher : ITerminalSessionEventPublisher
{
    private const string SystemSessionId = "__auretty__";
    private readonly ConcurrentDictionary<string, EventSubscription> _subscriptions = new(StringComparer.Ordinal);
    private readonly int _subscriptionBufferCapacity;
    private readonly TerminalMetrics? _metrics;

    public HttpTerminalSessionEventPublisher(TerminalServiceOptions? options = null, TerminalMetrics? metrics = null)
    {
        _subscriptionBufferCapacity = Math.Max(
            1,
            options?.SseSubscriptionBufferCapacity ?? TerminalServiceOptions.DefaultSseSubscriptionBufferCapacity);
        _metrics = metrics;
    }

    public Task SendTerminalSessionEventAsync(string viewerId, TerminalSessionEvent terminalSessionEvent)
    {
        foreach (var subscription in _subscriptions.Values)
        {
            if (!string.Equals(subscription.ViewerId, viewerId, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(subscription.ViewerId, "*", StringComparison.Ordinal))
            {
                continue;
            }

            if (subscription.Events.Writer.TryWrite(terminalSessionEvent))
            {
                continue;
            }

            subscription.RecordDroppedEvent();
            _metrics?.RecordSseEventDropped(1);
        }

        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<TerminalSessionEvent> StreamViewerEventsAsync(
        string viewerId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(viewerId);

        var subscriptionId = Guid.NewGuid().ToString("N");
        var channel = Channel.CreateBounded<TerminalSessionEvent>(new BoundedChannelOptions(_subscriptionBufferCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
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
                var droppedEvents = subscription.ConsumeDroppedEvents();
                if (droppedEvents > 0)
                {
                    yield return new TerminalSessionEvent(SystemSessionId, TerminalSessionEventType.Dropped)
                    {
                        State = TerminalSessionState.Running,
                        Error = $"Dropped {droppedEvents} SSE event(s) because subscriber is slow."
                    };
                }

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
        Channel<TerminalSessionEvent> Events)
    {
        private long _droppedEvents;

        public void RecordDroppedEvent()
        {
            Interlocked.Increment(ref _droppedEvents);
        }

        public long ConsumeDroppedEvents()
        {
            return Interlocked.Exchange(ref _droppedEvents, 0);
        }
    }
}

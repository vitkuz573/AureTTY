using System.Collections.Concurrent;
using System.Threading.Channels;
using AureTTY.Contracts.Abstractions;
using AureTTY.Contracts.DTOs;
using AureTTY.Contracts.Enums;
using AureTTY.Core.Services;

namespace AureTTY.Services;

public sealed class WebSocketTerminalSessionEventPublisher : ITerminalSessionEventPublisher
{
    private const string SystemSessionId = "__auretty__";
    private readonly ConcurrentDictionary<string, EventSubscription> _subscriptions = new(StringComparer.Ordinal);
    private readonly int _subscriptionBufferCapacity;
    private readonly TerminalMetrics? _metrics;

    public WebSocketTerminalSessionEventPublisher(TerminalServiceOptions? options = null, TerminalMetrics? metrics = null)
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
            _metrics?.RecordWsEventDropped(1);
        }

        return Task.CompletedTask;
    }

    public ChannelReader<TerminalSessionEvent> RegisterConnection(string connectionId, string viewerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(viewerId);

        var channel = Channel.CreateBounded<TerminalSessionEvent>(new BoundedChannelOptions(_subscriptionBufferCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });
        var subscription = new EventSubscription(viewerId, channel);

        if (!_subscriptions.TryAdd(connectionId, subscription))
        {
            throw new InvalidOperationException($"WebSocket connection '{connectionId}' is already registered.");
        }

        return channel.Reader;
    }

    public void UnregisterConnection(string connectionId)
    {
        if (_subscriptions.TryRemove(connectionId, out var removed))
        {
            removed.Events.Writer.TryComplete();
        }
    }

    public long ConsumeDroppedEvents(string connectionId)
    {
        if (_subscriptions.TryGetValue(connectionId, out var subscription))
        {
            return subscription.ConsumeDroppedEvents();
        }

        return 0;
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

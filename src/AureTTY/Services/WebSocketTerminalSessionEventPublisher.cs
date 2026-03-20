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
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> _sessionSubscriptions = new(StringComparer.Ordinal);
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
            var isViewerMatch = string.Equals(subscription.ViewerId, viewerId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(subscription.ViewerId, "*", StringComparison.Ordinal);

            if (!isViewerMatch)
            {
                continue;
            }

            if (subscription.IsMultiplexed)
            {
                if (!_sessionSubscriptions.TryGetValue(subscription.ConnectionId, out var sessionMap))
                {
                    continue;
                }

                if (!sessionMap.ContainsKey(terminalSessionEvent.SessionId))
                {
                    continue;
                }
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
        var subscription = new EventSubscription(connectionId, viewerId, channel, IsMultiplexed: false);

        if (!_subscriptions.TryAdd(connectionId, subscription))
        {
            throw new InvalidOperationException($"WebSocket connection '{connectionId}' is already registered.");
        }

        return channel.Reader;
    }

    public ChannelReader<TerminalSessionEvent> RegisterMultiplexedConnection(string connectionId, string viewerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(viewerId);

        var channel = Channel.CreateBounded<TerminalSessionEvent>(new BoundedChannelOptions(_subscriptionBufferCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });
        var subscription = new EventSubscription(connectionId, viewerId, channel, IsMultiplexed: true);

        if (!_subscriptions.TryAdd(connectionId, subscription))
        {
            throw new InvalidOperationException($"WebSocket connection '{connectionId}' is already registered.");
        }

        _sessionSubscriptions.TryAdd(connectionId, new ConcurrentDictionary<string, string>(StringComparer.Ordinal));

        return channel.Reader;
    }

    public void RegisterSessionSubscription(string connectionId, string sessionId, string viewerId)
    {
        if (_sessionSubscriptions.TryGetValue(connectionId, out var sessionMap))
        {
            sessionMap.TryAdd(sessionId, viewerId);
        }
    }

    public void UnregisterSessionSubscription(string connectionId, string sessionId)
    {
        if (_sessionSubscriptions.TryGetValue(connectionId, out var sessionMap))
        {
            sessionMap.TryRemove(sessionId, out _);
        }
    }

    public void UnregisterConnection(string connectionId)
    {
        if (_subscriptions.TryRemove(connectionId, out var removed))
        {
            removed.Events.Writer.TryComplete();
        }

        _sessionSubscriptions.TryRemove(connectionId, out _);
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
        string ConnectionId,
        string ViewerId,
        Channel<TerminalSessionEvent> Events,
        bool IsMultiplexed)
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

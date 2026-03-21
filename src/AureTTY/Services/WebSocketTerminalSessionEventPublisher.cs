using System.Collections.Concurrent;
using System.Threading.Channels;
using AureTTY.Contracts.Abstractions;
using AureTTY.Contracts.DTOs;
using AureTTY.Core.Services;

namespace AureTTY.Services;

public sealed class WebSocketTerminalSessionEventPublisher : ITerminalSessionEventPublisher
{
    private readonly ConcurrentDictionary<string, EventSubscription> _subscriptions = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _viewerSubscriptions = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<SessionSubscriptionKey, ConcurrentDictionary<string, byte>> _sessionSubscriptions = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, SessionSubscriptionKey>> _connectionSessionIndex = new(StringComparer.Ordinal);
    private readonly int _subscriptionBufferCapacity;
    private readonly TerminalMetrics? _metrics;

    public WebSocketTerminalSessionEventPublisher(TerminalServiceOptions? options = null, TerminalMetrics? metrics = null)
    {
        _subscriptionBufferCapacity = Math.Max(
            1,
            options?.WebSocketSubscriptionBufferCapacity ?? TerminalServiceOptions.DefaultWebSocketSubscriptionBufferCapacity);
        _metrics = metrics;
    }

    public Task SendTerminalSessionEventAsync(string viewerId, TerminalSessionEvent terminalSessionEvent)
    {
        if (_viewerSubscriptions.TryGetValue(viewerId, out var viewerConnectionIds))
        {
            DispatchToConnections(viewerConnectionIds, terminalSessionEvent);
        }

        var sessionKey = new SessionSubscriptionKey(viewerId, terminalSessionEvent.SessionId);
        if (_sessionSubscriptions.TryGetValue(sessionKey, out var sessionConnectionIds))
        {
            DispatchToConnections(sessionConnectionIds, terminalSessionEvent);
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

        var viewerConnections = _viewerSubscriptions.GetOrAdd(viewerId, static _ => new ConcurrentDictionary<string, byte>(StringComparer.Ordinal));
        viewerConnections.TryAdd(connectionId, 0);
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

        _connectionSessionIndex.TryAdd(connectionId, new ConcurrentDictionary<string, SessionSubscriptionKey>(StringComparer.Ordinal));
        return channel.Reader;
    }

    public void RegisterSessionSubscription(string connectionId, string sessionId, string viewerId)
    {
        if (!_subscriptions.TryGetValue(connectionId, out var subscription) ||
            !subscription.IsMultiplexed ||
            !string.Equals(subscription.ViewerId, viewerId, StringComparison.Ordinal))
        {
            return;
        }

        var key = new SessionSubscriptionKey(viewerId, sessionId);
        var connections = _sessionSubscriptions.GetOrAdd(key, static _ => new ConcurrentDictionary<string, byte>(StringComparer.Ordinal));
        connections.TryAdd(connectionId, 0);

        var sessionIndex = _connectionSessionIndex.GetOrAdd(connectionId, static _ => new ConcurrentDictionary<string, SessionSubscriptionKey>(StringComparer.Ordinal));
        sessionIndex[sessionId] = key;
    }

    public void UnregisterSessionSubscription(string connectionId, string sessionId)
    {
        if (!_connectionSessionIndex.TryGetValue(connectionId, out var sessionIndex) ||
            !sessionIndex.TryRemove(sessionId, out var key))
        {
            return;
        }

        RemoveConnectionFromSessionKey(connectionId, key);
    }

    public void UnregisterConnection(string connectionId)
    {
        if (!_subscriptions.TryRemove(connectionId, out var removed))
        {
            _connectionSessionIndex.TryRemove(connectionId, out _);
            return;
        }

        if (removed.IsMultiplexed)
        {
            if (_connectionSessionIndex.TryRemove(connectionId, out var sessionIndex))
            {
                foreach (var key in sessionIndex.Values)
                {
                    RemoveConnectionFromSessionKey(connectionId, key);
                }
            }
        }
        else
        {
            if (_viewerSubscriptions.TryGetValue(removed.ViewerId, out var viewerConnections))
            {
                viewerConnections.TryRemove(connectionId, out _);
                if (viewerConnections.IsEmpty)
                {
                    _viewerSubscriptions.TryRemove(removed.ViewerId, out _);
                }
            }
        }

        removed.Events.Writer.TryComplete();
    }

    public long ConsumeDroppedEvents(string connectionId)
    {
        if (_subscriptions.TryGetValue(connectionId, out var subscription))
        {
            return subscription.ConsumeDroppedEvents();
        }

        return 0;
    }

    private void DispatchToConnections(ConcurrentDictionary<string, byte> connectionIds, TerminalSessionEvent terminalSessionEvent)
    {
        foreach (var connectionId in connectionIds.Keys)
        {
            if (!_subscriptions.TryGetValue(connectionId, out var subscription))
            {
                connectionIds.TryRemove(connectionId, out _);
                continue;
            }

            if (subscription.Events.Writer.TryWrite(terminalSessionEvent))
            {
                continue;
            }

            subscription.RecordDroppedEvent();
            _metrics?.RecordWsEventDropped(1);
        }
    }

    private void RemoveConnectionFromSessionKey(string connectionId, SessionSubscriptionKey key)
    {
        if (!_sessionSubscriptions.TryGetValue(key, out var sessionConnections))
        {
            return;
        }

        sessionConnections.TryRemove(connectionId, out _);
        if (sessionConnections.IsEmpty)
        {
            _sessionSubscriptions.TryRemove(key, out _);
        }
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

    private readonly record struct SessionSubscriptionKey(string ViewerId, string SessionId);
}

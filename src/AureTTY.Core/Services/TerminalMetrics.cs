using System.Diagnostics.Metrics;

namespace AureTTY.Core.Services;

public sealed class TerminalMetrics
{
    private readonly Meter _meter = new("AureTTY.Terminal", "1.0.0");
    private readonly Counter<long> _sessionStartedCounter;
    private readonly Counter<long> _sessionClosedCounter;
    private readonly Counter<long> _sessionFailedCounter;
    private readonly Counter<long> _sessionRejectedCounter;
    private readonly Counter<long> _inputAcceptedCounter;
    private readonly Counter<long> _inputRejectedCounter;
    private readonly Counter<long> _wsEventDroppedCounter;

    public TerminalMetrics()
    {
        _sessionStartedCounter = _meter.CreateCounter<long>("auretty.sessions.started");
        _sessionClosedCounter = _meter.CreateCounter<long>("auretty.sessions.closed");
        _sessionFailedCounter = _meter.CreateCounter<long>("auretty.sessions.failed");
        _sessionRejectedCounter = _meter.CreateCounter<long>("auretty.sessions.rejected");
        _inputAcceptedCounter = _meter.CreateCounter<long>("auretty.input.chunks.accepted");
        _inputRejectedCounter = _meter.CreateCounter<long>("auretty.input.chunks.rejected");
        _wsEventDroppedCounter = _meter.CreateCounter<long>("auretty.ws.events.dropped");
    }

    public void RecordSessionStarted() => _sessionStartedCounter.Add(1);

    public void RecordSessionClosed() => _sessionClosedCounter.Add(1);

    public void RecordSessionFailed() => _sessionFailedCounter.Add(1);

    public void RecordSessionRejected() => _sessionRejectedCounter.Add(1);

    public void RecordInputAccepted(long chunks) => _inputAcceptedCounter.Add(chunks);

    public void RecordInputRejected(long chunks) => _inputRejectedCounter.Add(chunks);

    public void RecordWsEventDropped(long events) => _wsEventDroppedCounter.Add(events);
}

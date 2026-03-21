namespace AureTTY.Contracts.Configuration;

public sealed record TerminalRuntimeLimits(
    int MaxConcurrentSessions,
    int MaxSessionsPerViewer,
    int ReplayBufferCapacity,
    int MaxPendingInputChunks,
    int SessionIdleTimeoutSeconds = 900,
    int SessionHardLifetimeSeconds = 14400)
{
    public const int DefaultMaxConcurrentSessions = 32;
    public const int DefaultMaxSessionsPerViewer = 8;
    public const int DefaultReplayBufferCapacity = 4096;
    public const int DefaultMaxPendingInputChunks = 8192;
    public const int DefaultSessionIdleTimeoutSeconds = 900;
    public const int DefaultSessionHardLifetimeSeconds = 14400;

    public static TerminalRuntimeLimits Default { get; } = new(
        DefaultMaxConcurrentSessions,
        DefaultMaxSessionsPerViewer,
        DefaultReplayBufferCapacity,
        DefaultMaxPendingInputChunks);

    public TerminalRuntimeLimits Validate()
    {
        if (MaxConcurrentSessions <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxConcurrentSessions),
                MaxConcurrentSessions,
                "MaxConcurrentSessions must be greater than zero.");
        }

        if (MaxSessionsPerViewer <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxSessionsPerViewer),
                MaxSessionsPerViewer,
                "MaxSessionsPerViewer must be greater than zero.");
        }

        if (MaxSessionsPerViewer > MaxConcurrentSessions)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxSessionsPerViewer),
                MaxSessionsPerViewer,
                "MaxSessionsPerViewer cannot exceed MaxConcurrentSessions.");
        }

        if (ReplayBufferCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ReplayBufferCapacity),
                ReplayBufferCapacity,
                "ReplayBufferCapacity must be greater than zero.");
        }

        if (MaxPendingInputChunks <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxPendingInputChunks),
                MaxPendingInputChunks,
                "MaxPendingInputChunks must be greater than zero.");
        }

        if (SessionIdleTimeoutSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(SessionIdleTimeoutSeconds),
                SessionIdleTimeoutSeconds,
                "SessionIdleTimeoutSeconds must be greater than zero.");
        }

        if (SessionHardLifetimeSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(SessionHardLifetimeSeconds),
                SessionHardLifetimeSeconds,
                "SessionHardLifetimeSeconds must be greater than zero.");
        }

        if (SessionIdleTimeoutSeconds > SessionHardLifetimeSeconds)
        {
            throw new ArgumentOutOfRangeException(
                nameof(SessionIdleTimeoutSeconds),
                SessionIdleTimeoutSeconds,
                "SessionIdleTimeoutSeconds cannot exceed SessionHardLifetimeSeconds.");
        }

        return this;
    }
}

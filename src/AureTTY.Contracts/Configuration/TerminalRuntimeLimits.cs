namespace AureTTY.Contracts.Configuration;

public sealed record TerminalRuntimeLimits(
    int MaxConcurrentSessions,
    int MaxSessionsPerViewer,
    int ReplayBufferCapacity,
    int MaxPendingInputChunks)
{
    public const int DefaultMaxConcurrentSessions = 32;
    public const int DefaultMaxSessionsPerViewer = 8;
    public const int DefaultReplayBufferCapacity = 4096;
    public const int DefaultMaxPendingInputChunks = 8192;

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

        return this;
    }
}

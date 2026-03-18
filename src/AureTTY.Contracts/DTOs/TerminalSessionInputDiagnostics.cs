// Copyright © 2023-2026 Vitaly Kuzyaev. All rights reserved.
// This file is part of the AureTTY project.
// Licensed under the GNU Affero General Public License v3.0.

using AureTTY.Contracts.Enums;

namespace AureTTY.Contracts.DTOs;

[MessagePackObject]
public sealed class TerminalSessionInputDiagnostics(string sessionId)
{
    [Key(0)]
    public string SessionId { get; } = sessionId;

    [Key(1)]
    public TerminalSessionState State { get; set; } = TerminalSessionState.Starting;

    [Key(2)]
    public string? ViewerId { get; set; }

    [Key(3)]
    public long NextExpectedSequence { get; set; } = 1;

    [Key(4)]
    public long LastAcceptedSequence { get; set; }

    [Key(5)]
    public long[] PendingSequences { get; set; } = [];

    [Key(6)]
    public TerminalSessionInputChunkDiagnostics[] RecentChunks { get; set; } = [];

    [Key(7)]
    public DateTimeOffset GeneratedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    [Key(8)]
    public string? Error { get; set; }
}

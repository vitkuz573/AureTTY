// Copyright © 2023-2026 Vitaly Kuzyaev. All rights reserved.
// This file is part of the AureTTY project.
// Licensed under the GNU Affero General Public License v3.0.

using AureTTY.Contracts.Enums;

namespace AureTTY.Contracts.DTOs;

[MessagePackObject]
public sealed class TerminalSessionEvent(string sessionId, TerminalSessionEventType eventType)
{
    [Key(0)]
    public string SessionId { get; } = sessionId;

    [Key(1)]
    public TerminalSessionEventType EventType { get; } = eventType;

    [Key(2)]
    public TerminalSessionState State { get; set; } = TerminalSessionState.Starting;

    [Key(3)]
    public long? SequenceNumber { get; init; }

    [Key(4)]
    public string? Text { get; init; }

    [Key(5)]
    public bool IsStdErr { get; init; }

    [Key(6)]
    public long TimestampUnixMilliseconds { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    [Key(7)]
    public int? ProcessId { get; init; }

    [Key(8)]
    public int? ExitCode { get; init; }

    [Key(9)]
    public string? Error { get; init; }
}

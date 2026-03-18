// Copyright © 2023-2026 Vitaly Kuzyaev. All rights reserved.
// This file is part of the AureTTY project.
// Licensed under the GNU Affero General Public License v3.0.

using AureTTY.Contracts.Enums;

namespace AureTTY.Contracts.DTOs;

[MessagePackObject]
public sealed class TerminalSessionHandle(string sessionId)
{
    [Key(0)]
    public string SessionId { get; } = sessionId;

    [Key(1)]
    public TerminalSessionState State { get; set; } = TerminalSessionState.Starting;

    [Key(2)]
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    [Key(3)]
    public int? ProcessId { get; init; }

    [Key(4)]
    public string? Error { get; init; }
}

// Copyright © 2023-2026 Vitaly Kuzyaev. All rights reserved.
// This file is part of the AureTTY project.
// Licensed under the GNU Affero General Public License v3.0.

namespace AureTTY.Contracts.DTOs;

[MessagePackObject]
public sealed class TerminalSessionInputRequest(string sessionId, string text, long sequence)
{
    [Key(0)]
    public string SessionId { get; } = sessionId;

    [Key(1)]
    public string Text { get; } = text;

    [Key(2)]
    public long Sequence { get; } = sequence;
}

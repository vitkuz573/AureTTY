// Copyright © 2023-2026 Vitaly Kuzyaev. All rights reserved.
// This file is part of the AureTTY project.
// Licensed under the GNU Affero General Public License v3.0.

namespace AureTTY.Contracts.DTOs;

[MessagePackObject]
public sealed class TerminalSessionResizeRequest(string sessionId, int columns, int rows)
{
    [Key(0)]
    public string SessionId { get; } = sessionId;

    [Key(1)]
    public int Columns { get; } = columns;

    [Key(2)]
    public int Rows { get; } = rows;
}

// Copyright © 2023-2026 Vitaly Kuzyaev. All rights reserved.
// This file is part of the AureTTY project.
// Licensed under the GNU Affero General Public License v3.0.

using System.Text.Json;

namespace AureTTY.Protocol;

public sealed class TerminalIpcMessage
{
    public required string Type { get; init; }

    public string? Id { get; init; }

    public string? Method { get; init; }

    public JsonElement? Payload { get; init; }

    public string? Error { get; init; }
}

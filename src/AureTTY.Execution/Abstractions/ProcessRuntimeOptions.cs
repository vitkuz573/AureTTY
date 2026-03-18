// Copyright © 2023-2026 Vitaly Kuzyaev. All rights reserved.
// This file is part of the AureTTY project.
// Licensed under the GNU Affero General Public License v3.0.

namespace AureTTY.Execution.Abstractions;

public sealed class ProcessRuntimeOptions
{
    public bool UsePseudoTerminal { get; init; }

    public bool RequirePseudoTerminal { get; init; }

    public int? Columns { get; init; }

    public int? Rows { get; init; }
}

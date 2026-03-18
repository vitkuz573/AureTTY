// Copyright © 2023-2026 Vitaly Kuzyaev. All rights reserved.
// This file is part of the AureTTY project.
// Licensed under the GNU Affero General Public License v3.0.

using AureTTY.Execution.Abstractions;

namespace AureTTY.Windows.Models;

public struct NativeProcessOptions() : INativeProcessOptions
{
    public int? SessionId { get; set; } = null;

    public bool ForceConsoleSession { get; set; } = true;

    public string DesktopName { get; set; } = "Default";

    public bool UseCurrentUserToken { get; set; } = false;

    public string? UserName { get; set; } = null;

    public string? Domain { get; set; } = null;

    public string? Password { get; set; } = null;

    public bool LoadUserProfile { get; set; } = true;

    public bool UsePseudoTerminal { get; set; } = false;

    public bool RequirePseudoTerminal { get; set; } = false;

    public int? PseudoTerminalColumns { get; set; } = null;

    public int? PseudoTerminalRows { get; set; } = null;
}

// Copyright © 2023-2026 Vitaly Kuzyaev. All rights reserved.
// This file is part of the AureTTY project.
// Licensed under the GNU Affero General Public License v3.0.

using AureTTY.Execution.Abstractions;
using AureTTY.Contracts.Enums;
using AureTTY.Windows.Models;

namespace AureTTY.Windows.Services;

public class NativeProcessOptionsProvider : INativeProcessOptionsProvider
{
    public INativeProcessOptions Create(
        ExecutionRunContext runContext,
        ProcessCredentialOptions? credentials = null,
        ProcessRuntimeOptions? runtimeOptions = null)
    {
        var hasExplicitCredentials = !string.IsNullOrWhiteSpace(credentials?.UserName);

        return new NativeProcessOptions
        {
            ForceConsoleSession = true,
            DesktopName = "Default",
            UseCurrentUserToken = !hasExplicitCredentials && runContext == ExecutionRunContext.InteractiveUser,
            UserName = hasExplicitCredentials ? credentials!.UserName.Trim() : null,
            Domain = hasExplicitCredentials ? credentials!.Domain?.Trim() : null,
            Password = hasExplicitCredentials ? credentials!.Password : null,
            LoadUserProfile = credentials?.LoadUserProfile ?? true,
            UsePseudoTerminal = runtimeOptions?.UsePseudoTerminal ?? false,
            RequirePseudoTerminal = runtimeOptions?.RequirePseudoTerminal ?? false,
            PseudoTerminalColumns = runtimeOptions?.Columns,
            PseudoTerminalRows = runtimeOptions?.Rows
        };
    }
}

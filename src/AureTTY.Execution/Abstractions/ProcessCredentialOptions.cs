// Copyright © 2023-2026 Vitaly Kuzyaev. All rights reserved.
// This file is part of the AureTTY project.
// Licensed under the GNU Affero General Public License v3.0.

namespace AureTTY.Execution.Abstractions;

public sealed class ProcessCredentialOptions(string userName, string? password = null)
{
    public string UserName { get; } = userName;

    public string? Domain { get; init; }

    public string? Password { get; } = password;

    public bool LoadUserProfile { get; init; } = true;
}

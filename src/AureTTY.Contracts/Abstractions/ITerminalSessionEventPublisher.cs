// Copyright © 2023-2026 Vitaly Kuzyaev. All rights reserved.
// This file is part of the AureTTY project.
// Licensed under the GNU Affero General Public License v3.0.

using AureTTY.Contracts.DTOs;

namespace AureTTY.Contracts.Abstractions;

public interface ITerminalSessionEventPublisher
{
    Task SendTerminalSessionEventAsync(string viewerId, TerminalSessionEvent terminalSessionEvent);
}

// Copyright © 2023-2026 Vitaly Kuzyaev. All rights reserved.
// This file is part of the AureTTY project.
// Licensed under the GNU Affero General Public License v3.0.

using AureTTY.Contracts.Abstractions;
using AureTTY.Contracts.DTOs;
using AureTTY.Protocol;

namespace AureTTY.Services;

public sealed class PipeTerminalSessionEventPublisher : ITerminalSessionEventPublisher
{
    private readonly Lock _sync = new();
    private Func<TerminalIpcSessionEvent, CancellationToken, Task>? _sink;

    public void SetSink(Func<TerminalIpcSessionEvent, CancellationToken, Task>? sink)
    {
        lock (_sync)
        {
            _sink = sink;
        }
    }

    public Task SendTerminalSessionEventAsync(string viewerId, TerminalSessionEvent terminalSessionEvent)
    {
        Func<TerminalIpcSessionEvent, CancellationToken, Task>? sink;
        lock (_sync)
        {
            sink = _sink;
        }

        if (sink is null)
        {
            return Task.CompletedTask;
        }

        return sink(new TerminalIpcSessionEvent(viewerId, terminalSessionEvent), CancellationToken.None);
    }
}

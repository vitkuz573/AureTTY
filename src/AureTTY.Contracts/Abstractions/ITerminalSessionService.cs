// Copyright © 2023-2026 Vitaly Kuzyaev. All rights reserved.
// This file is part of the AureTTY project.
// Licensed under the GNU Affero General Public License v3.0.

using AureTTY.Contracts.DTOs;
using AureTTY.Contracts.Enums;

namespace AureTTY.Contracts.Abstractions;

public interface ITerminalSessionService
{
    Task<TerminalSessionHandle> StartAsync(string viewerId, TerminalSessionStartRequest request, CancellationToken cancellationToken = default);

    Task<TerminalSessionHandle> ResumeAsync(string viewerId, TerminalSessionResumeRequest request, CancellationToken cancellationToken = default);

    Task SendInputAsync(string viewerId, TerminalSessionInputRequest request, CancellationToken cancellationToken = default);

    Task<TerminalSessionInputDiagnostics> GetInputDiagnosticsAsync(string viewerId, string sessionId, CancellationToken cancellationToken = default);

    Task ResizeAsync(string viewerId, TerminalSessionResizeRequest request, CancellationToken cancellationToken = default);

    Task SignalAsync(string viewerId, string sessionId, TerminalSessionSignal signal, CancellationToken cancellationToken = default);

    Task CloseAsync(string viewerId, string sessionId, CancellationToken cancellationToken = default);

    Task CloseViewerSessionsAsync(string viewerId, CancellationToken cancellationToken = default);

    Task CloseAllSessionsAsync(CancellationToken cancellationToken = default);
}

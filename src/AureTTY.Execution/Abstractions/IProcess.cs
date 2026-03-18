// Copyright © 2023-2026 Vitaly Kuzyaev. All rights reserved.
// This file is part of the AureTTY project.
// Licensed under the GNU Affero General Public License v3.0.

using System.Diagnostics;

namespace AureTTY.Execution.Abstractions;

public interface IProcess : IDisposable
{
    int Id { get; }

    int ExitCode { get; }

    int SessionId { get; }

    StreamWriter StandardInput { get; }

    StreamReader StandardOutput { get; }

    StreamReader StandardError { get; }

    ProcessModule? MainModule { get; }

    string ProcessName { get; }

    long WorkingSet64 { get; }

    DateTime StartTime { get; }

    bool HasExited { get; }

    Task StartAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken = default);

    void Kill(bool entireProcessTree = false);

    Task<string[]> GetCommandLineAsync();

    bool WaitForExit(uint millisecondsTimeout = uint.MaxValue);

    Task WaitForExitAsync(CancellationToken cancellationToken = default);
}

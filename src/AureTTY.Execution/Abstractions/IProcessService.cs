// Copyright © 2023-2026 Vitaly Kuzyaev. All rights reserved.
// This file is part of the AureTTY project.
// Licensed under the GNU Affero General Public License v3.0.

namespace AureTTY.Execution.Abstractions;

public interface IProcessService
{
    IProcess[] GetProcesses();

    IProcess GetCurrentProcess();

    IProcess GetProcessById(int processId);

    IProcess[] GetProcessesByName(string processName);
}
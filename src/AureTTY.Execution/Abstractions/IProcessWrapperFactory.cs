// Copyright © 2023-2026 Vitaly Kuzyaev. All rights reserved.
// This file is part of the AureTTY project.
// Licensed under the GNU Affero General Public License v3.0.

using System.Diagnostics;

namespace AureTTY.Execution.Abstractions;

public interface IProcessWrapperFactory
{
    IProcess Create();

    IProcess Create(Process process);
}
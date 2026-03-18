// Copyright © 2023-2026 Vitaly Kuzyaev. All rights reserved.
// This file is part of the AureTTY project.
// Licensed under the GNU Affero General Public License v3.0.

using AureTTY.Contracts.Enums;

namespace AureTTY.Execution.Abstractions;

public interface IScriptProcessFactory
{
    IProcess Create(
        ExecutionRunContext runContext,
        ProcessCredentialOptions? credentials = null,
        ProcessRuntimeOptions? runtimeOptions = null);
}

// Copyright © 2023-2026 Vitaly Kuzyaev. All rights reserved.
// This file is part of the AureTTY project.
// Licensed under the GNU Affero General Public License v3.0.

namespace AureTTY.Services;

public sealed record TerminalServiceOptions(string PipeName, string PipeToken)
{
    public static bool TryParse(string[] args, out TerminalServiceOptions? options, out string? error)
    {
        ArgumentNullException.ThrowIfNull(args);

        options = null;
        error = null;

        string? pipeName = null;
        string? pipeToken = null;

        for (var i = 0; i < args.Length; i++)
        {
            var current = args[i];
            if (string.Equals(current, "--pipe-name", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                pipeName = args[++i]?.Trim();
                continue;
            }

            if (string.Equals(current, "--pipe-token", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                pipeToken = args[++i]?.Trim();
                continue;
            }
        }

        pipeName = string.IsNullOrWhiteSpace(pipeName) ? AureTTY.Protocol.TerminalIpcDefaults.PipeName : pipeName;
        pipeToken = string.IsNullOrWhiteSpace(pipeToken) ? AureTTY.Protocol.TerminalIpcDefaults.PipeToken : pipeToken;

        options = new TerminalServiceOptions(pipeName, pipeToken);
        return true;
    }
}

using AureTTY.Contracts.Enums;
using AureTTY.Execution.Abstractions;
using AureTTY.Linux.Models;

namespace AureTTY.Linux.Services;

public sealed class NativeProcessOptionsProvider : INativeProcessOptionsProvider
{
    public INativeProcessOptions Create(
        ExecutionRunContext runContext,
        ProcessCredentialOptions? credentials = null,
        ProcessRuntimeOptions? runtimeOptions = null)
    {
        var hasExplicitCredentials = !string.IsNullOrWhiteSpace(credentials?.UserName);

        return new NativeProcessOptions
        {
            RunContext = runContext,
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

using AureTTY.Contracts.Enums;

namespace AureTTY.Execution.Abstractions;

public interface INativeProcessOptionsProvider
{
    INativeProcessOptions Create(
        ExecutionRunContext runContext,
        ProcessCredentialOptions? credentials = null,
        ProcessRuntimeOptions? runtimeOptions = null);
}

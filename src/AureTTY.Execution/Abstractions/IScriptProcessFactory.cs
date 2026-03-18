using AureTTY.Contracts.Enums;

namespace AureTTY.Execution.Abstractions;

public interface IScriptProcessFactory
{
    IProcess Create(
        ExecutionRunContext runContext,
        ProcessCredentialOptions? credentials = null,
        ProcessRuntimeOptions? runtimeOptions = null);
}

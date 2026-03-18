using AureTTY.Execution.Abstractions;
using AureTTY.Contracts.Enums;

namespace AureTTY.Windows.Services;

public sealed class ScriptProcessFactory(
    INativeProcessFactory nativeProcessFactory,
    INativeProcessOptionsProvider nativeProcessOptionsProvider) : IScriptProcessFactory
{
    private readonly INativeProcessFactory _nativeProcessFactory = nativeProcessFactory ?? throw new ArgumentNullException(nameof(nativeProcessFactory));
    private readonly INativeProcessOptionsProvider _nativeProcessOptionsProvider = nativeProcessOptionsProvider ?? throw new ArgumentNullException(nameof(nativeProcessOptionsProvider));

    public IProcess Create(
        ExecutionRunContext runContext,
        ProcessCredentialOptions? credentials = null,
        ProcessRuntimeOptions? runtimeOptions = null)
    {
        var options = _nativeProcessOptionsProvider.Create(runContext, credentials, runtimeOptions);
        return _nativeProcessFactory.Create(options);
    }
}

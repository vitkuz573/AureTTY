using System.Diagnostics;
using System.Text;
using AureTTY.Execution.Abstractions;
using AureTTY.Linux.Models;

namespace AureTTY.Linux.Services;

public sealed class NativeProcess : IProcess, IResizableTerminalProcess
{
    private readonly NativeProcessOptions _options;
    private readonly IProcess _process;
    private bool _usesPseudoTerminal;

    public NativeProcess(INativeProcessOptions options, IProcessWrapperFactory processWrapperFactory)
    {
        if (options is not NativeProcessOptions nativeOptions)
        {
            throw new ArgumentException("Invalid options type. Expected NativeProcessOptions.", nameof(options));
        }

        ArgumentNullException.ThrowIfNull(processWrapperFactory);

        _options = nativeOptions;
        _process = processWrapperFactory.Create();
    }

    public int Id => _process.Id;

    public int ExitCode => _process.ExitCode;

    public int SessionId => _process.SessionId;

    public StreamWriter StandardInput => _process.StandardInput;

    public StreamReader StandardOutput => _process.StandardOutput;

    public StreamReader StandardError => _process.StandardError;

    public ProcessModule? MainModule => _process.MainModule;

    public string ProcessName => _process.ProcessName;

    public long WorkingSet64 => _process.WorkingSet64;

    public DateTime StartTime => _process.StartTime;

    public bool HasExited => _process.HasExited;

    public async Task StartAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        cancellationToken.ThrowIfCancellationRequested();

        if (HasExplicitCredentials(_options))
        {
            throw new PlatformNotSupportedException(
                "Explicit user credentials are not implemented for Linux yet. Use host process context.");
        }

        var effectiveStartInfo = BuildEffectiveStartInfo(startInfo, _options, out var usesPseudoTerminal);
        _usesPseudoTerminal = usesPseudoTerminal;

        await _process.StartAsync(effectiveStartInfo, cancellationToken);

        if (_usesPseudoTerminal &&
            _options.PseudoTerminalColumns is int columns &&
            _options.PseudoTerminalRows is int rows)
        {
            await ResizeTerminalAsync(columns, rows, cancellationToken);
        }
    }

    public void Kill(bool entireProcessTree = false)
    {
        _process.Kill(entireProcessTree);
    }

    public Task<string[]> GetCommandLineAsync()
    {
        return _process.GetCommandLineAsync();
    }

    public bool WaitForExit(uint millisecondsTimeout = uint.MaxValue)
    {
        return _process.WaitForExit(millisecondsTimeout);
    }

    public Task WaitForExitAsync(CancellationToken cancellationToken = default)
    {
        return _process.WaitForExitAsync(cancellationToken);
    }

    public async Task ResizeTerminalAsync(int columns, int rows, CancellationToken cancellationToken = default)
    {
        if (columns <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(columns), columns, "Columns must be greater than zero.");
        }

        if (rows <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rows), rows, "Rows must be greater than zero.");
        }

        if (!_usesPseudoTerminal)
        {
            return;
        }

        var command = $"stty cols {columns} rows {rows}\n";
        await _process.StandardInput.WriteAsync(command.AsMemory(), cancellationToken);
        await _process.StandardInput.FlushAsync(cancellationToken);
    }

    public void Dispose()
    {
        _process.Dispose();
    }

    private static ProcessStartInfo BuildEffectiveStartInfo(
        ProcessStartInfo requestedStartInfo,
        NativeProcessOptions options,
        out bool usesPseudoTerminal)
    {
        usesPseudoTerminal = false;
        if (!options.UsePseudoTerminal)
        {
            return CloneStartInfo(requestedStartInfo);
        }

        if (!TryResolveScriptBinary(out var scriptPath))
        {
            if (options.RequirePseudoTerminal)
            {
                throw new InvalidOperationException(
                    "Pseudo-terminal is required but 'script' binary was not found on this Linux host.");
            }

            return CloneStartInfo(requestedStartInfo);
        }

        var command = BuildCommandLine(requestedStartInfo);
        var wrappedStartInfo = new ProcessStartInfo
        {
            FileName = scriptPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = requestedStartInfo.StandardInputEncoding,
            StandardOutputEncoding = requestedStartInfo.StandardOutputEncoding,
            StandardErrorEncoding = requestedStartInfo.StandardErrorEncoding
        };

        if (!string.IsNullOrWhiteSpace(requestedStartInfo.WorkingDirectory))
        {
            wrappedStartInfo.WorkingDirectory = requestedStartInfo.WorkingDirectory;
        }

        foreach (var environment in requestedStartInfo.Environment)
        {
            wrappedStartInfo.Environment[environment.Key] = environment.Value;
        }

        wrappedStartInfo.ArgumentList.Add("--quiet");
        wrappedStartInfo.ArgumentList.Add("--flush");
        wrappedStartInfo.ArgumentList.Add("--return");
        wrappedStartInfo.ArgumentList.Add("--command");
        wrappedStartInfo.ArgumentList.Add(command);
        wrappedStartInfo.ArgumentList.Add("/dev/null");

        usesPseudoTerminal = true;
        return wrappedStartInfo;
    }

    private static ProcessStartInfo CloneStartInfo(ProcessStartInfo source)
    {
        var clone = new ProcessStartInfo
        {
            FileName = source.FileName,
            Arguments = source.Arguments,
            WorkingDirectory = source.WorkingDirectory,
            UseShellExecute = source.UseShellExecute,
            CreateNoWindow = source.CreateNoWindow,
            RedirectStandardInput = source.RedirectStandardInput,
            RedirectStandardOutput = source.RedirectStandardOutput,
            RedirectStandardError = source.RedirectStandardError,
            StandardInputEncoding = source.StandardInputEncoding,
            StandardOutputEncoding = source.StandardOutputEncoding,
            StandardErrorEncoding = source.StandardErrorEncoding
        };

        foreach (var argument in source.ArgumentList)
        {
            clone.ArgumentList.Add(argument);
        }

        foreach (var environment in source.Environment)
        {
            clone.Environment[environment.Key] = environment.Value;
        }

        return clone;
    }

    private static string BuildCommandLine(ProcessStartInfo startInfo)
    {
        if (string.IsNullOrWhiteSpace(startInfo.FileName))
        {
            throw new InvalidOperationException("StartInfo.FileName is required.");
        }

        var builder = new StringBuilder();
        builder.Append(EscapePosixToken(startInfo.FileName));

        if (startInfo.ArgumentList.Count > 0)
        {
            foreach (var argument in startInfo.ArgumentList)
            {
                builder.Append(' ');
                builder.Append(EscapePosixToken(argument));
            }

            return builder.ToString();
        }

        if (!string.IsNullOrWhiteSpace(startInfo.Arguments))
        {
            builder.Append(' ');
            builder.Append(startInfo.Arguments);
        }

        return builder.ToString();
    }

    private static string EscapePosixToken(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return "'" + value.Replace("'", "'\"'\"'", StringComparison.Ordinal) + "'";
    }

    private static bool TryResolveScriptBinary(out string scriptPath)
    {
        if (File.Exists("/usr/bin/script"))
        {
            scriptPath = "/usr/bin/script";
            return true;
        }

        if (File.Exists("/bin/script"))
        {
            scriptPath = "/bin/script";
            return true;
        }

        var pathValue = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(pathValue))
        {
            var candidates = pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var candidate in candidates)
            {
                var resolved = Path.Combine(candidate, "script");
                if (File.Exists(resolved))
                {
                    scriptPath = resolved;
                    return true;
                }
            }
        }

        scriptPath = string.Empty;
        return false;
    }

    private static bool HasExplicitCredentials(NativeProcessOptions options)
    {
        return !string.IsNullOrWhiteSpace(options.UserName);
    }
}

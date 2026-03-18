using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using AureTTY.Contracts.Abstractions;
using AureTTY.Contracts.DTOs;
using AureTTY.Contracts.Enums;
using AureTTY.Contracts.Exceptions;
using AureTTY.Execution.Abstractions;
using AureTTY.Execution.Services;
using Microsoft.Extensions.Logging;

namespace AureTTY.Core.Services;

public sealed class TerminalSessionService(
    IScriptProcessFactory processFactory,
    ITerminalSessionEventPublisher eventPublisher,
    ILogger<TerminalSessionService> logger) : ITerminalSessionService
{
    private const int MaxConcurrentSessions = 8;
    private const int OutputReadBufferSize = 1024;
    private const int ReplayBufferCapacity = 2048;
    private const int ActiveSessionSnapshotLimit = 8;
    private static readonly TimeSpan OutputPumpDrainTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan OutputPumpDrainTimeoutOnClose = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan OutputPumpForcedCloseTimeout = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan ProcessExitAfterCloseTimeout = TimeSpan.FromSeconds(2);
    private const int MaxPendingInputChunks = 8192;
    private const int InputDiagnosticsCapacity = 256;
    private static readonly bool EnableInputHexLogging = IsInputHexLoggingEnabled();
    private const int InputLogPreviewCharLimit = 96;
    private const int InputLogPreviewByteLimit = 128;

    private readonly IScriptProcessFactory _processFactory = processFactory ?? throw new ArgumentNullException(nameof(processFactory));
    private readonly ITerminalSessionEventPublisher _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
    private readonly ILogger<TerminalSessionService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly ConcurrentDictionary<string, TerminalSessionInstance> _sessions = new(StringComparer.OrdinalIgnoreCase);

    public Task<IReadOnlyCollection<TerminalSessionHandle>> GetAllSessionsAsync(CancellationToken cancellationToken = default)
    {
        var handles = _sessions.Values
            .Select(session => session.CreateHandleSnapshot())
            .OrderBy(static session => session.CreatedAtUtc)
            .ToArray();
        return Task.FromResult<IReadOnlyCollection<TerminalSessionHandle>>(handles);
    }

    public Task<IReadOnlyCollection<TerminalSessionHandle>> GetViewerSessionsAsync(string viewerId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(viewerId);

        var handles = _sessions.Values
            .Where(session => session.IsViewer(viewerId))
            .Select(session => session.CreateHandleSnapshot())
            .OrderBy(static session => session.CreatedAtUtc)
            .ToArray();
        return Task.FromResult<IReadOnlyCollection<TerminalSessionHandle>>(handles);
    }

    public Task<TerminalSessionHandle> GetSessionAsync(string viewerId, string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(viewerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        var session = ResolveSession(viewerId, sessionId);
        return Task.FromResult(session.CreateHandleSnapshot());
    }

    public Task<TerminalSessionHandle> StartAsync(string viewerId, TerminalSessionStartRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(viewerId);
        ArgumentNullException.ThrowIfNull(request);

        var activeSessionCount = GetActiveSessionCount();
        if (activeSessionCount >= MaxConcurrentSessions)
        {
            var snapshot = BuildActiveSessionSnapshot();
            _logger.LogWarning(
                "Terminal session start rejected due to session limit. ViewerId={ViewerId} ActiveSessions={ActiveSessions} MaxSessions={MaxSessions} Snapshot={Snapshot}.",
                viewerId,
                activeSessionCount,
                MaxConcurrentSessions,
                snapshot);

            throw new TerminalSessionConflictException($"Maximum number of concurrent terminal sessions ({MaxConcurrentSessions}) has been reached.");
        }

        var sessionId = request.SessionId?.Trim();
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new TerminalSessionValidationException("SessionId is required.");
        }

        var normalizedRequest = new TerminalSessionStartRequest(sessionId, request.Shell)
        {
            RunContext = request.RunContext,
            UserName = NormalizeNullable(request.UserName),
            Domain = NormalizeNullable(request.Domain),
            Password = request.Password,
            LoadUserProfile = request.LoadUserProfile,
            WorkingDirectory = NormalizeNullable(request.WorkingDirectory),
            Columns = NormalizeDimension(request.Columns),
            Rows = NormalizeDimension(request.Rows)
        };

        var session = new TerminalSessionInstance(viewerId, normalizedRequest);
        if (!_sessions.TryAdd(session.SessionId, session))
        {
            throw new TerminalSessionConflictException($"Terminal session '{session.SessionId}' already exists.");
        }

        _ = RunSessionAsync(session, CancellationToken.None);

        return Task.FromResult(session.CreateHandleSnapshot());
    }

    public async Task<TerminalSessionHandle> ResumeAsync(string viewerId, TerminalSessionResumeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(viewerId);
        ArgumentNullException.ThrowIfNull(request);

        var sessionId = request.SessionId?.Trim();
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new TerminalSessionValidationException("SessionId is required.");
        }

        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new TerminalSessionNotFoundException($"Terminal session '{sessionId}' was not found.");
        }

        session.Reattach(viewerId);
        if (request.Columns is not null)
        {
            session.Columns = NormalizeDimension(request.Columns);
        }

        if (request.Rows is not null)
        {
            session.Rows = NormalizeDimension(request.Rows);
        }

        var process = session.GetProcess();
        if (process is IResizableTerminalProcess resizableProcess &&
            session.Columns is int columns &&
            session.Rows is int rows)
        {
            await resizableProcess.ResizeTerminalAsync(columns, rows, cancellationToken);
        }

        var replayFromSequence = request.LastReceivedSequenceNumber.GetValueOrDefault(0);
        if (replayFromSequence < 0)
        {
            replayFromSequence = 0;
        }

        var replayEntries = session.GetOutputReplay(replayFromSequence);
        foreach (var replayEntry in replayEntries)
        {
            await PublishEventAsync(
                session,
                TerminalSessionEventType.Output,
                session.State,
                text: replayEntry.Text,
                isStdErr: replayEntry.IsStdErr,
                processId: session.ProcessId,
                sequenceNumber: replayEntry.SequenceNumber);
        }

        return session.CreateHandleSnapshot();
    }

    public async Task SendInputAsync(string viewerId, TerminalSessionInputRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(viewerId);
        ArgumentNullException.ThrowIfNull(request);
        if (request.Sequence <= 0)
        {
            throw new TerminalSessionValidationException("Terminal input sequence must be a positive integer.");
        }

        var session = ResolveSession(viewerId, request.SessionId);
        var input = NormalizeInput(request.Text);
        if (input.Length == 0)
        {
            return;
        }

        await session.EnterInputDispatchAsync(cancellationToken);
        try
        {
            var orderedInputChunks = session.AcceptOrderedInput(viewerId, request.Sequence, input);
            if (orderedInputChunks.Count == 0)
            {
                return;
            }

            var process = session.GetProcess();
            if (process is null)
            {
                throw new TerminalSessionConflictException($"Terminal session '{session.SessionId}' is not running.");
            }

            foreach (var chunk in orderedInputChunks)
            {
                var encodedBytes = process.StandardInput.Encoding.GetBytes(chunk.Text);
                var codepointsPreview = FormatInputCodepointsPreview(chunk.Text);
                var bytesPreview = FormatInputBytesPreview(encodedBytes);

                if (EnableInputHexLogging)
                {
                    _logger.LogWarning(
                        "Terminal input forwarded to process stdin. SessionId={SessionId} ViewerId={ViewerId} Sequence={Sequence} CharCount={CharCount} Codepoints={Codepoints}.",
                        session.SessionId,
                        viewerId,
                        chunk.Sequence,
                        chunk.Text.Length,
                        codepointsPreview);

                    _logger.LogWarning(
                        "Terminal input bytes to process stdin. SessionId={SessionId} ViewerId={ViewerId} Sequence={Sequence} ByteCount={ByteCount} Bytes={Bytes}.",
                        session.SessionId,
                        viewerId,
                        chunk.Sequence,
                        encodedBytes.Length,
                        bytesPreview);
                }

                await process.StandardInput.WriteAsync(chunk.Text.AsMemory(), cancellationToken);
                session.RecordAcceptedInputChunk(new TerminalSessionInputChunkDiagnostics(
                    chunk.Sequence,
                    chunk.Text.Length,
                    encodedBytes.Length,
                    codepointsPreview,
                    bytesPreview,
                    DateTimeOffset.UtcNow));
            }

            await process.StandardInput.FlushAsync(cancellationToken);
        }
        finally
        {
            session.ExitInputDispatch();
        }
    }

    public Task<TerminalSessionInputDiagnostics> GetInputDiagnosticsAsync(string viewerId, string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(viewerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        var session = ResolveSession(viewerId, sessionId);
        var diagnostics = session.CreateInputDiagnosticsSnapshot();
        return Task.FromResult(diagnostics);
    }

    public async Task ResizeAsync(string viewerId, TerminalSessionResizeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(viewerId);
        ArgumentNullException.ThrowIfNull(request);

        var session = ResolveSession(viewerId, request.SessionId);
        session.Columns = NormalizeDimension(request.Columns);
        session.Rows = NormalizeDimension(request.Rows);

        var process = session.GetProcess();
        if (process is IResizableTerminalProcess resizableProcess &&
            session.Columns is int columns &&
            session.Rows is int rows)
        {
            await resizableProcess.ResizeTerminalAsync(columns, rows, cancellationToken);
        }
    }

    public Task SignalAsync(string viewerId, string sessionId, TerminalSessionSignal signal, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(viewerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        var session = ResolveSession(viewerId, sessionId);
        switch (signal)
        {
            case TerminalSessionSignal.Interrupt:
                {
                    var process = session.GetProcess();
                    if (process != null)
                    {
                        if (!TrySendNativeInterrupt(process.Id))
                        {
                            process.StandardInput.Write("\u0003");
                            process.StandardInput.Flush();
                        }
                    }

                    return Task.CompletedTask;
                }

            case TerminalSessionSignal.EndOfInput:
                {
                    var process = session.GetProcess();
                    process?.StandardInput.Close();
                    return Task.CompletedTask;
                }

            case TerminalSessionSignal.Terminate:
                {
                    session.RequestClose();
                    session.TryKillProcess();
                    return Task.CompletedTask;
                }

            default:
                throw new TerminalSessionValidationException($"Unsupported terminal signal '{signal}'.");
        }
    }

    public Task CloseAsync(string viewerId, string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(viewerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return Task.CompletedTask;
        }

        if (!session.IsViewer(viewerId))
        {
            throw new TerminalSessionForbiddenException("Terminal session belongs to another viewer.");
        }

        session.RequestClose();
        session.TryKillProcess();
        return Task.CompletedTask;
    }

    public Task CloseViewerSessionsAsync(string viewerId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(viewerId);
        var closedSessions = 0;

        foreach (var session in _sessions.Values.Where(static session => !session.IsCompleted))
        {
            if (!session.IsViewer(viewerId))
            {
                continue;
            }

            _logger.LogInformation(
                "Closing terminal session for disconnected viewer. SessionId={SessionId} ViewerId={ViewerId}.",
                session.SessionId,
                viewerId);

            session.RequestClose();
            session.TryKillProcess();
            closedSessions++;
        }

        if (closedSessions > 0)
        {
            _logger.LogInformation("Requested close for {ClosedSessions} terminal sessions for disconnected viewer {ViewerId}.", closedSessions, viewerId);
        }

        return Task.CompletedTask;
    }

    public Task CloseAllSessionsAsync(CancellationToken cancellationToken = default)
    {
        var closedSessions = 0;

        foreach (var session in _sessions.Values.Where(static session => !session.IsCompleted))
        {
            cancellationToken.ThrowIfCancellationRequested();

            session.RequestClose();
            session.TryKillProcess();
            closedSessions++;
        }

        if (closedSessions > 0)
        {
            _logger.LogWarning(
                "Requested close for {ClosedSessions} terminal sessions after host IPC disconnect.",
                closedSessions);
        }

        return Task.CompletedTask;
    }

    private async Task RunSessionAsync(TerminalSessionInstance session, CancellationToken cancellationToken)
    {
        try
        {
            session.State = TerminalSessionState.Starting;
            var runtimeOptions = new ProcessRuntimeOptions
            {
                UsePseudoTerminal = true,
                RequirePseudoTerminal = true,
                Columns = session.Columns,
                Rows = session.Rows
            };

            var hasExplicitCredentials = !string.IsNullOrWhiteSpace(session.Request.UserName);
            _logger.LogInformation(
                "Terminal process launch request. SessionId={SessionId} ViewerId={ViewerId} Shell={Shell} RunContext={RunContext} HasExplicitCredentials={HasExplicitCredentials} UserName={UserName} Domain={Domain}.",
                session.SessionId,
                session.GetViewerIdForLog(),
                session.Request.Shell,
                session.Request.RunContext,
                hasExplicitCredentials,
                hasExplicitCredentials ? session.Request.UserName : null,
                hasExplicitCredentials ? session.Request.Domain : null);

            using var process = _processFactory.Create(session.Request.RunContext, BuildCredentials(session.Request), runtimeOptions);
            session.SetProcess(process);

            var startInfo = BuildStartInfo(session.Request);
            await process.StartAsync(startInfo, cancellationToken);

            session.ProcessId = process.Id;
            session.State = TerminalSessionState.Running;

            _logger.LogInformation(
                "Terminal session started. SessionId={SessionId} ViewerId={ViewerId} Shell={Shell} RunContext={RunContext} ProcessId={ProcessId}.",
                session.SessionId,
                session.GetViewerIdForLog(),
                session.Request.Shell,
                session.Request.RunContext,
                session.ProcessId);

            await PublishEventAsync(session, TerminalSessionEventType.Started, TerminalSessionState.Running, processId: session.ProcessId);

            using var outputPumpCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(session.CancelSource.Token);
            var stdoutTask = PumpOutputAsync(session, process.StandardOutput, isStdErr: false, outputPumpCancellationSource.Token);
            var stderrTask = PumpOutputAsync(session, process.StandardError, isStdErr: true, outputPumpCancellationSource.Token);

            var exitCode = await WaitForProcessExitAsync(session, process);

            var drainTimeout = session.CloseRequested
                ? OutputPumpDrainTimeoutOnClose
                : OutputPumpDrainTimeout;
            await DrainOutputPumpsAsync(session, process, stdoutTask, stderrTask, outputPumpCancellationSource, drainTimeout);

            session.State = TerminalSessionState.Closed;
            session.CompletedAtUtc = DateTimeOffset.UtcNow;

            await PublishEventAsync(
                session,
                TerminalSessionEventType.Closed,
                TerminalSessionState.Closed,
                processId: session.ProcessId,
                exitCode: exitCode);
        }
        catch (Exception ex)
        {
            session.State = TerminalSessionState.Failed;
            session.CompletedAtUtc = DateTimeOffset.UtcNow;

            _logger.LogError(
                ex,
                "Terminal session failed. SessionId={SessionId} ViewerId={ViewerId}.",
                session.SessionId,
                session.GetViewerIdForLog());

            await PublishEventAsync(
                session,
                TerminalSessionEventType.Failed,
                TerminalSessionState.Failed,
                processId: session.ProcessId,
                error: ex.Message);
        }
        finally
        {
            session.SetProcess(null);
            session.CancelSource.Dispose();
            _sessions.TryRemove(session.SessionId, out _);
        }
    }

    private async Task PumpOutputAsync(
        TerminalSessionInstance session,
        TextReader reader,
        bool isStdErr,
        CancellationToken cancellationToken)
    {
        var buffer = new char[OutputReadBufferSize];

        while (!session.CloseRequested)
        {
            int read;
            try
            {
                read = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            }
            catch (OperationCanceledException) when (session.CloseRequested)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (IOException)
            {
                break;
            }
            catch (InvalidOperationException)
            {
                break;
            }

            if (read == 0)
            {
                break;
            }

            var text = new string(buffer, 0, read);
            if (text.Length == 0)
            {
                continue;
            }

            await PublishEventAsync(
                session,
                TerminalSessionEventType.Output,
                TerminalSessionState.Running,
                text: text,
                isStdErr: isStdErr,
                processId: session.ProcessId);
        }
    }

    private async Task DrainOutputPumpsAsync(
        TerminalSessionInstance session,
        IProcess process,
        Task stdoutTask,
        Task stderrTask,
        CancellationTokenSource outputPumpCancellationSource,
        TimeSpan drainTimeout)
    {
        var outputPumpTask = Task.WhenAll(stdoutTask, stderrTask);
        try
        {
            await outputPumpTask.WaitAsync(drainTimeout);
            return;
        }
        catch (TimeoutException)
        {
            _logger.LogWarning(
                "Terminal output pump drain timed out. SessionId={SessionId} ProcessId={ProcessId} TimeoutMs={TimeoutMs} CloseRequested={CloseRequested}.",
                session.SessionId,
                session.ProcessId,
                (int)drainTimeout.TotalMilliseconds,
                session.CloseRequested);
        }

        outputPumpCancellationSource.Cancel();
        TryCloseReader(process.StandardOutput);
        TryCloseReader(process.StandardError);

        try
        {
            await outputPumpTask.WaitAsync(OutputPumpForcedCloseTimeout);
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or ObjectDisposedException or InvalidOperationException)
        {
            _logger.LogDebug(
                ex,
                "Terminal output pump force-close did not complete cleanly. SessionId={SessionId} ProcessId={ProcessId}.",
                session.SessionId,
                session.ProcessId);
        }
    }

    private async Task<int?> WaitForProcessExitAsync(TerminalSessionInstance session, IProcess process)
    {
        var waitForExitTask = process.WaitForExitAsync(CancellationToken.None);

        try
        {
            await waitForExitTask.WaitAsync(session.CancelSource.Token);
            return TryGetExitCode(process);
        }
        catch (OperationCanceledException) when (session.CloseRequested)
        {
            session.TryKillProcess();

            try
            {
                await waitForExitTask.WaitAsync(ProcessExitAfterCloseTimeout);
            }
            catch (TimeoutException)
            {
                _logger.LogWarning(
                    "Terminal process did not exit in time after close request. SessionId={SessionId} ProcessId={ProcessId} TimeoutMs={TimeoutMs}.",
                    session.SessionId,
                    session.ProcessId,
                    (int)ProcessExitAfterCloseTimeout.TotalMilliseconds);
            }
            catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
            {
                _logger.LogDebug(
                    ex,
                    "Terminal process exit wait ended unexpectedly after close request. SessionId={SessionId} ProcessId={ProcessId}.",
                    session.SessionId,
                    session.ProcessId);
            }

            return TryGetExitCode(process);
        }
    }

    private async Task PublishEventAsync(
        TerminalSessionInstance session,
        TerminalSessionEventType eventType,
        TerminalSessionState state,
        string? text = null,
        bool isStdErr = false,
        int? processId = null,
        int? exitCode = null,
        string? error = null,
        long? sequenceNumber = null)
    {
        if (eventType == TerminalSessionEventType.Output && !string.IsNullOrEmpty(text))
        {
            sequenceNumber ??= session.AppendOutput(text, isStdErr);
        }

        var viewerId = session.GetViewerId();
        if (string.IsNullOrWhiteSpace(viewerId))
        {
            return;
        }

        try
        {
            await _eventPublisher.SendTerminalSessionEventAsync(viewerId, new TerminalSessionEvent(session.SessionId, eventType)
            {
                State = state,
                SequenceNumber = sequenceNumber,
                Text = text,
                IsStdErr = isStdErr,
                ProcessId = processId,
                ExitCode = exitCode,
                Error = error
            });
        }
        catch (Exception ex)
        {
            _logger.LogDebug(
                ex,
                "Failed to publish terminal session event. SessionId={SessionId} ViewerId={ViewerId} Event={Event}.",
                session.SessionId,
                viewerId,
                eventType);
        }
    }

    private static ProcessStartInfo BuildStartInfo(TerminalSessionStartRequest request)
    {
        return ShellLaunchPlanner.BuildInteractiveShellStartInfo(request.Shell, request.WorkingDirectory);
    }

    private static ProcessCredentialOptions? BuildCredentials(TerminalSessionStartRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserName))
        {
            return null;
        }

        return new ProcessCredentialOptions(request.UserName.Trim(), request.Password)
        {
            Domain = NormalizeNullable(request.Domain),
            LoadUserProfile = request.LoadUserProfile
        };
    }

    private static int? NormalizeDimension(int? value)
    {
        if (value is null)
        {
            return null;
        }

        return Math.Clamp(value.Value, 1, 2000);
    }

    private static string? NormalizeNullable(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static string NormalizeInput(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (OperatingSystem.IsWindows())
        {
            // Keep terminal control input as-is and normalize only Enter semantics.
            // Input key semantics (DEL/BS/ESC combos) belong to the client+shell contract.
            if (value.IndexOfAny(['\r', '\n']) < 0)
            {
                return value;
            }

            var builder = new StringBuilder(value.Length + 8);
            for (var index = 0; index < value.Length; index++)
            {
                var ch = value[index];
                if (ch == '\r')
                {
                    builder.Append('\r');
                    if (index + 1 < value.Length && value[index + 1] == '\n')
                    {
                        index++;
                    }

                    continue;
                }

                if (ch == '\n')
                {
                    builder.Append('\r');
                    continue;
                }

                builder.Append(ch);
            }

            return builder.ToString();
        }

        if (value.IndexOf('\r') < 0)
        {
            return value;
        }

        return value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
    }

    private bool TrySendNativeInterrupt(int processId)
    {
        if (processId <= 0 || OperatingSystem.IsWindows())
        {
            return false;
        }

        var killExecutable = ResolveKillExecutablePath();
        if (killExecutable is null)
        {
            return false;
        }

        return TrySendPosixSignal(killExecutable, processId, processGroup: true) ||
               TrySendPosixSignal(killExecutable, processId, processGroup: false);
    }

    private bool TrySendPosixSignal(string killExecutable, int processId, bool processGroup)
    {
        try
        {
            var target = processGroup ? $"-{processId}" : processId.ToString();
            var startInfo = new ProcessStartInfo
            {
                FileName = killExecutable,
                UseShellExecute = false,
                RedirectStandardInput = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("-INT");
            startInfo.ArgumentList.Add(target);

            using var signalProcess = Process.Start(startInfo);
            if (signalProcess is null)
            {
                return false;
            }

            if (!signalProcess.WaitForExit(1000))
            {
                try
                {
                    signalProcess.Kill(entireProcessTree: true);
                }
                catch
                {
                    // ignored
                }

                return false;
            }

            return signalProcess.ExitCode == 0;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to send POSIX terminal interrupt. ProcessId={ProcessId} ProcessGroup={ProcessGroup}.", processId, processGroup);
            return false;
        }
    }

    private static string? ResolveKillExecutablePath()
    {
        const string binKill = "/bin/kill";
        if (File.Exists(binKill))
        {
            return binKill;
        }

        const string usrBinKill = "/usr/bin/kill";
        return File.Exists(usrBinKill)
            ? usrBinKill
            : null;
    }

    private int GetActiveSessionCount()
    {
        return _sessions.Values.Count(static session => !session.IsCompleted);
    }

    private string BuildActiveSessionSnapshot()
    {
        var activeSessions = _sessions.Values
            .Where(static session => !session.IsCompleted)
            .OrderBy(static session => session.CreatedAtUtc)
            .ToArray();

        if (activeSessions.Length == 0)
        {
            return "none";
        }

        var now = DateTimeOffset.UtcNow;
        var head = activeSessions
            .Take(ActiveSessionSnapshotLimit)
            .Select(session =>
            {
                var ageSeconds = Math.Max(0, (int)(now - session.CreatedAtUtc).TotalSeconds);
                return $"{session.SessionId}:{session.State}:{(session.CloseRequested ? "closing" : "open")}:{ageSeconds}s";
            });

        var snapshot = string.Join(", ", head);
        if (activeSessions.Length > ActiveSessionSnapshotLimit)
        {
            snapshot += $", +{activeSessions.Length - ActiveSessionSnapshotLimit} more";
        }

        return snapshot;
    }

    private static void TryCloseReader(TextReader reader)
    {
        try
        {
            reader.Close();
        }
        catch
        {
            // ignored
        }
    }

    private static int? TryGetExitCode(IProcess process)
    {
        try
        {
            if (process.HasExited)
            {
                return process.ExitCode;
            }
        }
        catch
        {
            // ignored
        }

        return null;
    }

    private static bool IsInputHexLoggingEnabled()
    {
        var value = Environment.GetEnvironmentVariable("AURETTY_TERMINAL_LOG_INPUT_HEX");
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatInputCodepointsPreview(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "<empty>";
        }

        var previewLength = Math.Min(text.Length, InputLogPreviewCharLimit);
        var builder = new StringBuilder(previewLength * 8);
        for (var index = 0; index < previewLength; index++)
        {
            if (index > 0)
            {
                builder.Append(' ');
            }

            builder.Append("U+");
            builder.Append(((int)text[index]).ToString("X4", System.Globalization.CultureInfo.InvariantCulture));
        }

        if (text.Length > previewLength)
        {
            builder.Append(" ...");
        }

        return builder.ToString();
    }

    private static string FormatInputBytesPreview(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return "<empty>";
        }

        var previewLength = Math.Min(bytes.Length, InputLogPreviewByteLimit);
        var builder = new StringBuilder(previewLength * 3);
        for (var index = 0; index < previewLength; index++)
        {
            if (index > 0)
            {
                builder.Append(' ');
            }

            builder.Append(bytes[index].ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
        }

        if (bytes.Length > previewLength)
        {
            builder.Append(" ...");
        }

        return builder.ToString();
    }

    private TerminalSessionInstance ResolveSession(string viewerId, string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new TerminalSessionNotFoundException($"Terminal session '{sessionId}' was not found.");
        }

        if (!session.IsViewer(viewerId))
        {
            throw new TerminalSessionForbiddenException("Terminal session belongs to another viewer.");
        }

        return session;
    }

    private sealed class TerminalSessionInstance(string viewerId, TerminalSessionStartRequest request)
    {
        private readonly Lock _processSync = new();
        private readonly Lock _stateSync = new();
        private readonly SemaphoreSlim _inputDispatchLock = new(1, 1);
        private readonly Queue<TerminalOutputEntry> _outputBuffer = new();
        private readonly Queue<TerminalSessionInputChunkDiagnostics> _inputDiagnosticsBuffer = new();
        private readonly SortedDictionary<long, string> _pendingInputBySequence = new();
        private IProcess? _process;
        private string? _viewerId = viewerId;
        private string? _inputSequenceViewerId = viewerId;
        private long _lastOutputSequenceNumber;
        private long _lastAcceptedInputSequenceNumber;
        private long _nextInputSequenceNumber = 1;

        public string SessionId { get; } = request.SessionId;

        public TerminalSessionStartRequest Request { get; } = request;

        public DateTimeOffset CreatedAtUtc { get; } = DateTimeOffset.UtcNow;

        public DateTimeOffset? CompletedAtUtc { get; set; }

        public CancellationTokenSource CancelSource { get; } = new();

        public TerminalSessionState State { get; set; } = TerminalSessionState.Starting;

        public int? ProcessId { get; set; }

        public int? Columns { get; set; } = request.Columns;

        public int? Rows { get; set; } = request.Rows;

        public bool CloseRequested { get; private set; }

        public bool IsCompleted => State is TerminalSessionState.Closed or TerminalSessionState.Failed;

        public void RequestClose()
        {
            CloseRequested = true;
            CancelSource.Cancel();
        }

        public bool IsViewer(string viewerId)
        {
            lock (_stateSync)
            {
                return !string.IsNullOrWhiteSpace(_viewerId) &&
                       string.Equals(_viewerId, viewerId, StringComparison.Ordinal);
            }
        }

        public void Reattach(string viewerId)
        {
            lock (_stateSync)
            {
                _viewerId = viewerId;
                ResetInputSequenceStateCore(viewerId);
            }
        }

        public string? GetViewerId()
        {
            lock (_stateSync)
            {
                return _viewerId;
            }
        }

        public string GetViewerIdForLog()
        {
            return GetViewerId() ?? "<detached>";
        }

        public long AppendOutput(string text, bool isStdErr)
        {
            lock (_stateSync)
            {
                _lastOutputSequenceNumber++;
                _outputBuffer.Enqueue(new TerminalOutputEntry(_lastOutputSequenceNumber, text, isStdErr));

                while (_outputBuffer.Count > ReplayBufferCapacity)
                {
                    _ = _outputBuffer.Dequeue();
                }

                return _lastOutputSequenceNumber;
            }
        }

        public IReadOnlyCollection<TerminalOutputEntry> GetOutputReplay(long lastReceivedSequenceNumber)
        {
            lock (_stateSync)
            {
                if (_outputBuffer.Count == 0)
                {
                    return [];
                }

                return _outputBuffer
                    .Where(entry => entry.SequenceNumber > lastReceivedSequenceNumber)
                    .ToArray();
            }
        }

        public Task EnterInputDispatchAsync(CancellationToken cancellationToken)
        {
            return _inputDispatchLock.WaitAsync(cancellationToken);
        }

        public void ExitInputDispatch()
        {
            _inputDispatchLock.Release();
        }

        public IReadOnlyList<TerminalInputChunk> AcceptOrderedInput(string viewerId, long sequence, string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return [];
            }

            lock (_stateSync)
            {
                if (!string.Equals(_inputSequenceViewerId, viewerId, StringComparison.Ordinal))
                {
                    ResetInputSequenceStateCore(viewerId);
                }

                if (sequence < _nextInputSequenceNumber)
                {
                    return [];
                }

                if (_pendingInputBySequence.ContainsKey(sequence))
                {
                    return [];
                }

                if (_pendingInputBySequence.Count >= MaxPendingInputChunks)
                {
                    throw new TerminalSessionConflictException("Terminal input sequence window overflow.");
                }

                _pendingInputBySequence[sequence] = input;
                if (!_pendingInputBySequence.ContainsKey(_nextInputSequenceNumber))
                {
                    return [];
                }

                var orderedInput = new List<TerminalInputChunk>();
                while (_pendingInputBySequence.Remove(_nextInputSequenceNumber, out var nextChunk))
                {
                    orderedInput.Add(new TerminalInputChunk(_nextInputSequenceNumber, nextChunk));
                    _nextInputSequenceNumber++;
                }

                return orderedInput;
            }
        }

        public void RecordAcceptedInputChunk(TerminalSessionInputChunkDiagnostics chunk)
        {
            lock (_stateSync)
            {
                _lastAcceptedInputSequenceNumber = chunk.Sequence;
                _inputDiagnosticsBuffer.Enqueue(chunk);

                while (_inputDiagnosticsBuffer.Count > InputDiagnosticsCapacity)
                {
                    _ = _inputDiagnosticsBuffer.Dequeue();
                }
            }
        }

        public TerminalSessionInputDiagnostics CreateInputDiagnosticsSnapshot()
        {
            lock (_stateSync)
            {
                return new TerminalSessionInputDiagnostics(SessionId)
                {
                    State = State,
                    ViewerId = _viewerId,
                    NextExpectedSequence = _nextInputSequenceNumber,
                    LastAcceptedSequence = _lastAcceptedInputSequenceNumber,
                    PendingSequences = [.. _pendingInputBySequence.Keys],
                    RecentChunks = [.. _inputDiagnosticsBuffer],
                    GeneratedAtUtc = DateTimeOffset.UtcNow
                };
            }
        }

        public TerminalSessionHandle CreateHandleSnapshot()
        {
            lock (_stateSync)
            {
                return new TerminalSessionHandle(SessionId)
                {
                    State = State,
                    CreatedAtUtc = CreatedAtUtc,
                    ProcessId = ProcessId,
                    Error = State == TerminalSessionState.Failed ? "Terminal session is in failed state." : null
                };
            }
        }

        private void ResetInputSequenceStateCore(string viewerId)
        {
            _inputSequenceViewerId = viewerId;
            _nextInputSequenceNumber = 1;
            _pendingInputBySequence.Clear();
            _lastAcceptedInputSequenceNumber = 0;
            _inputDiagnosticsBuffer.Clear();
        }

        public void SetProcess(IProcess? process)
        {
            lock (_processSync)
            {
                _process = process;
            }
        }

        public IProcess? GetProcess()
        {
            lock (_processSync)
            {
                return _process;
            }
        }

        public void TryKillProcess()
        {
            var process = GetProcess();
            if (process is null)
            {
                return;
            }

            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // ignored
            }
        }

        public readonly record struct TerminalInputChunk(long Sequence, string Text);
    }

    private readonly record struct TerminalOutputEntry(long SequenceNumber, string Text, bool IsStdErr);
}

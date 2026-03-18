using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using AureTTY.Contracts.Abstractions;
using AureTTY.Contracts.DTOs;
using AureTTY.Contracts.Enums;
using AureTTY.Contracts.Exceptions;
using AureTTY.Core.Services;
using AureTTY.Execution.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AureTTY.Core.Tests;

public sealed class TerminalSessionServiceTests
{
    [Fact]
    public async Task StartAsync_ShouldUsePseudoTerminal_AndPublishStartedAndOutput()
    {
        var eventPublisher = new Mock<ITerminalSessionEventPublisher>();
        var processFactory = new Mock<IScriptProcessFactory>();
        var events = new ConcurrentQueue<TerminalSessionEvent>();
        using var signal = new SemaphoreSlim(0, int.MaxValue);
        using var process = new FakeTerminalProcess("hello");
        ProcessRuntimeOptions? capturedRuntimeOptions = null;

        eventPublisher
            .Setup(n => n.SendTerminalSessionEventAsync(It.IsAny<string>(), It.IsAny<TerminalSessionEvent>()))
            .Callback<string, TerminalSessionEvent>((_, e) =>
            {
                events.Enqueue(e);
                signal.Release();
            })
            .Returns(Task.CompletedTask);

        processFactory
            .Setup(f => f.Create(It.IsAny<ExecutionRunContext>(), It.IsAny<ProcessCredentialOptions?>(), It.IsAny<ProcessRuntimeOptions?>()))
            .Callback<ExecutionRunContext, ProcessCredentialOptions?, ProcessRuntimeOptions?>((_, _, runtime) => capturedRuntimeOptions = runtime)
            .Returns(process);

        var service = new TerminalSessionService(processFactory.Object, eventPublisher.Object, NullLogger<TerminalSessionService>.Instance);
        var request = new TerminalSessionStartRequest("session-1", Shell.Pwsh)
        {
            Columns = 120,
            Rows = 40
        };

        _ = await service.StartAsync("viewer-1", request);
        await WaitForEventAsync(signal, expectedCount: 2);

        Assert.NotNull(capturedRuntimeOptions);
        Assert.True(capturedRuntimeOptions!.UsePseudoTerminal);
        Assert.True(capturedRuntimeOptions.RequirePseudoTerminal);
        Assert.Equal(120, capturedRuntimeOptions.Columns);
        Assert.Equal(40, capturedRuntimeOptions.Rows);
        Assert.Contains(events, e => e.EventType == TerminalSessionEventType.Started);
        Assert.Contains(events, e => e.EventType == TerminalSessionEventType.Output && e.Text == "hello");

        await service.CloseAsync("viewer-1", "session-1");
    }

    [Fact]
    public async Task ResumeAsync_ShouldReplayBufferedOutput()
    {
        var eventPublisher = new Mock<ITerminalSessionEventPublisher>();
        var processFactory = new Mock<IScriptProcessFactory>();
        var events = new ConcurrentQueue<TerminalSessionEvent>();
        using var signal = new SemaphoreSlim(0, int.MaxValue);
        using var process = new FakeTerminalProcess("replay-me");

        eventPublisher
            .Setup(n => n.SendTerminalSessionEventAsync(It.IsAny<string>(), It.IsAny<TerminalSessionEvent>()))
            .Callback<string, TerminalSessionEvent>((_, e) =>
            {
                events.Enqueue(e);
                signal.Release();
            })
            .Returns(Task.CompletedTask);

        processFactory
            .Setup(f => f.Create(It.IsAny<ExecutionRunContext>(), It.IsAny<ProcessCredentialOptions?>(), It.IsAny<ProcessRuntimeOptions?>()))
            .Returns(process);

        var service = new TerminalSessionService(processFactory.Object, eventPublisher.Object, NullLogger<TerminalSessionService>.Instance);
        var request = new TerminalSessionStartRequest("session-2", Shell.Pwsh);
        _ = await service.StartAsync("viewer-1", request);
        await WaitForEventAsync(signal, expectedCount: 2);

        var beforeReplayCount = events.Count(e => e.EventType == TerminalSessionEventType.Output);

        _ = await service.ResumeAsync("viewer-1", new TerminalSessionResumeRequest("session-2")
        {
            LastReceivedSequenceNumber = 0
        });
        await WaitForEventAsync(signal, expectedCount: 1);

        var afterReplayCount = events.Count(e => e.EventType == TerminalSessionEventType.Output);
        Assert.True(afterReplayCount > beforeReplayCount);

        await service.CloseAsync("viewer-1", "session-2");
    }

    [Fact]
    public async Task ResizeAsync_ShouldInvokeResizableProcess()
    {
        var eventPublisher = new Mock<ITerminalSessionEventPublisher>();
        var processFactory = new Mock<IScriptProcessFactory>();
        using var process = new FakeTerminalProcess(string.Empty);
        var service = new TerminalSessionService(processFactory.Object, eventPublisher.Object, NullLogger<TerminalSessionService>.Instance);

        processFactory
            .Setup(f => f.Create(It.IsAny<ExecutionRunContext>(), It.IsAny<ProcessCredentialOptions?>(), It.IsAny<ProcessRuntimeOptions?>()))
            .Returns(process);

        _ = await service.StartAsync("viewer-1", new TerminalSessionStartRequest("session-3", Shell.Pwsh));
        await Task.Delay(100);

        await service.ResizeAsync("viewer-1", new TerminalSessionResizeRequest("session-3", 180, 55));

        Assert.Equal(180, process.LastResizeColumns);
        Assert.Equal(55, process.LastResizeRows);

        await service.CloseAsync("viewer-1", "session-3");
    }

    [Fact]
    public async Task SignalAsync_Interrupt_ShouldWriteCtrlC()
    {
        var eventPublisher = new Mock<ITerminalSessionEventPublisher>();
        var processFactory = new Mock<IScriptProcessFactory>();
        using var process = new FakeTerminalProcess(string.Empty, processId: 0);
        var service = new TerminalSessionService(processFactory.Object, eventPublisher.Object, NullLogger<TerminalSessionService>.Instance);

        processFactory
            .Setup(f => f.Create(It.IsAny<ExecutionRunContext>(), It.IsAny<ProcessCredentialOptions?>(), It.IsAny<ProcessRuntimeOptions?>()))
            .Returns(process);

        _ = await service.StartAsync("viewer-1", new TerminalSessionStartRequest("session-4", Shell.Pwsh));
        await Task.Delay(100);

        await service.SignalAsync("viewer-1", "session-4", TerminalSessionSignal.Interrupt);

        Assert.Contains("\u0003", process.GetInputBuffer(), StringComparison.Ordinal);
        await service.CloseAsync("viewer-1", "session-4");
    }

    [Fact]
    public async Task SendInputAsync_ShouldNormalizeLineEndingsForPlatform()
    {
        var eventPublisher = new Mock<ITerminalSessionEventPublisher>();
        var processFactory = new Mock<IScriptProcessFactory>();
        using var process = new FakeTerminalProcess(string.Empty);
        var service = new TerminalSessionService(processFactory.Object, eventPublisher.Object, NullLogger<TerminalSessionService>.Instance);

        processFactory
            .Setup(f => f.Create(It.IsAny<ExecutionRunContext>(), It.IsAny<ProcessCredentialOptions?>(), It.IsAny<ProcessRuntimeOptions?>()))
            .Returns(process);

        _ = await service.StartAsync("viewer-1", new TerminalSessionStartRequest("session-6", Shell.Pwsh));
        await Task.Delay(100);

        await service.SendInputAsync("viewer-1", new TerminalSessionInputRequest("session-6", "abc\rdef\r\nghi", 1));

        var expected = OperatingSystem.IsWindows()
            ? "abc\rdef\rghi"
            : "abc\ndef\nghi";
        Assert.Contains(expected, process.GetInputBuffer(), StringComparison.Ordinal);

        await service.CloseAsync("viewer-1", "session-6");
    }

    [Fact]
    public async Task SendInputAsync_ShouldPreserveInputOrderAsReceived()
    {
        var eventPublisher = new Mock<ITerminalSessionEventPublisher>();
        var processFactory = new Mock<IScriptProcessFactory>();
        using var process = new FakeTerminalProcess(string.Empty);
        var service = new TerminalSessionService(processFactory.Object, eventPublisher.Object, NullLogger<TerminalSessionService>.Instance);

        processFactory
            .Setup(f => f.Create(It.IsAny<ExecutionRunContext>(), It.IsAny<ProcessCredentialOptions?>(), It.IsAny<ProcessRuntimeOptions?>()))
            .Returns(process);

        _ = await service.StartAsync("viewer-1", new TerminalSessionStartRequest("session-input-order", Shell.Pwsh));
        await Task.Delay(100);

        await service.SendInputAsync("viewer-1", new TerminalSessionInputRequest("session-input-order", "B", 1));
        await service.SendInputAsync("viewer-1", new TerminalSessionInputRequest("session-input-order", "A", 2));
        await service.SendInputAsync("viewer-1", new TerminalSessionInputRequest("session-input-order", "C", 3));

        Assert.Equal("BAC", process.GetInputBuffer());
        await service.CloseAsync("viewer-1", "session-input-order");
    }

    [Fact]
    public async Task SendInputAsync_WhenSequencesArriveOutOfOrder_ShouldFlushInSequenceOrder()
    {
        var eventPublisher = new Mock<ITerminalSessionEventPublisher>();
        var processFactory = new Mock<IScriptProcessFactory>();
        using var process = new FakeTerminalProcess(string.Empty);
        var service = new TerminalSessionService(processFactory.Object, eventPublisher.Object, NullLogger<TerminalSessionService>.Instance);

        processFactory
            .Setup(f => f.Create(It.IsAny<ExecutionRunContext>(), It.IsAny<ProcessCredentialOptions?>(), It.IsAny<ProcessRuntimeOptions?>()))
            .Returns(process);

        _ = await service.StartAsync("viewer-1", new TerminalSessionStartRequest("session-input-reorder", Shell.Pwsh));
        await Task.Delay(100);

        await service.SendInputAsync("viewer-1", new TerminalSessionInputRequest("session-input-reorder", "B", 2));
        await service.SendInputAsync("viewer-1", new TerminalSessionInputRequest("session-input-reorder", "A", 1));
        await service.SendInputAsync("viewer-1", new TerminalSessionInputRequest("session-input-reorder", "C", 3));

        Assert.Equal("ABC", process.GetInputBuffer());
        await service.CloseAsync("viewer-1", "session-input-reorder");
    }

    [Fact]
    public async Task SendInputAsync_WhenSequenceIsDuplicate_ShouldIgnoreDuplicateChunk()
    {
        var eventPublisher = new Mock<ITerminalSessionEventPublisher>();
        var processFactory = new Mock<IScriptProcessFactory>();
        using var process = new FakeTerminalProcess(string.Empty);
        var service = new TerminalSessionService(processFactory.Object, eventPublisher.Object, NullLogger<TerminalSessionService>.Instance);

        processFactory
            .Setup(f => f.Create(It.IsAny<ExecutionRunContext>(), It.IsAny<ProcessCredentialOptions?>(), It.IsAny<ProcessRuntimeOptions?>()))
            .Returns(process);

        _ = await service.StartAsync("viewer-1", new TerminalSessionStartRequest("session-input-duplicate", Shell.Pwsh));
        await Task.Delay(100);

        await service.SendInputAsync("viewer-1", new TerminalSessionInputRequest("session-input-duplicate", "A", 1));
        await service.SendInputAsync("viewer-1", new TerminalSessionInputRequest("session-input-duplicate", "A", 1));
        await service.SendInputAsync("viewer-1", new TerminalSessionInputRequest("session-input-duplicate", "B", 2));

        Assert.Equal("AB", process.GetInputBuffer());
        await service.CloseAsync("viewer-1", "session-input-duplicate");
    }

    [Fact]
    public async Task SendInputAsync_ShouldPreserveEscapeData()
    {
        var eventPublisher = new Mock<ITerminalSessionEventPublisher>();
        var processFactory = new Mock<IScriptProcessFactory>();
        using var process = new FakeTerminalProcess(string.Empty);
        var service = new TerminalSessionService(processFactory.Object, eventPublisher.Object, NullLogger<TerminalSessionService>.Instance);

        processFactory
            .Setup(f => f.Create(It.IsAny<ExecutionRunContext>(), It.IsAny<ProcessCredentialOptions?>(), It.IsAny<ProcessRuntimeOptions?>()))
            .Returns(process);

        _ = await service.StartAsync("viewer-1", new TerminalSessionStartRequest("session-escape-preserve", Shell.Pwsh));
        await Task.Delay(100);

        await service.SendInputAsync("viewer-1", new TerminalSessionInputRequest("session-escape-preserve", "\u001b[", 1));
        await service.SendInputAsync("viewer-1", new TerminalSessionInputRequest("session-escape-preserve", "A", 2));

        Assert.Equal("\u001b[A", process.GetInputBuffer());
        await service.CloseAsync("viewer-1", "session-escape-preserve");
    }

    [Fact]
    public async Task SendInputAsync_WhenPayloadContainsDeleteChar_ShouldPreserveDeleteCharacter()
    {
        var eventPublisher = new Mock<ITerminalSessionEventPublisher>();
        var processFactory = new Mock<IScriptProcessFactory>();
        using var process = new FakeTerminalProcess(string.Empty);
        var service = new TerminalSessionService(processFactory.Object, eventPublisher.Object, NullLogger<TerminalSessionService>.Instance);

        processFactory
            .Setup(f => f.Create(It.IsAny<ExecutionRunContext>(), It.IsAny<ProcessCredentialOptions?>(), It.IsAny<ProcessRuntimeOptions?>()))
            .Returns(process);

        _ = await service.StartAsync("viewer-1", new TerminalSessionStartRequest("session-delete-char", Shell.Pwsh));
        await Task.Delay(100);

        await service.SendInputAsync("viewer-1", new TerminalSessionInputRequest("session-delete-char", "a\u007fb", 1));

        Assert.Equal("a\u007fb", process.GetInputBuffer());
        await service.CloseAsync("viewer-1", "session-delete-char");
    }

    [Fact]
    public async Task SendInputAsync_WhenPayloadContainsBackspaceChar_ShouldPreserveBackspaceCharacter()
    {
        var eventPublisher = new Mock<ITerminalSessionEventPublisher>();
        var processFactory = new Mock<IScriptProcessFactory>();
        using var process = new FakeTerminalProcess(string.Empty);
        var service = new TerminalSessionService(processFactory.Object, eventPublisher.Object, NullLogger<TerminalSessionService>.Instance);

        processFactory
            .Setup(f => f.Create(It.IsAny<ExecutionRunContext>(), It.IsAny<ProcessCredentialOptions?>(), It.IsAny<ProcessRuntimeOptions?>()))
            .Returns(process);

        _ = await service.StartAsync("viewer-1", new TerminalSessionStartRequest("session-backspace-char", Shell.Pwsh));
        await Task.Delay(100);

        await service.SendInputAsync("viewer-1", new TerminalSessionInputRequest("session-backspace-char", "a\bb", 1));

        Assert.Equal("a\bb", process.GetInputBuffer());
        await service.CloseAsync("viewer-1", "session-backspace-char");
    }

    [Fact]
    public async Task CloseViewerSessionsAsync_WhenViewerDisconnected_ShouldTerminateOwnedSessions()
    {
        var eventPublisher = new Mock<ITerminalSessionEventPublisher>();
        var processFactory = new Mock<IScriptProcessFactory>();
        using var process = new FakeTerminalProcess(string.Empty);
        var service = new TerminalSessionService(processFactory.Object, eventPublisher.Object, NullLogger<TerminalSessionService>.Instance);

        processFactory
            .Setup(f => f.Create(It.IsAny<ExecutionRunContext>(), It.IsAny<ProcessCredentialOptions?>(), It.IsAny<ProcessRuntimeOptions?>()))
            .Returns(process);

        _ = await service.StartAsync("viewer-1", new TerminalSessionStartRequest("session-disconnect", Shell.Pwsh));
        await Task.Delay(100);

        await service.CloseViewerSessionsAsync("viewer-1");
        await Task.Delay(50);

        Assert.True(process.HasExited);
    }

    [Fact]
    public async Task CloseViewerSessionsAsync_WhenViewerDoesNotOwnSession_ShouldNotTerminateSession()
    {
        var eventPublisher = new Mock<ITerminalSessionEventPublisher>();
        var processFactory = new Mock<IScriptProcessFactory>();
        using var process = new FakeTerminalProcess(string.Empty);
        var service = new TerminalSessionService(processFactory.Object, eventPublisher.Object, NullLogger<TerminalSessionService>.Instance);

        processFactory
            .Setup(f => f.Create(It.IsAny<ExecutionRunContext>(), It.IsAny<ProcessCredentialOptions?>(), It.IsAny<ProcessRuntimeOptions?>()))
            .Returns(process);

        _ = await service.StartAsync("viewer-1", new TerminalSessionStartRequest("session-other-viewer", Shell.Pwsh));
        await Task.Delay(100);

        await service.CloseViewerSessionsAsync("viewer-2");
        await Task.Delay(50);

        Assert.False(process.HasExited);
        await service.CloseAsync("viewer-1", "session-other-viewer");
    }

    [Fact]
    public async Task GetViewerSessionsAsync_ShouldReturnOnlyOwnedSessions()
    {
        var eventPublisher = new Mock<ITerminalSessionEventPublisher>();
        var processFactory = new Mock<IScriptProcessFactory>();
        using var process = new FakeTerminalProcess(string.Empty);
        var service = new TerminalSessionService(processFactory.Object, eventPublisher.Object, NullLogger<TerminalSessionService>.Instance);

        processFactory
            .Setup(f => f.Create(It.IsAny<ExecutionRunContext>(), It.IsAny<ProcessCredentialOptions?>(), It.IsAny<ProcessRuntimeOptions?>()))
            .Returns(process);

        _ = await service.StartAsync("viewer-1", new TerminalSessionStartRequest("session-viewer-1", Shell.Pwsh));
        _ = await service.StartAsync("viewer-2", new TerminalSessionStartRequest("session-viewer-2", Shell.Pwsh));

        var sessions = await service.GetViewerSessionsAsync("viewer-1");

        Assert.Single(sessions);
        Assert.Equal("session-viewer-1", sessions.Single().SessionId);

        await service.CloseAllSessionsAsync();
    }

    [Fact]
    public async Task GetAllSessionsAsync_ShouldReturnAllActiveSessions()
    {
        var eventPublisher = new Mock<ITerminalSessionEventPublisher>();
        var processFactory = new Mock<IScriptProcessFactory>();
        using var process = new FakeTerminalProcess(string.Empty);
        var service = new TerminalSessionService(processFactory.Object, eventPublisher.Object, NullLogger<TerminalSessionService>.Instance);

        processFactory
            .Setup(f => f.Create(It.IsAny<ExecutionRunContext>(), It.IsAny<ProcessCredentialOptions?>(), It.IsAny<ProcessRuntimeOptions?>()))
            .Returns(process);

        _ = await service.StartAsync("viewer-1", new TerminalSessionStartRequest("session-all-a", Shell.Pwsh));
        _ = await service.StartAsync("viewer-2", new TerminalSessionStartRequest("session-all-b", Shell.Pwsh));

        var sessions = await service.GetAllSessionsAsync();

        Assert.Equal(2, sessions.Count);
        Assert.Contains(sessions, session => session.SessionId == "session-all-a");
        Assert.Contains(sessions, session => session.SessionId == "session-all-b");

        await service.CloseAllSessionsAsync();
    }

    [Fact]
    public async Task GetSessionAsync_WhenViewerDoesNotOwnSession_ThrowsForbiddenException()
    {
        var eventPublisher = new Mock<ITerminalSessionEventPublisher>();
        var processFactory = new Mock<IScriptProcessFactory>();
        using var process = new FakeTerminalProcess(string.Empty);
        var service = new TerminalSessionService(processFactory.Object, eventPublisher.Object, NullLogger<TerminalSessionService>.Instance);

        processFactory
            .Setup(f => f.Create(It.IsAny<ExecutionRunContext>(), It.IsAny<ProcessCredentialOptions?>(), It.IsAny<ProcessRuntimeOptions?>()))
            .Returns(process);

        _ = await service.StartAsync("viewer-owner", new TerminalSessionStartRequest("session-secured", Shell.Pwsh));

        var act = () => service.GetSessionAsync("viewer-stranger", "session-secured");
        await Assert.ThrowsAsync<TerminalSessionForbiddenException>(act);

        await service.CloseAllSessionsAsync();
    }

    [Fact]
    public async Task StartAsync_WhenProcessStartThrows_ShouldPublishFailedEvent()
    {
        var eventPublisher = new Mock<ITerminalSessionEventPublisher>();
        var processFactory = new Mock<IScriptProcessFactory>();
        var events = new ConcurrentQueue<TerminalSessionEvent>();
        using var signal = new SemaphoreSlim(0, int.MaxValue);
        using var process = new FakeTerminalProcess(string.Empty, failOnStart: true);

        eventPublisher
            .Setup(n => n.SendTerminalSessionEventAsync(It.IsAny<string>(), It.IsAny<TerminalSessionEvent>()))
            .Callback<string, TerminalSessionEvent>((_, e) =>
            {
                events.Enqueue(e);
                signal.Release();
            })
            .Returns(Task.CompletedTask);

        processFactory
            .Setup(f => f.Create(It.IsAny<ExecutionRunContext>(), It.IsAny<ProcessCredentialOptions?>(), It.IsAny<ProcessRuntimeOptions?>()))
            .Returns(process);

        var service = new TerminalSessionService(processFactory.Object, eventPublisher.Object, NullLogger<TerminalSessionService>.Instance);
        _ = await service.StartAsync("viewer-1", new TerminalSessionStartRequest("session-5", Shell.Pwsh));
        await WaitForEventAsync(signal, expectedCount: 1);

        Assert.Contains(events, e => e.EventType == TerminalSessionEventType.Failed);
    }

    [Fact]
    public async Task CloseViewerSessionsAsync_WithHangingOutputReaders_ShouldNotExhaustSessionLimit()
    {
        var eventPublisher = new Mock<ITerminalSessionEventPublisher>();
        var processFactory = new Mock<IScriptProcessFactory>();
        var processes = new List<HangingOutputTerminalProcess>();

        processFactory
            .Setup(f => f.Create(It.IsAny<ExecutionRunContext>(), It.IsAny<ProcessCredentialOptions?>(), It.IsAny<ProcessRuntimeOptions?>()))
            .Returns(() =>
            {
                var process = new HangingOutputTerminalProcess();
                processes.Add(process);
                return process;
            });

        var service = new TerminalSessionService(processFactory.Object, eventPublisher.Object, NullLogger<TerminalSessionService>.Instance);

        Exception? failure = null;
        try
        {
            for (var cycle = 1; cycle <= 10; cycle++)
            {
                var sessionId = $"session-hanging-{cycle}";
                _ = await service.StartAsync("viewer-hanging", new TerminalSessionStartRequest(sessionId, Shell.Pwsh));
                await Task.Delay(40);

                await service.CloseViewerSessionsAsync("viewer-hanging");
                await Task.Delay(450);
            }
        }
        catch (Exception ex)
        {
            failure = ex;
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }

        Assert.Null(failure);
    }

    [Fact]
    public async Task CloseViewerSessionsAsync_WhenProcessIgnoresExitCancellation_ShouldNotExhaustSessionLimit()
    {
        var eventPublisher = new Mock<ITerminalSessionEventPublisher>();
        var processFactory = new Mock<IScriptProcessFactory>();
        var processes = new List<NonCancelableExitWaitTerminalProcess>();

        processFactory
            .Setup(f => f.Create(It.IsAny<ExecutionRunContext>(), It.IsAny<ProcessCredentialOptions?>(), It.IsAny<ProcessRuntimeOptions?>()))
            .Returns(() =>
            {
                var process = new NonCancelableExitWaitTerminalProcess();
                processes.Add(process);
                return process;
            });

        var service = new TerminalSessionService(processFactory.Object, eventPublisher.Object, NullLogger<TerminalSessionService>.Instance);

        Exception? failure = null;
        try
        {
            for (var cycle = 1; cycle <= 10; cycle++)
            {
                var sessionId = $"session-noncancel-{cycle}";
                _ = await service.StartAsync("viewer-noncancel", new TerminalSessionStartRequest(sessionId, Shell.Pwsh));
                await Task.Delay(40);

                await service.CloseViewerSessionsAsync("viewer-noncancel");
                await Task.Delay(700);
            }
        }
        catch (Exception ex)
        {
            failure = ex;
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }

        Assert.Null(failure);
    }

    [Fact]
    public async Task CloseAllSessionsAsync_ShouldTerminateAllActiveSessions()
    {
        var eventPublisher = new Mock<ITerminalSessionEventPublisher>();
        var processFactory = new Mock<IScriptProcessFactory>();
        var firstProcess = new FakeTerminalProcess(string.Empty);
        var secondProcess = new FakeTerminalProcess(string.Empty, processId: 5252);
        var processQueue = new Queue<IProcess>([firstProcess, secondProcess]);

        processFactory
            .Setup(f => f.Create(It.IsAny<ExecutionRunContext>(), It.IsAny<ProcessCredentialOptions?>(), It.IsAny<ProcessRuntimeOptions?>()))
            .Returns(() => processQueue.Dequeue());

        var service = new TerminalSessionService(processFactory.Object, eventPublisher.Object, NullLogger<TerminalSessionService>.Instance);

        _ = await service.StartAsync("viewer-one", new TerminalSessionStartRequest("session-all-1", Shell.Pwsh));
        _ = await service.StartAsync("viewer-two", new TerminalSessionStartRequest("session-all-2", Shell.Pwsh));
        await Task.Delay(100);

        await service.CloseAllSessionsAsync();
        await Task.Delay(100);

        Assert.True(firstProcess.HasExited);
        Assert.True(secondProcess.HasExited);

        firstProcess.Dispose();
        secondProcess.Dispose();
    }

    private static async Task WaitForEventAsync(SemaphoreSlim signal, int expectedCount)
    {
        var remaining = expectedCount;
        while (remaining > 0)
        {
            var signaled = await signal.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(signaled, "Timed out while waiting for terminal events.");
            remaining--;
        }
    }

    private sealed class FakeTerminalProcess : IProcess, IResizableTerminalProcess
    {
        private readonly bool _failOnStart;
        private readonly MemoryStream _inputStream = new();
        private readonly TaskCompletionSource _waitForExit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _disposed;

        public FakeTerminalProcess(string stdoutContent, bool failOnStart = false, int processId = 4242)
        {
            _failOnStart = failOnStart;

            StandardInput = new StreamWriter(_inputStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true)
            {
                AutoFlush = true
            };

            var stdoutBytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(stdoutContent);
            StandardOutput = new StreamReader(new MemoryStream(stdoutBytes, writable: false), Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
            StandardError = new StreamReader(new MemoryStream(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
            Id = processId;
        }

        public int LastResizeColumns { get; private set; }

        public int LastResizeRows { get; private set; }

        public int Id { get; private set; }

        public int ExitCode { get; private set; }

        public int SessionId { get; } = 1;

        public StreamWriter StandardInput { get; }

        public StreamReader StandardOutput { get; }

        public StreamReader StandardError { get; }

        public ProcessModule? MainModule => null;

        public string ProcessName => "fake-terminal";

        public long WorkingSet64 => 0;

        public DateTime StartTime => DateTime.UtcNow;

        public bool HasExited { get; private set; }

        public Task StartAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(startInfo);
            cancellationToken.ThrowIfCancellationRequested();

            if (_failOnStart)
            {
                throw new InvalidOperationException("Fake process start failed.");
            }

            return Task.CompletedTask;
        }

        public void Kill(bool entireProcessTree = false)
        {
            _ = entireProcessTree;
            HasExited = true;
            ExitCode = 1;
            _waitForExit.TrySetResult();
        }

        public Task<string[]> GetCommandLineAsync()
        {
            return Task.FromResult(Array.Empty<string>());
        }

        public bool WaitForExit(uint millisecondsTimeout = uint.MaxValue)
        {
            if (HasExited)
            {
                return true;
            }

            return _waitForExit.Task.Wait((int)Math.Min(millisecondsTimeout, int.MaxValue));
        }

        public Task WaitForExitAsync(CancellationToken cancellationToken = default)
        {
            return _waitForExit.Task.WaitAsync(cancellationToken);
        }

        public Task ResizeTerminalAsync(int columns, int rows, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastResizeColumns = columns;
            LastResizeRows = rows;
            return Task.CompletedTask;
        }

        public string GetInputBuffer()
        {
            return Encoding.UTF8.GetString(_inputStream.ToArray());
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            StandardInput.Dispose();
            StandardOutput.Dispose();
            StandardError.Dispose();
            _inputStream.Dispose();
            _waitForExit.TrySetResult();
        }
    }

    private sealed class HangingOutputTerminalProcess : IProcess
    {
        private readonly TaskCompletionSource _waitForExit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly BlockingReadStream _blockingReadStream = new();
        private bool _disposed;

        public HangingOutputTerminalProcess()
        {
            StandardInput = new StreamWriter(new MemoryStream(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: false)
            {
                AutoFlush = true
            };

            StandardOutput = new StreamReader(_blockingReadStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
            StandardError = new StreamReader(new MemoryStream(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
            Id = Random.Shared.Next(10000, 50000);
        }

        public int Id { get; }

        public int ExitCode { get; private set; }

        public int SessionId { get; } = 1;

        public StreamWriter StandardInput { get; }

        public StreamReader StandardOutput { get; }

        public StreamReader StandardError { get; }

        public ProcessModule? MainModule => null;

        public string ProcessName => "hanging-output";

        public long WorkingSet64 => 0;

        public DateTime StartTime => DateTime.UtcNow;

        public bool HasExited { get; private set; }

        public Task StartAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken = default)
        {
            _ = startInfo;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public void Kill(bool entireProcessTree = false)
        {
            _ = entireProcessTree;
            HasExited = true;
            ExitCode = 1;
            _waitForExit.TrySetResult();
        }

        public Task<string[]> GetCommandLineAsync()
        {
            return Task.FromResult(Array.Empty<string>());
        }

        public bool WaitForExit(uint millisecondsTimeout = uint.MaxValue)
        {
            if (HasExited)
            {
                return true;
            }

            return _waitForExit.Task.Wait((int)Math.Min(millisecondsTimeout, int.MaxValue));
        }

        public Task WaitForExitAsync(CancellationToken cancellationToken = default)
        {
            return _waitForExit.Task.WaitAsync(cancellationToken);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            StandardInput.Dispose();
            StandardOutput.Dispose();
            StandardError.Dispose();
            _waitForExit.TrySetResult();
        }
    }

    private sealed class NonCancelableExitWaitTerminalProcess : IProcess
    {
        private readonly TaskCompletionSource _waitForExit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _disposed;

        public NonCancelableExitWaitTerminalProcess()
        {
            StandardInput = new StreamWriter(new MemoryStream(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: false)
            {
                AutoFlush = true
            };

            StandardOutput = new StreamReader(new MemoryStream(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
            StandardError = new StreamReader(new MemoryStream(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
            Id = Random.Shared.Next(50001, 90000);
        }

        public int Id { get; }

        public int ExitCode { get; private set; }

        public int SessionId { get; } = 1;

        public StreamWriter StandardInput { get; }

        public StreamReader StandardOutput { get; }

        public StreamReader StandardError { get; }

        public ProcessModule? MainModule => null;

        public string ProcessName => "noncancel-exit-wait";

        public long WorkingSet64 => 0;

        public DateTime StartTime => DateTime.UtcNow;

        public bool HasExited { get; private set; }

        public Task StartAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken = default)
        {
            _ = startInfo;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public void Kill(bool entireProcessTree = false)
        {
            _ = entireProcessTree;
            HasExited = true;
            ExitCode = 1;
        }

        public Task<string[]> GetCommandLineAsync()
        {
            return Task.FromResult(Array.Empty<string>());
        }

        public bool WaitForExit(uint millisecondsTimeout = uint.MaxValue)
        {
            _ = millisecondsTimeout;
            return HasExited;
        }

        public Task WaitForExitAsync(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return _waitForExit.Task;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            StandardInput.Dispose();
            StandardOutput.Dispose();
            StandardError.Dispose();
            _waitForExit.TrySetResult();
        }
    }

    private sealed class BlockingReadStream : Stream
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _disposed;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            _ = buffer;
            _ = offset;
            _ = count;
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            _ = offset;
            _ = origin;
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            _ = value;
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _ = buffer;
            _ = offset;
            _ = count;
            throw new NotSupportedException();
        }

        public async override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            _ = buffer;

            if (_disposed)
            {
                return 0;
            }

            await _release.Task.WaitAsync(cancellationToken);
            return 0;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                _disposed = true;
                _release.TrySetResult();
            }

            base.Dispose(disposing);
        }
    }
}

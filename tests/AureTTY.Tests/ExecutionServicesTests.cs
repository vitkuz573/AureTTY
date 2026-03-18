using System.Diagnostics;
using AureTTY.Contracts.Enums;
using AureTTY.Execution.Abstractions;
using AureTTY.Execution.Services;
using Moq;

namespace AureTTY.Tests;

public sealed class ExecutionServicesTests
{
    [Fact]
    public void ProcessCredentialOptions_WhenCreated_StoresValuesAndDefaults()
    {
        var options = new ProcessCredentialOptions("demo-user", "demo-pass")
        {
            Domain = "demo-domain"
        };

        Assert.Equal("demo-user", options.UserName);
        Assert.Equal("demo-pass", options.Password);
        Assert.Equal("demo-domain", options.Domain);
        Assert.True(options.LoadUserProfile);
    }

    [Fact]
    public void ShellLaunchPlanner_WhenUsingWindowsShells_ProducesExpectedStartInfo()
    {
        var cmd = ShellLaunchPlanner.BuildInteractiveShellStartInfo(Shell.Cmd, "C:\\");
        var powershell = ShellLaunchPlanner.BuildInteractiveShellStartInfo(Shell.PowerShell, null);
        var pwsh = ShellLaunchPlanner.BuildInteractiveShellStartInfo(Shell.Pwsh, null);
        var bash = ShellLaunchPlanner.BuildInteractiveShellStartInfo(Shell.Bash, null);

        Assert.Equal("cmd", cmd.FileName);
        Assert.Contains("/Q", cmd.ArgumentList);
        Assert.Equal("C:\\", cmd.WorkingDirectory);
        Assert.True(cmd.RedirectStandardOutput);
        Assert.True(cmd.RedirectStandardInput);

        Assert.Equal("powershell", powershell.FileName);
        Assert.Contains("-NoLogo", powershell.ArgumentList);
        Assert.Contains("-NoProfile", powershell.ArgumentList);
        Assert.Contains("Bypass", powershell.ArgumentList);

        Assert.Equal("pwsh", pwsh.FileName);
        Assert.Contains("-NoLogo", pwsh.ArgumentList);
        Assert.Contains("-NoProfile", pwsh.ArgumentList);

        Assert.Equal("bash", bash.FileName);
        Assert.Contains("--noprofile", bash.ArgumentList);
        Assert.Contains("--norc", bash.ArgumentList);
    }

    [Fact]
    public void ShellLaunchPlanner_WhenShellIsUnsupported_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => ShellLaunchPlanner.BuildInteractiveShellStartInfo((Shell)999, null));

        Assert.Contains("Unsupported shell", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessWrapper_WhenNotStarted_HasExitedReturnsFalseAndCommandLineDelegates()
    {
        using var process = new Process();
        var commandLineProvider = new Mock<ICommandLineProvider>(MockBehavior.Strict);
        commandLineProvider
            .Setup(provider => provider.GetCommandLineAsync(It.IsAny<IProcess>()))
            .ReturnsAsync(["cmd", "/c", "echo", "demo"]);

        using var wrapper = new ProcessWrapper(process, commandLineProvider.Object);

        Assert.False(wrapper.HasExited);
        var commandLine = await wrapper.GetCommandLineAsync();
        Assert.Equal(["cmd", "/c", "echo", "demo"], commandLine);
        commandLineProvider.VerifyAll();
    }

    [Fact]
    public async Task ProcessWrapper_StartAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        using var process = new Process();
        var commandLineProvider = new Mock<ICommandLineProvider>(MockBehavior.Strict);
        using var wrapper = new ProcessWrapper(process, commandLineProvider.Object);

        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => wrapper.StartAsync(new ProcessStartInfo
        {
            FileName = "cmd",
            Arguments = "/c exit 0",
            UseShellExecute = false
        }, cancellationTokenSource.Token));
    }

    [Fact]
    public async Task ProcessWrapper_StartAsyncAndWaitForExit_CompletesSuccessfully()
    {
        using var process = new Process();
        var commandLineProvider = new Mock<ICommandLineProvider>(MockBehavior.Strict);
        using var wrapper = new ProcessWrapper(process, commandLineProvider.Object);

        var startInfo = new ProcessStartInfo
        {
            FileName = "cmd",
            Arguments = "/c exit 0",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        await wrapper.StartAsync(startInfo);
        Assert.True(wrapper.WaitForExit(5000));
        await wrapper.WaitForExitAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ProcessWrapper_WhenStarted_ExposesWrappedProcessProperties()
    {
        using var process = new Process();
        var commandLineProvider = new Mock<ICommandLineProvider>(MockBehavior.Strict);
        using var wrapper = new ProcessWrapper(process, commandLineProvider.Object);

        await wrapper.StartAsync(new ProcessStartInfo
        {
            FileName = "cmd",
            Arguments = "/c ping 127.0.0.1 -n 5 > nul",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        });

        Assert.Equal(process.Id, wrapper.Id);
        Assert.Equal(process.SessionId, wrapper.SessionId);
        Assert.Equal(process.ProcessName, wrapper.ProcessName);
        Assert.Equal(process.WorkingSet64, wrapper.WorkingSet64);
        Assert.Equal(process.StartTime, wrapper.StartTime);
        Assert.Same(process.StandardInput, wrapper.StandardInput);
        Assert.Same(process.StandardOutput, wrapper.StandardOutput);
        Assert.Same(process.StandardError, wrapper.StandardError);
        Assert.Equal(process.MainModule?.ModuleName, wrapper.MainModule?.ModuleName);

        wrapper.Kill(entireProcessTree: true);
        await wrapper.WaitForExitAsync(CancellationToken.None);
        Assert.Equal(process.ExitCode, wrapper.ExitCode);
    }

    [Fact]
    public async Task ProcessWrapper_WhenLongRunningProcess_KillTerminatesProcess()
    {
        using var process = new Process();
        var commandLineProvider = new Mock<ICommandLineProvider>(MockBehavior.Strict);
        using var wrapper = new ProcessWrapper(process, commandLineProvider.Object);

        var startInfo = new ProcessStartInfo
        {
            FileName = "cmd",
            Arguments = "/c ping 127.0.0.1 -n 10 > nul",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        await wrapper.StartAsync(startInfo);
        Assert.False(wrapper.HasExited);

        wrapper.Kill(entireProcessTree: true);
        Assert.True(wrapper.WaitForExit(5000));
    }

    [Fact]
    public async Task ProcessWrapper_Dispose_CanBeCalledMultipleTimes()
    {
        using var process = new Process();
        var commandLineProvider = new Mock<ICommandLineProvider>(MockBehavior.Strict);
        var wrapper = new ProcessWrapper(process, commandLineProvider.Object);

        await wrapper.StartAsync(new ProcessStartInfo
        {
            FileName = "cmd",
            Arguments = "/c exit 0",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            CreateNoWindow = true
        });
        await wrapper.WaitForExitAsync();

        wrapper.Dispose();
        wrapper.Dispose();
    }

    [Fact]
    public void ProcessWrapperFactory_Create_ReturnsProcessWrappers()
    {
        var commandLineProvider = new Mock<ICommandLineProvider>(MockBehavior.Strict);
        var factory = new ProcessWrapperFactory(commandLineProvider.Object);

        var wrapperFromDefault = factory.Create();
        using var process = new Process();
        var wrapperFromExisting = factory.Create(process);

        Assert.IsType<ProcessWrapper>(wrapperFromDefault);
        Assert.IsType<ProcessWrapper>(wrapperFromExisting);

        wrapperFromDefault.Dispose();
        wrapperFromExisting.Dispose();
    }

    [Fact]
    public void ProcessService_Methods_WrapProcessInstances()
    {
        var wrappedProcess = Mock.Of<IProcess>();
        var processWrapperFactory = new Mock<IProcessWrapperFactory>(MockBehavior.Strict);
        processWrapperFactory
            .Setup(factory => factory.Create(It.IsAny<Process>()))
            .Returns(wrappedProcess);

        var service = new ProcessService(processWrapperFactory.Object);

        var currentProcess = service.GetCurrentProcess();
        var byId = service.GetProcessById(Environment.ProcessId);
        var all = service.GetProcesses();
        var byName = service.GetProcessesByName("dotnet");

        Assert.Same(wrappedProcess, currentProcess);
        Assert.Same(wrappedProcess, byId);
        Assert.NotEmpty(all);
        Assert.NotNull(byName);
        processWrapperFactory.Verify(factory => factory.Create(It.IsAny<Process>()), Times.AtLeast(2));
    }
}

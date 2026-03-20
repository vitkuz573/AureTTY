using System.Diagnostics;
using AureTTY.Contracts.Enums;

namespace AureTTY.Execution.Services;

public static class ShellLaunchPlanner
{
    public static ProcessStartInfo BuildInteractiveShellStartInfo(Shell shell, string? workingDirectory)
    {
        var startInfo = shell switch
        {
            Shell.Cmd when OperatingSystem.IsWindows() => new ProcessStartInfo
            {
                FileName = "cmd"
            },
            Shell.PowerShell when OperatingSystem.IsWindows() => new ProcessStartInfo
            {
                FileName = "powershell"
            },
            Shell.Pwsh => new ProcessStartInfo
            {
                FileName = "pwsh"
            },
            Shell.Bash => new ProcessStartInfo
            {
                FileName = "bash"
            },
            Shell.Sh => new ProcessStartInfo
            {
                FileName = "sh"
            },
            Shell.Cmd => throw new InvalidOperationException("CMD is available only on Windows hosts."),
            Shell.PowerShell => throw new InvalidOperationException("Windows PowerShell is available only on Windows hosts."),
            _ => throw new InvalidOperationException($"Unsupported shell '{shell}'.")
        };

        if (shell == Shell.Cmd)
        {
            startInfo.ArgumentList.Add("/Q");
        }
        else if (shell == Shell.PowerShell)
        {
            startInfo.ArgumentList.Add("-NoLogo");
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
        }
        else if (shell == Shell.Pwsh)
        {
            startInfo.ArgumentList.Add("-NoLogo");
            startInfo.ArgumentList.Add("-NoProfile");
        }
        else if (shell == Shell.Bash)
        {
            startInfo.ArgumentList.Add("--noprofile");
            startInfo.ArgumentList.Add("--norc");
        }

        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.RedirectStandardInput = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;

        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            startInfo.WorkingDirectory = workingDirectory;
        }

        return startInfo;
    }
}

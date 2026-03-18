// Copyright © 2023-2026 Vitaly Kuzyaev. All rights reserved.
// This file is part of the AureTTY project.
// Licensed under the GNU Affero General Public License v3.0.

using System.ComponentModel;
using System.Runtime.InteropServices;
using AureTTY.Execution.Abstractions;
using Windows.Wdk.System.Threading;
using Windows.Win32.Foundation;
using Windows.Win32.System.Threading;
using static Windows.Wdk.PInvoke;
using static Windows.Win32.PInvoke;

namespace AureTTY.Windows.Services;

public class CommandLineProvider : ICommandLineProvider
{
    public async Task<string[]> GetCommandLineAsync(IProcess process)
    {
        ArgumentNullException.ThrowIfNull(process);

        if (process.HasExited)
        {
            return [];
        }

        using var processHandle = OpenProcess_SafeHandle(PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)process.Id);

        if (processHandle.IsInvalid)
        {
            return [];
        }

        uint bufferSize = 256;
        uint returnLength = 0;

        while (true)
        {
            var buffer = new byte[bufferSize];
            NTSTATUS status;

            unsafe
            {
                fixed (byte* bufferPtr = buffer)
                {
                    status = NtQueryInformationProcess((HANDLE)processHandle.DangerousGetHandle(), PROCESSINFOCLASS.ProcessCommandLineInformation, bufferPtr, bufferSize, ref returnLength);
                }
            }

            if (status.SeverityCode == NTSTATUS.Severity.Success)
            {
                if (returnLength < (uint)Marshal.SizeOf<UNICODE_STRING>())
                {
                    return [];
                }

                UNICODE_STRING unicodeString;

                unsafe
                {
                    fixed (byte* bufferPtr = buffer)
                    {
                        unicodeString = Marshal.PtrToStructure<UNICODE_STRING>((nint)bufferPtr);
                    }

                    if (unicodeString.Buffer.Value == null)
                    {
                        return [];
                    }
                }

                nint commandLinePtr;

                unsafe
                {
                    commandLinePtr = (nint)unicodeString.Buffer.Value;
                }

                var commandLine = Marshal.PtrToStringUni(commandLinePtr, unicodeString.Length / 2);
                var parsedArgs = ParseCommandLine(commandLine);

                return await Task.FromResult(parsedArgs);
            }

            if (status == NTSTATUS.STATUS_BUFFER_OVERFLOW || status == NTSTATUS.STATUS_INFO_LENGTH_MISMATCH)
            {
                // Some processes may report inconsistent buffer hints.
                // Keep growing conservatively to avoid an endless retry loop.
                bufferSize = returnLength > bufferSize ? returnLength : bufferSize * 2;
                continue;
            }

            // Command line inspection is best-effort. For short-lived or protected processes
            // (for example terminating processes, STATUS 0xC000010A), treat as unavailable.
            if (process.HasExited || IsTransientNtQueryFailure(status))
            {
                return [];
            }

            return [];
        }
    }

    private static unsafe string[] ParseCommandLine(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return [];
        }

        var argv = CommandLineToArgv(commandLine, out var argc);

        if (argv == null)
        {
            return [.. commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries)];
        }

        try
        {
            var args = new string[argc];

            for (var i = 0; i < argc; i++)
            {
                var arg = argv[i];

                args[i] = Marshal.PtrToStringUni((nint)arg.Value) ?? string.Empty;
            }

            return args;
        }
        finally
        {
            using var handle = LocalFree_SafeHandle(new HLOCAL((nint)argv));
        }
    }

    private static unsafe bool IsTransientNtQueryFailure(NTSTATUS status)
    {
        const uint statusProcessIsTerminating = 0xC000010A;
        const uint statusAccessDenied = 0xC0000022;
        const uint statusInvalidCid = 0xC000000B;
        const uint statusPartialCopy = 0x8000000D;

        var value = *(uint*)&status;

        return value is statusProcessIsTerminating
            or statusAccessDenied
            or statusInvalidCid
            or statusPartialCopy;
    }
}

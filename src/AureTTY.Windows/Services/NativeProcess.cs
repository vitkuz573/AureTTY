// Copyright © 2023-2026 Vitaly Kuzyaev. All rights reserved.
// This file is part of the AureTTY project.
// Licensed under the GNU Affero General Public License v3.0.

using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using AureTTY.Execution.Abstractions;
using AureTTY.Windows.Abstractions;
using AureTTY.Windows.Models;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security;
using Windows.Win32.System.Console;
using Windows.Win32.System.Threading;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.Shell;
using static Windows.Win32.PInvoke;

namespace AureTTY.Windows.Services;

public class NativeProcess : IProcess, IResizableTerminalProcess
{
    private static readonly Lock CreateProcessLock = new();
    private static readonly Lock PrivilegeInitializationLock = new();
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private const uint ProfileInfoNoUiFlag = 0x00000001;
    private const string UsEnglishKeyboardLayoutId = "00000409";
    private const ACTIVATE_KEYBOARD_LAYOUT_FLAGS ActivateKeyboardLayoutFlags =
        ACTIVATE_KEYBOARD_LAYOUT_FLAGS.KLF_ACTIVATE | ACTIVATE_KEYBOARD_LAYOUT_FLAGS.KLF_SETFORPROCESS;
    private static bool _processTokenPrivilegesInitialized;
    private static readonly string[] RequiredProcessPrivileges =
    [
        "SeTcbPrivilege",
        "SeAssignPrimaryTokenPrivilege",
        "SeIncreaseQuotaPrivilege"
    ];

    private readonly NativeProcessOptions _options;
    private readonly ISessionService _sessionService;
    private readonly ICommandLineProvider _commandLineProvider;
    private readonly IProcessService _processService;
    private readonly IFileSystem _fileSystem;

    private SafeProcessHandle? _processHandle;
    private ClosePseudoConsoleSafeHandle? _pseudoConsoleHandle;
    private SafeFileHandle? _loadedUserProfileTokenHandle;
    private HANDLE _loadedUserProfileHandle = HANDLE.Null;

    private IProcess? _attachedProcess;

    public int Id { get; private set; }

    public int ExitCode => _attachedProcess?.ExitCode ?? 0;

    public int SessionId => _attachedProcess?.SessionId ?? 0;

    public StreamWriter StandardInput { get; private set; } = null!;

    public StreamReader StandardOutput { get; private set; } = null!;

    public StreamReader StandardError { get; private set; } = null!;

    public ProcessModule? MainModule => _attachedProcess?.MainModule;

    public string ProcessName => _attachedProcess?.ProcessName ?? string.Empty;

    public long WorkingSet64 => _attachedProcess?.WorkingSet64 ?? 0;

    public DateTime StartTime => _attachedProcess?.StartTime ?? DateTime.MinValue;

    public bool HasExited
    {
        get
        {
            if (_processHandle == null || _processHandle.IsInvalid || _processHandle.IsClosed)
            {
                throw new InvalidOperationException("No process is associated with this NativeProcess object.");
            }

            if (GetExitCodeProcess(_processHandle, out var exitCode))
            {
                return exitCode != NTSTATUS.STILL_ACTIVE;
            }

            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    public NativeProcess(INativeProcessOptions options, ISessionService sessionService, ICommandLineProvider commandLineProvider, IProcessService processService, IFileSystem fileSystem)
    {
        if (options is not NativeProcessOptions nativeOptions)
        {
            throw new ArgumentException("Invalid options type. Expected NativeProcessOptions.", nameof(options));
        }

        _options = nativeOptions;
        _sessionService = sessionService;
        _commandLineProvider = commandLineProvider;
        _processService = processService;
        _fileSystem = fileSystem;
    }

    public Task StartAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureRequiredProcessPrivileges();

        var preferredSessionId = _options is { SessionId: not null, ForceConsoleSession: false }
            ? _sessionService.FindTargetSessionId(_options.SessionId.Value)
            : _sessionService.GetActiveConsoleSessionId();

        SafeFileHandle? hPrimaryToken = null;
        uint resolvedSessionId;

        try
        {
            hPrimaryToken = AcquirePrimaryToken(preferredSessionId, out resolvedSessionId);

            LoadUserProfileIfNeededCore(hPrimaryToken);

            _ = resolvedSessionId;
            if (_options.UsePseudoTerminal)
            {
                try
                {
                    StartWithPseudoConsoleCore(startInfo, hPrimaryToken);
                }
                catch (Exception ex) when (CanFallbackFromPseudoConsole(ex) && !_options.RequirePseudoTerminal)
                {
                    ResetPseudoConsoleState();
                    StartWithCreateProcessCore(startInfo, hPrimaryToken);
                }
                catch (Exception ex) when (CanFallbackFromPseudoConsole(ex) && _options.RequirePseudoTerminal)
                {
                    throw new InvalidOperationException("Failed to start terminal with pseudo console and fallback is disabled.", ex);
                }
            }
            else
            {
                StartWithCreateProcessCore(startInfo, hPrimaryToken);
            }
        }
        catch
        {
            UnloadUserProfileIfLoadedCore();
            throw;
        }
        finally
        {
            hPrimaryToken?.Dispose();
        }

        return Task.CompletedTask;
    }

    protected virtual SafeFileHandle AcquirePrimaryToken(uint preferredSessionId, out uint resolvedSessionId)
    {
        SafeFileHandle? hPrimaryToken = null;
        resolvedSessionId = preferredSessionId;

        if (HasExplicitCredentials(_options))
        {
            if (!TryGetPrimaryCredentialToken(_options, out hPrimaryToken))
            {
                throw new InvalidOperationException("Failed to acquire token for provided credentials.");
            }
        }
        else if (_options.UseCurrentUserToken)
        {
            if (!TryGetPrimaryUserToken(preferredSessionId, out hPrimaryToken, out resolvedSessionId))
            {
                throw new InvalidOperationException($"Failed to acquire active user token for session ID {preferredSessionId}.");
            }
        }
        else
        {
            if (!TryGetPrimaryProcessToken(preferredSessionId, "winlogon", out hPrimaryToken))
            {
                foreach (var candidateSessionId in GetActiveSessionCandidates(preferredSessionId))
                {
                    if (TryGetPrimaryProcessToken(candidateSessionId, "winlogon", out hPrimaryToken))
                    {
                        resolvedSessionId = candidateSessionId;
                        break;
                    }
                }

                if (hPrimaryToken == null)
                {
                    throw new InvalidOperationException($"Failed to acquire system token from winlogon for session ID {preferredSessionId}.");
                }
            }
        }

        if (hPrimaryToken == null || hPrimaryToken.IsInvalid || hPrimaryToken.IsClosed)
        {
            throw new InvalidOperationException("Failed to get or duplicate a primary token.");
        }

        return hPrimaryToken;
    }

    private static void EnsureRequiredProcessPrivileges()
    {
        lock (PrivilegeInitializationLock)
        {
            if (_processTokenPrivilegesInitialized)
            {
                return;
            }

            try
            {
                using var processHandle = GetCurrentProcess_SafeHandle();
                if (!OpenProcessToken(
                        processHandle,
                        TOKEN_ACCESS_MASK.TOKEN_ADJUST_PRIVILEGES | TOKEN_ACCESS_MASK.TOKEN_QUERY,
                        out var processToken))
                {
                    return;
                }

                using (processToken)
                {
                    foreach (var privilegeName in RequiredProcessPrivileges)
                    {
                        _ = TryEnablePrivilege(processToken, privilegeName);
                    }
                }
            }
            finally
            {
                _processTokenPrivilegesInitialized = true;
            }
        }
    }

    private static unsafe bool TryEnablePrivilege(SafeHandle tokenHandle, string privilegeName)
    {
        if (!LookupPrivilegeValue(null, privilegeName, out var luid))
        {
            return false;
        }

        var tokenPrivileges = new TOKEN_PRIVILEGES
        {
            PrivilegeCount = 1,
            Privileges =
            {
                e0 =
                {
                    Luid = luid,
                    Attributes = TOKEN_PRIVILEGES_ATTRIBUTES.SE_PRIVILEGE_ENABLED
                }
            }
        };

        return AdjustTokenPrivileges((HANDLE)tokenHandle.DangerousGetHandle(), false, &tokenPrivileges, (uint)sizeof(TOKEN_PRIVILEGES), null, null);
    }

    protected virtual void StartWithPseudoConsoleCore(ProcessStartInfo startInfo, SafeHandle hUserTokenDup)
    {
        TryActivateUsEnglishKeyboardLayout();
        StartWithPseudoConsole(startInfo, hUserTokenDup);
    }

    protected virtual void StartWithCreateProcessCore(ProcessStartInfo startInfo, SafeHandle hUserTokenDup)
    {
        StartWithCreateProcess(startInfo, hUserTokenDup);
    }

    protected virtual void LoadUserProfileIfNeededCore(SafeHandle primaryToken)
    {
        TryLoadUserProfileIfNeeded(primaryToken);
    }

    protected virtual void UnloadUserProfileIfLoadedCore()
    {
        UnloadUserProfileIfLoaded();
    }

    public void Kill(bool entireProcessTree = false)
    {
        if (entireProcessTree)
        {
            try
            {
                using var process = _processService.GetProcessById(Id);
                process.Kill(true);
                return;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to kill the process tree.", ex);
            }
        }

        if (_processHandle == null || _processHandle.IsInvalid || _processHandle.IsClosed)
        {
            throw new InvalidOperationException("No process is associated with this NativeProcess object.");
        }

        if (!TerminateProcess(_processHandle, 1))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    private static string EscapeArgument(string arg)
    {
        if (string.IsNullOrWhiteSpace(arg))
        {
            return "\"\"";
        }

        if (!arg.Contains(' ') && !arg.Contains('"'))
        {
            return arg;
        }

        var escaped = arg.Replace("\"", "\\\"");

        return $"\"{escaped}\"";
    }

    private void StartWithCreateProcess(ProcessStartInfo startInfo, SafeHandle hUserTokenDup)
    {
        STARTUPINFOW startupInfo = default;
        PROCESS_INFORMATION processInfo = default;
        SECURITY_ATTRIBUTES securityAttributes = default;
#pragma warning disable CA2000
        var procSH = new SafeProcessHandle();
#pragma warning restore CA2000

        SafeFileHandle? parentInputPipeHandle = null;
        SafeFileHandle? childInputPipeHandle = null;
        SafeFileHandle? parentOutputPipeHandle = null;
        SafeFileHandle? childOutputPipeHandle = null;
        SafeFileHandle? parentErrorPipeHandle = null;
        SafeFileHandle? childErrorPipeHandle = null;
        var userEnvironmentBlock = IntPtr.Zero;

        using (CreateProcessLock.EnterScope())
        {
            try
            {
                startupInfo.cb = (uint)Marshal.SizeOf<STARTUPINFOW>();

                if (startInfo.RedirectStandardInput || startInfo.RedirectStandardOutput || startInfo.RedirectStandardError)
                {
                    if (startInfo.RedirectStandardInput)
                    {
#pragma warning disable CA2000
                        CreatePipe(out parentInputPipeHandle, out childInputPipeHandle, true);
#pragma warning restore CA2000
                    }
                    else
                    {
                        childInputPipeHandle = GetStdHandle_SafeHandle(STD_HANDLE.STD_INPUT_HANDLE);
                    }

                    if (startInfo.RedirectStandardOutput)
                    {
#pragma warning disable CA2000
                        CreatePipe(out parentOutputPipeHandle, out childOutputPipeHandle, false);
#pragma warning restore CA2000
                    }
                    else
                    {
                        childOutputPipeHandle = GetStdHandle_SafeHandle(STD_HANDLE.STD_OUTPUT_HANDLE);
                    }

                    if (startInfo.RedirectStandardError)
                    {
#pragma warning disable CA2000
                        CreatePipe(out parentErrorPipeHandle, out childErrorPipeHandle, false);
#pragma warning restore CA2000
                    }
                    else
                    {
                        childErrorPipeHandle = GetStdHandle_SafeHandle(STD_HANDLE.STD_ERROR_HANDLE);
                    }

                    startupInfo.hStdInput = (HANDLE)childInputPipeHandle.DangerousGetHandle();
                    startupInfo.hStdOutput = (HANDLE)childOutputPipeHandle.DangerousGetHandle();
                    startupInfo.hStdError = (HANDLE)childErrorPipeHandle.DangerousGetHandle();

                    startupInfo.dwFlags = STARTUPINFOW_FLAGS.STARTF_USESTDHANDLES;
                }

                PROCESS_CREATION_FLAGS dwCreationFlags = 0;

                dwCreationFlags |= startInfo.CreateNoWindow
                    ? PROCESS_CREATION_FLAGS.CREATE_NO_WINDOW
                    : PROCESS_CREATION_FLAGS.CREATE_NEW_CONSOLE;

                dwCreationFlags |= PROCESS_CREATION_FLAGS.CREATE_UNICODE_ENVIRONMENT;
                var desktopName = $@"winsta0\{_options.DesktopName}";
                userEnvironmentBlock = CreateEnvironmentBlockForToken(hUserTokenDup);

                string fullCommand;

                if (startInfo is { Arguments: not null, ArgumentList.Count: > 0 })
                {
                    var escapedArgs = startInfo.ArgumentList.Select(EscapeArgument);
                    fullCommand = $"{startInfo.FileName} {string.Join(" ", escapedArgs)}";
                }
                else
                {
                    fullCommand = $"{startInfo.FileName} {startInfo.Arguments}";
                }

                fullCommand += char.MinValue;

                var commandSpan = new Span<char>(fullCommand.ToCharArray());

                bool retVal;
                var errorCode = 0;

                unsafe
                {
                    fixed (char* pDesktopName = desktopName)
                    {
                        startupInfo.lpDesktop = pDesktopName;
                        retVal = CreateProcessAsUser(hUserTokenDup, null, ref commandSpan, securityAttributes, securityAttributes, true, dwCreationFlags, (char*)userEnvironmentBlock, null, startupInfo, out processInfo);
                    }
                }

                if (!retVal)
                {
                    errorCode = Marshal.GetLastWin32Error();
                }

                if (processInfo.hProcess != nint.Zero && processInfo.hProcess != new nint(-1))
                {
                    Marshal.InitHandle(procSH, processInfo.hProcess);
                }

                if (processInfo.hThread != nint.Zero && processInfo.hThread != new nint(-1))
                {
                    CloseHandle(processInfo.hThread);
                }
            }
            catch
            {
                parentInputPipeHandle?.Dispose();
                parentOutputPipeHandle?.Dispose();
                parentErrorPipeHandle?.Dispose();
                procSH.Dispose();
                throw;
            }
            finally
            {
                DestroyEnvironmentBlockSafe(userEnvironmentBlock);
                childInputPipeHandle?.Dispose();
                childOutputPipeHandle?.Dispose();
                childErrorPipeHandle?.Dispose();
            }
        }

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        if (startInfo.RedirectStandardInput)
        {
            var enc = startInfo.StandardInputEncoding ?? Encoding.GetEncoding((int)GetConsoleCP());

            StandardInput = new StreamWriter(_fileSystem.FileStream.New(parentInputPipeHandle!, FileAccess.Write, 4096, false), enc, 4096)
            {
                AutoFlush = true
            };
        }

        if (startInfo.RedirectStandardOutput)
        {
            var enc = startInfo.StandardOutputEncoding ?? Encoding.GetEncoding((int)GetConsoleOutputCP());

            StandardOutput = new StreamReader(_fileSystem.FileStream.New(parentOutputPipeHandle!, FileAccess.Read, 4096, false), enc, true, 4096);
        }

        if (startInfo.RedirectStandardError)
        {
            var enc = startInfo.StandardErrorEncoding ?? Encoding.GetEncoding((int)GetConsoleOutputCP());

            StandardError = new StreamReader(_fileSystem.FileStream.New(parentErrorPipeHandle!, FileAccess.Read, 4096, false), enc, true, 4096);
        }

        if (procSH.IsInvalid)
        {
            procSH.Dispose();
            throw new Win32Exception("Failed to create process with CreateProcessAsUser.");
        }

        _processHandle = new SafeProcessHandle(processInfo.hProcess, true);
        SetProcessId((int)processInfo.dwProcessId);

        try
        {
            _attachedProcess = _processService.GetProcessById(Id);
        }
        catch
        {
            _attachedProcess = null;
        }
    }

    private unsafe void StartWithPseudoConsole(ProcessStartInfo startInfo, SafeHandle hUserTokenDup)
    {
        PROCESS_INFORMATION processInfo = default;
        SafeFileHandle? hostInputHandle = null;
        SafeFileHandle? pseudoInputHandle = null;
        SafeFileHandle? hostOutputHandle = null;
        SafeFileHandle? pseudoOutputHandle = null;
        StreamWriter? standardInput = null;
        StreamReader? standardOutput = null;
        StreamReader? standardError = null;
        IntPtr attributeListBuffer = IntPtr.Zero;
        LPPROC_THREAD_ATTRIBUTE_LIST attributeList = default;
        STARTUPINFOEXW startupInfoEx = default;
        ClosePseudoConsoleSafeHandle? pseudoConsoleHandle = null;
        var userEnvironmentBlock = IntPtr.Zero;
        var started = false;

        using (CreateProcessLock.EnterScope())
        {
            try
            {
                CreatePipe(out hostInputHandle, out pseudoInputHandle, true);
                CreatePipe(out hostOutputHandle, out pseudoOutputHandle, false);

                var columns = ClampPseudoTerminalDimension(_options.PseudoTerminalColumns, fallback: 120);
                var rows = ClampPseudoTerminalDimension(_options.PseudoTerminalRows, fallback: 40);
                var initialSize = new COORD
                {
                    X = (short)columns,
                    Y = (short)rows
                };

                var hResult = CreatePseudoConsole(
                    initialSize,
                    pseudoInputHandle,
                    pseudoOutputHandle,
                    0,
                    out pseudoConsoleHandle);
                hResult.ThrowOnFailure();

                _pseudoConsoleHandle = pseudoConsoleHandle;
                pseudoConsoleHandle = null;

                pseudoInputHandle.Dispose();
                pseudoInputHandle = null;
                pseudoOutputHandle.Dispose();
                pseudoOutputHandle = null;

                nuint attributeListSize = 0;
                _ = InitializeProcThreadAttributeList(attributeList, 1, 0u, &attributeListSize);
                attributeListBuffer = Marshal.AllocHGlobal((nint)attributeListSize);
                attributeList = (LPPROC_THREAD_ATTRIBUTE_LIST)attributeListBuffer;

                if (!InitializeProcThreadAttributeList(attributeList, 1, 0u, &attributeListSize))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                var pseudoConsoleValue = _pseudoConsoleHandle.DangerousGetHandle();
                if (!UpdateProcThreadAttribute(
                        attributeList,
                        0,
                        PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE,
                        (void*)pseudoConsoleValue,
                        (nuint)IntPtr.Size,
                        null,
                        null))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                startupInfoEx = new STARTUPINFOEXW
                {
                    StartupInfo =
                    {
                        cb = (uint)Marshal.SizeOf<STARTUPINFOEXW>()
                    },
                    lpAttributeList = attributeList
                };

                var commandLine = BuildCommandLine(startInfo);
                var desktopName = $@"winsta0\{_options.DesktopName}";
                userEnvironmentBlock = CreateEnvironmentBlockForToken(hUserTokenDup);
                var creationFlags = PROCESS_CREATION_FLAGS.CREATE_UNICODE_ENVIRONMENT |
                                    PROCESS_CREATION_FLAGS.EXTENDED_STARTUPINFO_PRESENT;

                fixed (char* pCommandLine = commandLine)
                fixed (char* pDesktopName = desktopName)
                {
                    var commandLineSpan = new Span<char>(pCommandLine, commandLine.Length);
                    ref var startupInfo = ref Unsafe.As<STARTUPINFOEXW, STARTUPINFOW>(ref startupInfoEx);
                    startupInfo.lpDesktop = pDesktopName;

                    var retVal = CreateProcessAsUser(
                        hUserTokenDup,
                        null,
                        ref commandLineSpan,
                        null,
                        null,
                        false,
                        creationFlags,
                        (char*)userEnvironmentBlock,
                        string.IsNullOrWhiteSpace(startInfo.WorkingDirectory) ? null : startInfo.WorkingDirectory,
                        startupInfo,
                        out processInfo);
                    if (!retVal)
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    }
                }

                if (processInfo.hThread != nint.Zero && processInfo.hThread != new nint(-1))
                {
                    CloseHandle(processInfo.hThread);
                    processInfo.hThread = default;
                }

                _processHandle = new SafeProcessHandle(processInfo.hProcess, true);
                processInfo.hProcess = default;
                SetProcessId((int)processInfo.dwProcessId);

                try
                {
                    _attachedProcess = _processService.GetProcessById(Id);
                }
                catch
                {
                    _attachedProcess = null;
                }

                standardInput = new StreamWriter(_fileSystem.FileStream.New(hostInputHandle!, FileAccess.Write, 4096, false), Utf8NoBom, 4096)
                {
                    AutoFlush = true
                };
                hostInputHandle = null;

                standardOutput = new StreamReader(_fileSystem.FileStream.New(hostOutputHandle!, FileAccess.Read, 4096, false), Utf8NoBom, true, 4096);
                hostOutputHandle = null;
                standardError = new StreamReader(Stream.Null);

                StandardInput = standardInput;
                standardInput = null;
                StandardOutput = standardOutput;
                standardOutput = null;
                StandardError = standardError;
                standardError = null;
                started = true;
            }
            finally
            {
                standardInput?.Dispose();
                standardOutput?.Dispose();
                standardError?.Dispose();

                hostInputHandle?.Dispose();
                pseudoInputHandle?.Dispose();
                hostOutputHandle?.Dispose();
                pseudoOutputHandle?.Dispose();

                if (attributeListBuffer != IntPtr.Zero)
                {
                    DeleteProcThreadAttributeList(attributeList);
                    Marshal.FreeHGlobal(attributeListBuffer);
                }

                DestroyEnvironmentBlockSafe(userEnvironmentBlock);

                if (processInfo.hThread != nint.Zero && processInfo.hThread != new nint(-1))
                {
                    CloseHandle(processInfo.hThread);
                }

                if (!started)
                {
                    if (processInfo.hProcess != nint.Zero && processInfo.hProcess != new nint(-1))
                    {
                        CloseHandle(processInfo.hProcess);
                    }

                    _processHandle?.Dispose();
                    _processHandle = null;

                    _pseudoConsoleHandle?.Dispose();
                    _pseudoConsoleHandle = null;
                    pseudoConsoleHandle?.Dispose();
                }
            }
        }
    }

    private void SetProcessId(int processId)
    {
        Id = processId;
    }

    private static void CreatePipeWithSecurityAttributes(out SafeFileHandle hReadPipe, out SafeFileHandle hWritePipe, ref SECURITY_ATTRIBUTES lpPipeAttributes, uint nSize)
    {
        var ret = PInvoke.CreatePipe(out hReadPipe, out hWritePipe, lpPipeAttributes, nSize);

        if (!ret || hReadPipe.IsInvalid || hWritePipe.IsInvalid)
        {
            throw new Win32Exception();
        }
    }

    private static void CreatePipe(out SafeFileHandle parentHandle, out SafeFileHandle childHandle, bool parentInputs)
    {
        SECURITY_ATTRIBUTES securityAttributesParent = default;
        securityAttributesParent.bInheritHandle = true;

        SafeFileHandle? hTmp = null;

        try
        {
            if (parentInputs)
            {
                CreatePipeWithSecurityAttributes(out childHandle, out hTmp, ref securityAttributesParent, 0);
            }
            else
            {
                CreatePipeWithSecurityAttributes(out hTmp, out childHandle, ref securityAttributesParent, 0);
            }

            using var currentProcHandle = GetCurrentProcess_SafeHandle();

            if (!DuplicateHandle(currentProcHandle, hTmp, currentProcHandle, out parentHandle, 0, false, DUPLICATE_HANDLE_OPTIONS.DUPLICATE_SAME_ACCESS))
            {
                throw new Win32Exception();
            }
        }
        finally
        {
            hTmp?.Dispose();
        }
    }

    private static IntPtr CreateEnvironmentBlockForToken(SafeHandle userToken)
    {
        unsafe
        {
            void* environmentBlock = null;
            if (!CreateEnvironmentBlock(&environmentBlock, (HANDLE)userToken.DangerousGetHandle(), false) ||
                environmentBlock is null)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            return (IntPtr)environmentBlock;
        }
    }

    private static void DestroyEnvironmentBlockSafe(IntPtr environmentBlock)
    {
        if (environmentBlock == IntPtr.Zero)
        {
            return;
        }

        unsafe
        {
            _ = DestroyEnvironmentBlock((void*)environmentBlock);
        }
    }

    private static string BuildCommandLine(ProcessStartInfo startInfo)
    {
        if (startInfo is { ArgumentList.Count: > 0 })
        {
            var escapedArgs = startInfo.ArgumentList.Select(EscapeArgument);
            return $"{startInfo.FileName} {string.Join(" ", escapedArgs)}\0";
        }

        return $"{startInfo.FileName} {startInfo.Arguments}\0";
    }

    private static bool TryGetUserToken(uint sessionId, out SafeFileHandle hUserToken)
    {
        var userTokenHandle = default(HANDLE);
        var success = WTSQueryUserToken(sessionId, ref userTokenHandle);
        hUserToken = new SafeFileHandle(userTokenHandle, true);

        return success;
    }

    private bool TryGetPrimaryUserToken(uint preferredSessionId, out SafeFileHandle hPrimaryToken, out uint resolvedSessionId)
    {
        resolvedSessionId = preferredSessionId;
        if (TryGetPrimaryUserTokenForSession(preferredSessionId, out hPrimaryToken))
        {
            return true;
        }

        foreach (var candidateSessionId in GetActiveSessionCandidates(preferredSessionId))
        {
            if (!TryGetPrimaryUserTokenForSession(candidateSessionId, out hPrimaryToken))
            {
                continue;
            }

            resolvedSessionId = candidateSessionId;
            return true;
        }

        hPrimaryToken = null!;
        return false;
    }

    private bool TryGetPrimaryUserTokenForSession(uint sessionId, out SafeFileHandle hPrimaryToken)
    {
        hPrimaryToken = null!;
        SafeFileHandle? userToken = null;
        try
        {
            if (TryGetUserToken(sessionId, out var acquiredToken))
            {
                userToken = acquiredToken;

                if (!userToken.IsInvalid
                    && !userToken.IsClosed
                    && TryDuplicatePrimaryToken(userToken, out hPrimaryToken))
                {
                    return true;
                }
            }
        }
        finally
        {
            userToken?.Dispose();
        }

        return TryGetPrimaryProcessToken(sessionId, "explorer", out hPrimaryToken);
    }

    private IEnumerable<uint> GetActiveSessionCandidates(uint preferredSessionId)
    {
        return _sessionService.GetActiveSessions()
            .Select(static session => session.SessionId)
            .Where(sessionId => sessionId != preferredSessionId)
            .Distinct();
    }

    private bool TryGetPrimaryProcessToken(uint sessionId, string processName, out SafeFileHandle hPrimaryToken)
    {
        hPrimaryToken = null!;

        uint processId;
        try
        {
            processId = _sessionService.GetProcessId(sessionId, processName);
        }
        catch
        {
            return false;
        }

        using var processHandle = OpenProcess_SafeHandle(
            PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_INFORMATION | PROCESS_ACCESS_RIGHTS.PROCESS_DUP_HANDLE,
            false,
            processId);

        if (processHandle.IsInvalid || processHandle.IsClosed)
        {
            return false;
        }

        if (!OpenProcessToken(processHandle, TOKEN_ACCESS_MASK.TOKEN_DUPLICATE | TOKEN_ACCESS_MASK.TOKEN_QUERY, out var processToken))
        {
            return false;
        }

        using (processToken)
        {
            return TryDuplicatePrimaryToken(processToken, out hPrimaryToken);
        }
    }

    private static bool TryGetPrimaryCredentialToken(NativeProcessOptions options, out SafeFileHandle hPrimaryToken)
    {
        hPrimaryToken = null!;

        var userName = options.UserName?.Trim();
        if (string.IsNullOrWhiteSpace(userName))
        {
            return false;
        }

        var domain = string.IsNullOrWhiteSpace(options.Domain) ? "." : options.Domain.Trim();
        var password = options.Password ?? string.Empty;

        SafeFileHandle? userToken = null;
        try
        {
            if (!LogonUser(
                    userName,
                    domain,
                    password,
                    LOGON32_LOGON.LOGON32_LOGON_INTERACTIVE,
                    LOGON32_PROVIDER.LOGON32_PROVIDER_DEFAULT,
                    out var token))
            {
                return false;
            }

            userToken = token;
            return TryDuplicatePrimaryToken(userToken, out hPrimaryToken);
        }
        finally
        {
            userToken?.Dispose();
        }
    }

    private static bool HasExplicitCredentials(NativeProcessOptions options)
    {
        return !string.IsNullOrWhiteSpace(options.UserName);
    }

    private static int ClampPseudoTerminalDimension(int? value, int fallback)
    {
        return Math.Clamp(value ?? fallback, 1, short.MaxValue);
    }

    private static bool CanFallbackFromPseudoConsole(Exception exception)
    {
        return exception is Win32Exception
               or COMException
               or InvalidOperationException;
    }

    private void ResetPseudoConsoleState()
    {
        _pseudoConsoleHandle?.Dispose();
        _pseudoConsoleHandle = null;
    }

    private void TryLoadUserProfileIfNeeded(SafeHandle primaryToken)
    {
        if (!_options.LoadUserProfile || !HasExplicitCredentials(_options))
        {
            return;
        }

        var userName = _options.UserName?.Trim();
        if (string.IsNullOrWhiteSpace(userName))
        {
            return;
        }

        if (!TryDuplicatePrimaryToken(primaryToken, out var profileToken))
        {
            throw new InvalidOperationException("Failed to duplicate token for profile loading.");
        }

        var profileInfo = new PROFILEINFOW
        {
            dwSize = (uint)Marshal.SizeOf<PROFILEINFOW>(),
            dwFlags = ProfileInfoNoUiFlag
        };

        unsafe
        {
            fixed (char* pUserName = userName)
            {
                profileInfo.lpUserName = pUserName;

                if (!LoadUserProfile(profileToken, ref profileInfo))
                {
                    profileToken.Dispose();
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
        }

        if (profileInfo.hProfile == HANDLE.Null)
        {
            profileToken.Dispose();
            throw new InvalidOperationException("Profile load returned an empty profile handle.");
        }

        _loadedUserProfileTokenHandle = profileToken;
        _loadedUserProfileHandle = profileInfo.hProfile;
    }

    private void UnloadUserProfileIfLoaded()
    {
        var tokenHandle = _loadedUserProfileTokenHandle;
        var profileHandle = _loadedUserProfileHandle;

        _loadedUserProfileTokenHandle = null;
        _loadedUserProfileHandle = HANDLE.Null;

        if (tokenHandle is null || tokenHandle.IsInvalid || tokenHandle.IsClosed || profileHandle == HANDLE.Null)
        {
            tokenHandle?.Dispose();
            return;
        }

        try
        {
            _ = UnloadUserProfile((HANDLE)tokenHandle.DangerousGetHandle(), profileHandle);
        }
        finally
        {
            tokenHandle.Dispose();
        }
    }

    private static bool TryDuplicatePrimaryToken(SafeHandle sourceToken, out SafeFileHandle hPrimaryToken)
    {
        hPrimaryToken = null!;
        return DuplicateTokenEx(
            sourceToken,
            TOKEN_ACCESS_MASK.TOKEN_DUPLICATE
            | TOKEN_ACCESS_MASK.TOKEN_QUERY
            | TOKEN_ACCESS_MASK.TOKEN_ASSIGN_PRIMARY
            | TOKEN_ACCESS_MASK.TOKEN_IMPERSONATE
            | TOKEN_ACCESS_MASK.TOKEN_ADJUST_DEFAULT
            | TOKEN_ACCESS_MASK.TOKEN_ADJUST_SESSIONID,
            null,
            SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation,
            TOKEN_TYPE.TokenPrimary,
            out hPrimaryToken);
    }

    public async Task<string[]> GetCommandLineAsync()
    {
        return await _commandLineProvider.GetCommandLineAsync(this);
    }

    public bool WaitForExit(uint millisecondsTimeout = uint.MaxValue)
    {
        if (_processHandle == null || _processHandle.IsInvalid || _processHandle.IsClosed)
        {
            throw new InvalidOperationException("No process is associated with this NativeProcess object.");
        }

        var result = WaitForSingleObject(_processHandle, millisecondsTimeout);

        return result == WAIT_EVENT.WAIT_OBJECT_0;
    }

    public Task WaitForExitAsync(CancellationToken cancellationToken = default)
    {
        if (_attachedProcess == null)
        {
            throw new InvalidOperationException("No process is associated with this NativeProcess object.");
        }

        return _attachedProcess.WaitForExitAsync(cancellationToken);
    }

    public Task ResizeTerminalAsync(int columns, int rows, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        if (!TryGetPseudoConsoleHandleForResize(out var pseudoConsoleHandle))
        {
            return Task.CompletedTask;
        }

        var size = new COORD
        {
            X = (short)ClampPseudoTerminalDimension(columns, columns),
            Y = (short)ClampPseudoTerminalDimension(rows, rows)
        };

        var hResult = ResizePseudoConsoleCore(pseudoConsoleHandle, size);
        hResult.ThrowOnFailure();
        return Task.CompletedTask;
    }

    protected virtual bool TryGetPseudoConsoleHandleForResize([NotNullWhen(true)] out ClosePseudoConsoleSafeHandle? pseudoConsoleHandle)
    {
        pseudoConsoleHandle = _pseudoConsoleHandle;
        return pseudoConsoleHandle is not null
               && !pseudoConsoleHandle.IsInvalid
               && !pseudoConsoleHandle.IsClosed;
    }

    protected virtual HRESULT ResizePseudoConsoleCore(ClosePseudoConsoleSafeHandle pseudoConsoleHandle, COORD size)
    {
        return ResizePseudoConsole(pseudoConsoleHandle, size);
    }

    private static unsafe void TryActivateUsEnglishKeyboardLayout()
    {
        fixed (char* keyboardLayoutId = UsEnglishKeyboardLayoutId)
        {
            var keyboardLayout = LoadKeyboardLayout((PCWSTR)keyboardLayoutId, ActivateKeyboardLayoutFlags);
            if (keyboardLayout.IsNull)
            {
                return;
            }

            _ = ActivateKeyboardLayout(keyboardLayout, default);
        }
    }

    public void Dispose()
    {
        UnloadUserProfileIfLoadedCore();
        _pseudoConsoleHandle?.Dispose();
        _pseudoConsoleHandle = null;
        StandardOutput?.Dispose();
        StandardError?.Dispose();
        StandardInput?.Dispose();
        _processHandle?.Close();
    }
}

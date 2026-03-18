// Copyright © 2023-2026 Vitaly Kuzyaev. All rights reserved.
// This file is part of the AureTTY project.
// Licensed under the GNU Affero General Public License v3.0.

namespace AureTTY.Protocol;

public static class TerminalIpcMethods
{
    public const string Hello = "hello";
    public const string Ping = "ping";
    public const string Start = "terminal.start";
    public const string Resume = "terminal.resume";
    public const string SendInput = "terminal.input";
    public const string GetInputDiagnostics = "terminal.inputDiagnostics";
    public const string Resize = "terminal.resize";
    public const string Signal = "terminal.signal";
    public const string Close = "terminal.close";
    public const string CloseViewerSessions = "terminal.closeViewerSessions";
    public const string CloseAllSessions = "terminal.closeAllSessions";
    public const string SessionEvent = "terminal.event";
}

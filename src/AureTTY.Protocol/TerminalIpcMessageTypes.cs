// Copyright © 2023-2026 Vitaly Kuzyaev. All rights reserved.
// This file is part of the AureTTY project.
// Licensed under the GNU Affero General Public License v3.0.

namespace AureTTY.Protocol;

public static class TerminalIpcMessageTypes
{
    public const string Request = "request";
    public const string Response = "response";
    public const string Error = "error";
    public const string Event = "event";
    public const string Hello = "hello";
}

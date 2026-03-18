using AureTTY.Contracts.DTOs;
using AureTTY.Contracts.Enums;

namespace AureTTY.Protocol;

public sealed record TerminalIpcHelloPayload(string Token, int ProtocolVersion = 1);

public sealed record TerminalIpcStartRequest(string ViewerId, TerminalSessionStartRequest Request);

public sealed record TerminalIpcResumeRequest(string ViewerId, TerminalSessionResumeRequest Request);

public sealed record TerminalIpcInputRequest(string ViewerId, TerminalSessionInputRequest Request);

public sealed record TerminalIpcInputDiagnosticsRequest(string ViewerId, string SessionId);

public sealed record TerminalIpcResizeRequest(string ViewerId, TerminalSessionResizeRequest Request);

public sealed record TerminalIpcSignalRequest(string ViewerId, string SessionId, TerminalSessionSignal Signal);

public sealed record TerminalIpcCloseRequest(string ViewerId, string SessionId);

public sealed record TerminalIpcCloseViewerSessionsRequest(string ViewerId);

public sealed record TerminalIpcSessionEvent(string ViewerId, TerminalSessionEvent Event);

public sealed record TerminalIpcAck(bool Success = true);

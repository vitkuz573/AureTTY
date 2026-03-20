using AureTTY.Contracts.DTOs;
using AureTTY.Contracts.Enums;
using MessagePack;

namespace AureTTY.Protocol;

[MessagePackObject]
public sealed record TerminalIpcHelloPayload([property: Key(0)] string Token, [property: Key(1)] int ProtocolVersion = 1);

[MessagePackObject]
public sealed record TerminalIpcStartRequest([property: Key(0)] string ViewerId, [property: Key(1)] TerminalSessionStartRequest Request);

[MessagePackObject]
public sealed record TerminalIpcResumeRequest([property: Key(0)] string ViewerId, [property: Key(1)] TerminalSessionResumeRequest Request);

[MessagePackObject]
public sealed record TerminalIpcInputRequest([property: Key(0)] string ViewerId, [property: Key(1)] TerminalSessionInputRequest Request);

[MessagePackObject]
public sealed record TerminalIpcInputDiagnosticsRequest([property: Key(0)] string ViewerId, [property: Key(1)] string SessionId);

[MessagePackObject]
public sealed record TerminalIpcResizeRequest([property: Key(0)] string ViewerId, [property: Key(1)] TerminalSessionResizeRequest Request);

[MessagePackObject]
public sealed record TerminalIpcSignalRequest([property: Key(0)] string ViewerId, [property: Key(1)] string SessionId, [property: Key(2)] TerminalSessionSignal Signal);

[MessagePackObject]
public sealed record TerminalIpcCloseRequest([property: Key(0)] string ViewerId, [property: Key(1)] string SessionId);

[MessagePackObject]
public sealed record TerminalIpcCloseViewerSessionsRequest([property: Key(0)] string ViewerId);

[MessagePackObject]
public sealed record TerminalIpcSessionEvent([property: Key(0)] string ViewerId, [property: Key(1)] TerminalSessionEvent Event);

[MessagePackObject]
public sealed record TerminalIpcAck([property: Key(0)] bool Success = true);

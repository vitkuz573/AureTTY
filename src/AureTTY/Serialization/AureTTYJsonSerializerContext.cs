using System.Text.Json;
using System.Text.Json.Serialization;
using AureTTY.Api.Models;
using AureTTY.Contracts.DTOs;
using AureTTY.Protocol;

namespace AureTTY.Serialization;

[JsonSourceGenerationOptions(
    JsonSerializerDefaults.Web,
    Converters = [typeof(TerminalIpcPayloadConverter)])]
[JsonSerializable(typeof(ApiErrorResponse))]
[JsonSerializable(typeof(ApiProblemResponse))]
[JsonSerializable(typeof(AttachTerminalSessionRequest))]
[JsonSerializable(typeof(CreateTerminalInputRequest))]
[JsonSerializable(typeof(CreateTerminalSessionRequest))]
[JsonSerializable(typeof(CreateTerminalSignalRequest))]
[JsonSerializable(typeof(TerminalHealthResponse))]
[JsonSerializable(typeof(UpdateTerminalSizeRequest))]
[JsonSerializable(typeof(TerminalSessionHandle))]
[JsonSerializable(typeof(TerminalSessionHandle[]))]
[JsonSerializable(typeof(TerminalSessionInputDiagnostics))]
[JsonSerializable(typeof(TerminalSessionEvent))]
[JsonSerializable(typeof(TerminalIpcAck))]
[JsonSerializable(typeof(TerminalIpcCloseRequest))]
[JsonSerializable(typeof(TerminalIpcCloseViewerSessionsRequest))]
[JsonSerializable(typeof(TerminalIpcHelloPayload))]
[JsonSerializable(typeof(TerminalIpcInputDiagnosticsRequest))]
[JsonSerializable(typeof(TerminalIpcInputRequest))]
[JsonSerializable(typeof(TerminalIpcMessage))]
[JsonSerializable(typeof(TerminalIpcResizeRequest))]
[JsonSerializable(typeof(TerminalIpcResumeRequest))]
[JsonSerializable(typeof(TerminalIpcSessionEvent))]
[JsonSerializable(typeof(TerminalIpcSignalRequest))]
[JsonSerializable(typeof(TerminalIpcStartRequest))]
public partial class AureTTYJsonSerializerContext : JsonSerializerContext;

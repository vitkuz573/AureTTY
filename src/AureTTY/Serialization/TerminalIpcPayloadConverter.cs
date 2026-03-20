using System.Text.Json;
using System.Text.Json.Serialization;
using AureTTY.Contracts.DTOs;
using AureTTY.Protocol;

namespace AureTTY.Serialization;

public sealed class TerminalIpcPayloadConverter : JsonConverter<object?>
{
    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return JsonElement.ParseValue(ref reader);
    }

    public override void Write(Utf8JsonWriter writer, object? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        if (value is JsonElement jsonElement)
        {
            jsonElement.WriteTo(writer);
            return;
        }

        switch (value)
        {
            case TerminalIpcAck payload:
                JsonSerializer.Serialize(writer, payload, AureTTYJsonSerializerContext.Default.TerminalIpcAck);
                return;
            case TerminalIpcCloseRequest payload:
                JsonSerializer.Serialize(writer, payload, AureTTYJsonSerializerContext.Default.TerminalIpcCloseRequest);
                return;
            case TerminalIpcCloseViewerSessionsRequest payload:
                JsonSerializer.Serialize(writer, payload, AureTTYJsonSerializerContext.Default.TerminalIpcCloseViewerSessionsRequest);
                return;
            case TerminalIpcHelloPayload payload:
                JsonSerializer.Serialize(writer, payload, AureTTYJsonSerializerContext.Default.TerminalIpcHelloPayload);
                return;
            case TerminalIpcInputDiagnosticsRequest payload:
                JsonSerializer.Serialize(writer, payload, AureTTYJsonSerializerContext.Default.TerminalIpcInputDiagnosticsRequest);
                return;
            case TerminalIpcInputRequest payload:
                JsonSerializer.Serialize(writer, payload, AureTTYJsonSerializerContext.Default.TerminalIpcInputRequest);
                return;
            case TerminalIpcResizeRequest payload:
                JsonSerializer.Serialize(writer, payload, AureTTYJsonSerializerContext.Default.TerminalIpcResizeRequest);
                return;
            case TerminalIpcResumeRequest payload:
                JsonSerializer.Serialize(writer, payload, AureTTYJsonSerializerContext.Default.TerminalIpcResumeRequest);
                return;
            case TerminalIpcSessionEvent payload:
                JsonSerializer.Serialize(writer, payload, AureTTYJsonSerializerContext.Default.TerminalIpcSessionEvent);
                return;
            case TerminalIpcSignalRequest payload:
                JsonSerializer.Serialize(writer, payload, AureTTYJsonSerializerContext.Default.TerminalIpcSignalRequest);
                return;
            case TerminalIpcStartRequest payload:
                JsonSerializer.Serialize(writer, payload, AureTTYJsonSerializerContext.Default.TerminalIpcStartRequest);
                return;
            case TerminalSessionHandle payload:
                JsonSerializer.Serialize(writer, payload, AureTTYJsonSerializerContext.Default.TerminalSessionHandle);
                return;
            case TerminalSessionInputDiagnostics payload:
                JsonSerializer.Serialize(writer, payload, AureTTYJsonSerializerContext.Default.TerminalSessionInputDiagnostics);
                return;
        }

        throw new JsonException(
            $"Unsupported payload type '{value.GetType().FullName}'. " +
            "Only known AureTTY IPC payload types are supported in AOT mode.");
    }
}

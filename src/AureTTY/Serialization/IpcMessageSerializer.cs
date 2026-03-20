using System.Buffers;
using System.Text.Json;
using AureTTY.Protocol;
using MessagePack;

namespace AureTTY.Serialization;

public static class IpcMessageSerializer
{
    public static byte[] Serialize(TerminalIpcMessage message, IpcProtocol protocol)
    {
        return protocol switch
        {
            IpcProtocol.Json => JsonSerializer.SerializeToUtf8Bytes(message, AureTTYJsonSerializerContext.Default.TerminalIpcMessage),
            IpcProtocol.MessagePack => MessagePackSerializer.Serialize(message, MessagePackSerializerOptions.Standard),
            _ => throw new ArgumentOutOfRangeException(nameof(protocol), protocol, "Unsupported IPC protocol.")
        };
    }

    public static TerminalIpcMessage? Deserialize(ReadOnlySpan<byte> data, IpcProtocol protocol)
    {
        return protocol switch
        {
            IpcProtocol.Json => JsonSerializer.Deserialize(data, AureTTYJsonSerializerContext.Default.TerminalIpcMessage),
            IpcProtocol.MessagePack => MessagePackSerializer.Deserialize<TerminalIpcMessage>(new ReadOnlySequence<byte>(data.ToArray()), MessagePackSerializerOptions.Standard),
            _ => throw new ArgumentOutOfRangeException(nameof(protocol), protocol, "Unsupported IPC protocol.")
        };
    }
}

public enum IpcProtocol
{
    Json,
    MessagePack
}

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
        if (data.IsEmpty)
        {
            return null;
        }

        return Deserialize(data.ToArray().AsMemory(), protocol);
    }

    public static TerminalIpcMessage? Deserialize(ReadOnlyMemory<byte> data, IpcProtocol protocol)
    {
        return protocol switch
        {
            IpcProtocol.Json => JsonSerializer.Deserialize(data.Span, AureTTYJsonSerializerContext.Default.TerminalIpcMessage),
            IpcProtocol.MessagePack => DeserializeMessagePack(data),
            _ => throw new ArgumentOutOfRangeException(nameof(protocol), protocol, "Unsupported IPC protocol.")
        };
    }

    private static TerminalIpcMessage? DeserializeMessagePack(ReadOnlyMemory<byte> data)
    {
        var reader = new MessagePackReader(new ReadOnlySequence<byte>(data));
        return MessagePackSerializer.Deserialize<TerminalIpcMessage>(ref reader, MessagePackSerializerOptions.Standard);
    }
}

public enum IpcProtocol
{
    Json,
    MessagePack
}

// Copyright © 2023-2026 Vitaly Kuzyaev. All rights reserved.
// This file is part of the AureTTY project.
// Licensed under the GNU Affero General Public License v3.0.

namespace AureTTY.Contracts.DTOs;

[MessagePackObject]
public sealed class TerminalSessionInputChunkDiagnostics(
    long sequence,
    int charCount,
    int byteCount,
    string codepoints,
    string bytesHex,
    DateTimeOffset receivedAtUtc)
{
    [Key(0)]
    public long Sequence { get; } = sequence;

    [Key(1)]
    public int CharCount { get; } = charCount;

    [Key(2)]
    public int ByteCount { get; } = byteCount;

    [Key(3)]
    public string Codepoints { get; } = codepoints;

    [Key(4)]
    public string BytesHex { get; } = bytesHex;

    [Key(5)]
    public DateTimeOffset ReceivedAtUtc { get; } = receivedAtUtc;
}

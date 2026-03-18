using System.ComponentModel.DataAnnotations;

namespace AureTTY.Api.Models;

public sealed class AttachTerminalSessionRequest
{
    [Range(0, long.MaxValue)]
    public long? LastReceivedSequenceNumber { get; init; }

    [Range(1, int.MaxValue)]
    public int? Columns { get; init; }

    [Range(1, int.MaxValue)]
    public int? Rows { get; init; }
}

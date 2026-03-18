using System.ComponentModel.DataAnnotations;

namespace AureTTY.Api.Models;

public sealed class CreateTerminalInputRequest
{
    [Required]
    public string Text { get; init; } = string.Empty;

    [Range(1, long.MaxValue)]
    public long Sequence { get; init; }
}

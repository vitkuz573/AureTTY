using System.ComponentModel.DataAnnotations;

namespace AureTTY.Api.Models;

public sealed class UpdateTerminalSizeRequest
{
    [Range(1, int.MaxValue)]
    public int Columns { get; init; }

    [Range(1, int.MaxValue)]
    public int Rows { get; init; }
}

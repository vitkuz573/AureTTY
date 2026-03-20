using System.ComponentModel.DataAnnotations;
using AureTTY.Contracts.Enums;

namespace AureTTY.Api.Models;

public sealed class CreateTerminalSessionRequest
{
    public string? SessionId { get; init; }

    [EnumDataType(typeof(Shell))]
    public Shell? Shell { get; init; }

    [EnumDataType(typeof(ExecutionRunContext))]
    public ExecutionRunContext RunContext { get; init; } = ExecutionRunContext.InteractiveUser;

    public string? UserName { get; init; }

    public string? Domain { get; init; }

    public string? Password { get; init; }

    public bool LoadUserProfile { get; init; } = true;

    public string? WorkingDirectory { get; init; }

    [Range(1, int.MaxValue)]
    public int? Columns { get; init; }

    [Range(1, int.MaxValue)]
    public int? Rows { get; init; }
}

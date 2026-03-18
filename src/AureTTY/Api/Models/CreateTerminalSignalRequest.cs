using System.ComponentModel.DataAnnotations;
using AureTTY.Contracts.Enums;

namespace AureTTY.Api.Models;

public sealed class CreateTerminalSignalRequest
{
    [EnumDataType(typeof(TerminalSessionSignal))]
    public TerminalSessionSignal Signal { get; init; }
}

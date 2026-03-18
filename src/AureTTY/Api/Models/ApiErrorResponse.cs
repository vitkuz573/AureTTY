using System.Text.Json.Serialization;

namespace AureTTY.Api.Models;

public sealed class ApiErrorResponse
{
    [JsonPropertyName("error")]
    public required string Error { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }
}

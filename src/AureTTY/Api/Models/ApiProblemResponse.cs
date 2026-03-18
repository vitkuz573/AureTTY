using System.Text.Json.Serialization;

namespace AureTTY.Api.Models;

public sealed class ApiProblemResponse
{
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    [JsonPropertyName("detail")]
    public required string Detail { get; init; }

    [JsonPropertyName("status")]
    public required int Status { get; init; }
}

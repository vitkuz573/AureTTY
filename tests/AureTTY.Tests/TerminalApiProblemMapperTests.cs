using System.Text.Json;
using AureTTY.Api;
using AureTTY.Contracts.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AureTTY.Tests;

public sealed class TerminalApiProblemMapperTests
{
    [Fact]
    public void Map_WhenExceptionIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => TerminalApiProblemMapper.Map(null!));
    }

    [Fact]
    public async Task Map_WhenValidationException_ReturnsBadRequestJson()
    {
        var result = TerminalApiProblemMapper.Map(new TerminalSessionValidationException("invalid"));
        var (statusCode, body) = await ExecuteResultAsync(result);

        Assert.Equal(StatusCodes.Status400BadRequest, statusCode);
        Assert.Equal("invalid", ExtractErrorMessageFromJson(body));
    }

    [Fact]
    public async Task Map_WhenNotFoundExceptionWrapped_UsesBaseExceptionAndReturnsNotFoundJson()
    {
        var wrapped = new InvalidOperationException("wrapper", new TerminalSessionNotFoundException("missing"));
        var result = TerminalApiProblemMapper.Map(wrapped);
        var (statusCode, body) = await ExecuteResultAsync(result);

        Assert.Equal(StatusCodes.Status404NotFound, statusCode);
        Assert.Equal("missing", ExtractErrorMessageFromJson(body));
    }

    [Fact]
    public async Task Map_WhenForbiddenException_ReturnsForbiddenJson()
    {
        var result = TerminalApiProblemMapper.Map(new TerminalSessionForbiddenException("forbidden"));
        var (statusCode, body) = await ExecuteResultAsync(result);

        Assert.Equal(StatusCodes.Status403Forbidden, statusCode);
        Assert.Equal("forbidden", ExtractErrorMessageFromJson(body));
    }

    [Fact]
    public async Task Map_WhenConflictException_ReturnsConflictJson()
    {
        var result = TerminalApiProblemMapper.Map(new TerminalSessionConflictException("conflict"));
        var (statusCode, body) = await ExecuteResultAsync(result);

        Assert.Equal(StatusCodes.Status409Conflict, statusCode);
        Assert.Equal("conflict", ExtractErrorMessageFromJson(body));
    }

    [Fact]
    public async Task Map_WhenUnauthorizedAccessException_ReturnsUnauthorized()
    {
        var result = TerminalApiProblemMapper.Map(new UnauthorizedAccessException("nope"));
        var (statusCode, body) = await ExecuteResultAsync(result);

        Assert.Equal(StatusCodes.Status401Unauthorized, statusCode);
        Assert.True(string.IsNullOrWhiteSpace(body));
    }

    [Fact]
    public async Task Map_WhenUnknownException_ReturnsProblemJson()
    {
        var result = TerminalApiProblemMapper.Map(new Exception("unexpected"));
        var (statusCode, body) = await ExecuteResultAsync(result);

        Assert.Equal(StatusCodes.Status500InternalServerError, statusCode);
        using var document = JsonDocument.Parse(body!);
        Assert.Equal("Terminal operation failed.", document.RootElement.GetProperty("title").GetString());
        Assert.Equal("unexpected", document.RootElement.GetProperty("detail").GetString());
        Assert.Equal(StatusCodes.Status500InternalServerError, document.RootElement.GetProperty("status").GetInt32());
    }

    private static string? ExtractErrorMessageFromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        using var document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty("error", out var errorProperty)
            ? errorProperty.GetString()
            : null;
    }

    private static async Task<(int StatusCode, string? Body)> ExecuteResultAsync(IResult result)
    {
        var context = new DefaultHttpContext();
        var services = new ServiceCollection();
        services.AddLogging();
        context.RequestServices = services.BuildServiceProvider();
        context.Response.Body = new MemoryStream();

        await result.ExecuteAsync(context);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();
        return (context.Response.StatusCode, body);
    }
}

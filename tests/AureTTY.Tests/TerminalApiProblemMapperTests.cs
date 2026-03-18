using System.Text.Json;
using AureTTY.Api;
using AureTTY.Contracts.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace AureTTY.Tests;

public sealed class TerminalApiProblemMapperTests
{
    [Fact]
    public void Map_WhenControllerIsNull_Throws()
    {
        var exception = new Exception("boom");

        Assert.Throws<ArgumentNullException>(() => TerminalApiProblemMapper.Map(null!, exception));
    }

    [Fact]
    public void Map_WhenExceptionIsNull_Throws()
    {
        var controller = new TestController();

        Assert.Throws<ArgumentNullException>(() => TerminalApiProblemMapper.Map(controller, null!));
    }

    [Fact]
    public void Map_WhenValidationException_ReturnsBadRequest()
    {
        var result = TerminalApiProblemMapper.Map(new TestController(), new TerminalSessionValidationException("invalid"));

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
        Assert.Equal("invalid", ExtractErrorMessage(badRequest.Value));
    }

    [Fact]
    public void Map_WhenNotFoundExceptionWrapped_UsesBaseExceptionAndReturnsNotFound()
    {
        var wrapped = new InvalidOperationException("wrapper", new TerminalSessionNotFoundException("missing"));

        var result = TerminalApiProblemMapper.Map(new TestController(), wrapped);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFound.StatusCode);
        Assert.Equal("missing", ExtractErrorMessage(notFound.Value));
    }

    [Fact]
    public void Map_WhenForbiddenException_Returns403()
    {
        var result = TerminalApiProblemMapper.Map(new TestController(), new TerminalSessionForbiddenException("forbidden"));

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
        Assert.Equal("forbidden", ExtractErrorMessage(objectResult.Value));
    }

    [Fact]
    public void Map_WhenConflictException_Returns409()
    {
        var result = TerminalApiProblemMapper.Map(new TestController(), new TerminalSessionConflictException("conflict"));

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
        Assert.Equal("conflict", ExtractErrorMessage(conflict.Value));
    }

    [Fact]
    public void Map_WhenUnauthorizedAccessException_ReturnsUnauthorized()
    {
        var result = TerminalApiProblemMapper.Map(new TestController(), new UnauthorizedAccessException("nope"));

        _ = Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public void Map_WhenUnknownException_ReturnsProblemDetails500()
    {
        var result = TerminalApiProblemMapper.Map(new TestController(), new Exception("unexpected"));

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);

        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("Terminal operation failed.", problem.Title);
        Assert.Equal("unexpected", problem.Detail);
        Assert.Equal(StatusCodes.Status500InternalServerError, problem.Status);
    }

    [Fact]
    public async Task Map_MinimalApi_WhenValidationException_ReturnsBadRequestJson()
    {
        var result = TerminalApiProblemMapper.Map(new TerminalSessionValidationException("invalid"));

        var (statusCode, body) = await ExecuteResultAsync(result);

        Assert.Equal(StatusCodes.Status400BadRequest, statusCode);
        Assert.Equal("invalid", ExtractErrorMessageFromJson(body));
    }

    [Fact]
    public async Task Map_MinimalApi_WhenUnknownException_ReturnsProblemJson()
    {
        var result = TerminalApiProblemMapper.Map(new Exception("unexpected"));

        var (statusCode, body) = await ExecuteResultAsync(result);

        Assert.Equal(StatusCodes.Status500InternalServerError, statusCode);
        using var document = JsonDocument.Parse(body!);
        Assert.Equal("Terminal operation failed.", document.RootElement.GetProperty("title").GetString());
        Assert.Equal("unexpected", document.RootElement.GetProperty("detail").GetString());
        Assert.Equal(StatusCodes.Status500InternalServerError, document.RootElement.GetProperty("status").GetInt32());
    }

    private static string? ExtractErrorMessage(object? value)
    {
        if (value is null)
        {
            return null;
        }

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(value));
        return document.RootElement.TryGetProperty("error", out var errorProperty)
            ? errorProperty.GetString()
            : null;
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

    private sealed class TestController : ControllerBase;
}

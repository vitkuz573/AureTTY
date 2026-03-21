using System.Net.WebSockets;
using System.Text.Json;
using AureTTY.Api;
using AureTTY.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace AureTTY.Tests;

public sealed class ApiKeyAuthenticationMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenApiKeyIsNotConfigured_Throws()
    {
        var middleware = new ApiKeyAuthenticationMiddleware(_ =>
            Task.CompletedTask);

        var context = CreateContext("/health");

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context, CreateOptions(" ")));
    }

    [Fact]
    public async Task InvokeAsync_WhenHeaderKeyMatches_CallsNext()
    {
        var wasCalled = false;
        var middleware = new ApiKeyAuthenticationMiddleware(_ =>
        {
            wasCalled = true;
            return Task.CompletedTask;
        });

        var context = CreateContext("/api/v1/health");
        context.Request.Headers[TerminalServiceOptions.ApiKeyHeaderName] = "secret";

        await middleware.InvokeAsync(context, CreateOptions("secret"));

        Assert.True(wasCalled);
    }

    [Fact]
    public async Task InvokeAsync_WhenWebSocketRequest_SkipsHttpApiKeyValidation()
    {
        var wasCalled = false;
        var middleware = new ApiKeyAuthenticationMiddleware(_ =>
        {
            wasCalled = true;
            return Task.CompletedTask;
        });

        var context = CreateContext("/api/v1/viewers/v/sessions/ws");
        context.Features.Set<IHttpWebSocketFeature>(new TestWebSocketFeature(isWebSocketRequest: true));

        await middleware.InvokeAsync(context, CreateOptions("secret"));

        Assert.True(wasCalled);
    }

    [Fact]
    public async Task InvokeAsync_WhenUnauthorized_Returns401WithPayload()
    {
        var wasCalled = false;
        var middleware = new ApiKeyAuthenticationMiddleware(_ =>
        {
            wasCalled = true;
            return Task.CompletedTask;
        });

        var context = CreateContext("/api/v1/health");
        context.Request.Headers[TerminalServiceOptions.ApiKeyHeaderName] = "invalid";

        await middleware.InvokeAsync(context, CreateOptions("secret"));

        Assert.False(wasCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var payload = await reader.ReadToEndAsync();
        using var document = JsonDocument.Parse(payload);
        Assert.Equal("Unauthorized", document.RootElement.GetProperty("error").GetString());
        Assert.Contains(TerminalServiceOptions.ApiKeyHeaderName, document.RootElement.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.DoesNotContain("api_key", document.RootElement.GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    private static DefaultHttpContext CreateContext(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static TerminalServiceOptions CreateOptions(string apiKey)
    {
        return new TerminalServiceOptions(
            PipeName: "pipe-auth",
            PipeToken: "token-auth",
            EnablePipeApi: true,
            EnableHttpApi: true,
            HttpListenUrl: "http://127.0.0.1:17850",
            ApiKey: apiKey);
    }

    private sealed class TestWebSocketFeature(bool isWebSocketRequest) : IHttpWebSocketFeature
    {
        public bool IsWebSocketRequest { get; } = isWebSocketRequest;

        public Task<WebSocket> AcceptAsync(WebSocketAcceptContext context)
        {
            throw new NotSupportedException();
        }
    }
}

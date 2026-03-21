using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using AureTTY.Api;
using AureTTY.Contracts.Abstractions;
using AureTTY.Contracts.DTOs;
using AureTTY.Contracts.Enums;
using AureTTY.Contracts.Exceptions;
using AureTTY.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace AureTTY.Tests;

public sealed class HttpTransportTests
{
    [Fact]
    public async Task Api_WhenApiKeyIsMissing_ReturnsUnauthorized()
    {
        await using var host = await CreateHostAsync();
        using var client = host.GetTestClient();

        var response = await client.GetAsync("/api/v1/health");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Api_WhenSessionLifecycleOverHttp_CompletesSuccessfully()
    {
        await using var host = await CreateHostAsync();
        using var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add(TerminalServiceOptions.ApiKeyHeaderName, "test-api-key");

        var createResponse = await client.PostAsJsonAsync(
            "/api/v1/viewers/viewer-http/sessions",
            new
            {
                shell = Shell.Bash
            });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var handle = await createResponse.Content.ReadFromJsonAsync<TerminalSessionHandle>();
        Assert.NotNull(handle);
        Assert.False(string.IsNullOrWhiteSpace(handle.SessionId));

        var getResponse = await client.GetAsync($"/api/v1/viewers/viewer-http/sessions/{handle.SessionId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var sendInputResponse = await client.PostAsJsonAsync(
            $"/api/v1/viewers/viewer-http/sessions/{handle.SessionId}/inputs",
            new
            {
                text = "echo hi\n",
                sequence = 1L
            });
        Assert.Equal(HttpStatusCode.Accepted, sendInputResponse.StatusCode);

        var diagnosticsResponse = await client.GetAsync($"/api/v1/viewers/viewer-http/sessions/{handle.SessionId}/input-diagnostics");
        Assert.Equal(HttpStatusCode.OK, diagnosticsResponse.StatusCode);

        var diagnostics = await diagnosticsResponse.Content.ReadFromJsonAsync<TerminalSessionInputDiagnostics>();
        Assert.NotNull(diagnostics);
        Assert.Equal(handle.SessionId, diagnostics.SessionId);
        Assert.Equal("viewer-http", diagnostics.ViewerId);

        var deleteResponse = await client.DeleteAsync($"/api/v1/viewers/viewer-http/sessions/{handle.SessionId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    private static async Task<WebApplication> CreateHostAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(new TerminalServiceOptions(
            PipeName: "pipe-test",
            PipeToken: "pipe-token",
            EnablePipeApi: false,
            EnableHttpApi: true,
            HttpListenUrl: "http://127.0.0.1:17850",
            ApiKey: "test-api-key"));
        builder.Services.AddSingleton<ITerminalSessionService, InMemoryTerminalSessionService>();

        var app = builder.Build();
        app.UseMiddleware<ApiKeyAuthenticationMiddleware>();
        app.MapTerminalHttpEndpoints();

        await app.StartAsync();
        return app;
    }

    private sealed class InMemoryTerminalSessionService : ITerminalSessionService
    {
        private readonly ConcurrentDictionary<string, SessionRecord> _sessions = new(StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlyCollection<TerminalSessionHandle>> GetAllSessionsAsync(CancellationToken cancellationToken = default)
        {
            var result = _sessions.Values
                .Select(static record => record.Handle)
                .ToArray();
            return Task.FromResult<IReadOnlyCollection<TerminalSessionHandle>>(result);
        }

        public Task<IReadOnlyCollection<TerminalSessionHandle>> GetViewerSessionsAsync(string viewerId, CancellationToken cancellationToken = default)
        {
            var result = _sessions.Values
                .Where(record => string.Equals(record.ViewerId, viewerId, StringComparison.Ordinal))
                .Select(static record => record.Handle)
                .ToArray();
            return Task.FromResult<IReadOnlyCollection<TerminalSessionHandle>>(result);
        }

        public Task<TerminalSessionHandle> GetSessionAsync(string viewerId, string sessionId, CancellationToken cancellationToken = default)
        {
            if (!_sessions.TryGetValue(sessionId, out var record))
            {
                throw new TerminalSessionNotFoundException($"Terminal session '{sessionId}' was not found.");
            }

            if (!string.Equals(record.ViewerId, viewerId, StringComparison.Ordinal))
            {
                throw new TerminalSessionForbiddenException("Terminal session belongs to another viewer.");
            }

            return Task.FromResult(record.Handle);
        }

        public Task<TerminalSessionHandle> StartAsync(string viewerId, TerminalSessionStartRequest request, CancellationToken cancellationToken = default)
        {
            var handle = new TerminalSessionHandle(request.SessionId)
            {
                State = TerminalSessionState.Running
            };

            if (!_sessions.TryAdd(request.SessionId, new SessionRecord(viewerId, handle)))
            {
                throw new TerminalSessionConflictException($"Terminal session '{request.SessionId}' already exists.");
            }

            return Task.FromResult(handle);
        }

        public Task<TerminalSessionHandle> ResumeAsync(string viewerId, TerminalSessionResumeRequest request, CancellationToken cancellationToken = default)
        {
            return GetSessionAsync(viewerId, request.SessionId, cancellationToken);
        }

        public Task SendInputAsync(string viewerId, TerminalSessionInputRequest request, CancellationToken cancellationToken = default)
        {
            _ = GetSessionAsync(viewerId, request.SessionId, cancellationToken);
            return Task.CompletedTask;
        }

        public Task<TerminalSessionInputDiagnostics> GetInputDiagnosticsAsync(string viewerId, string sessionId, CancellationToken cancellationToken = default)
        {
            _ = GetSessionAsync(viewerId, sessionId, cancellationToken);

            return Task.FromResult(new TerminalSessionInputDiagnostics(sessionId)
            {
                State = TerminalSessionState.Running,
                ViewerId = viewerId,
                GeneratedAtUtc = DateTimeOffset.UtcNow
            });
        }

        public Task ResizeAsync(string viewerId, TerminalSessionResizeRequest request, CancellationToken cancellationToken = default)
        {
            _ = GetSessionAsync(viewerId, request.SessionId, cancellationToken);
            return Task.CompletedTask;
        }

        public Task SignalAsync(string viewerId, string sessionId, TerminalSessionSignal signal, CancellationToken cancellationToken = default)
        {
            _ = GetSessionAsync(viewerId, sessionId, cancellationToken);
            return Task.CompletedTask;
        }

        public Task CloseAsync(string viewerId, string sessionId, CancellationToken cancellationToken = default)
        {
            _ = GetSessionAsync(viewerId, sessionId, cancellationToken);
            _sessions.TryRemove(sessionId, out _);
            return Task.CompletedTask;
        }

        public Task CloseViewerSessionsAsync(string viewerId, CancellationToken cancellationToken = default)
        {
            var sessionIds = _sessions.Values
                .Where(record => string.Equals(record.ViewerId, viewerId, StringComparison.Ordinal))
                .Select(record => record.Handle.SessionId)
                .ToArray();

            foreach (var sessionId in sessionIds)
            {
                _sessions.TryRemove(sessionId, out _);
            }

            return Task.CompletedTask;
        }

        public Task CloseAllSessionsAsync(CancellationToken cancellationToken = default)
        {
            _sessions.Clear();
            return Task.CompletedTask;
        }

        private sealed record SessionRecord(string ViewerId, TerminalSessionHandle Handle);
    }
}

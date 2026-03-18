using AureTTY.Api.Models;
using AureTTY.Controllers;
using AureTTY.Services;
using Microsoft.AspNetCore.Mvc;

namespace AureTTY.Tests;

public sealed class TerminalHealthControllerTests
{
    [Fact]
    public void Constructor_WhenOptionsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new TerminalHealthController(null!));
    }

    [Fact]
    public void Get_WhenHttpAndPipeEnabled_ReturnsBothTransports()
    {
        var options = CreateOptions(enablePipeApi: true, enableHttpApi: true);
        var controller = new TerminalHealthController(options);

        var action = controller.Get();

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var payload = Assert.IsType<TerminalHealthResponse>(ok.Value);
        Assert.Equal("ok", payload.Status);
        Assert.Equal(TerminalServiceOptions.ApiVersion, payload.ApiVersion);
        Assert.Equal(["http", "pipe"], payload.Transports);
    }

    [Fact]
    public void Get_WhenOnlyPipeEnabled_ReturnsOnlyPipeTransport()
    {
        var options = CreateOptions(enablePipeApi: true, enableHttpApi: false);
        var controller = new TerminalHealthController(options);

        var action = controller.Get();

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var payload = Assert.IsType<TerminalHealthResponse>(ok.Value);
        Assert.Equal(["pipe"], payload.Transports);
    }

    private static TerminalServiceOptions CreateOptions(bool enablePipeApi, bool enableHttpApi)
    {
        return new TerminalServiceOptions(
            PipeName: "pipe-health",
            PipeToken: "token-health",
            EnablePipeApi: enablePipeApi,
            EnableHttpApi: enableHttpApi,
            HttpListenUrl: "http://127.0.0.1:17850",
            ApiKey: "key-health");
    }
}

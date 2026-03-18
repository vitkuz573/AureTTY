using AureTTY.Execution.Abstractions;
using AureTTY.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AureTTY.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddAureTTYTerminalServices_WhenBuildingProvider_ResolvesProcessService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAureTTYTerminalServices(new TerminalServiceOptions(
            PipeName: "pipe-test",
            PipeToken: "token-test",
            EnablePipeApi: true,
            EnableHttpApi: true,
            HttpListenUrl: "http://127.0.0.1:17850",
            ApiKey: "token-test"));

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        var processService = provider.GetRequiredService<IProcessService>();
        var hostedServices = provider.GetServices<IHostedService>();

        Assert.NotNull(processService);
        Assert.Contains(hostedServices, hostedService => hostedService is TerminalPipeServer);
    }

    [Fact]
    public void AddAureTTYTerminalServices_WhenPipeApiIsDisabled_DoesNotRegisterPipeServer()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAureTTYTerminalServices(new TerminalServiceOptions(
            PipeName: "pipe-test",
            PipeToken: "token-test",
            EnablePipeApi: false,
            EnableHttpApi: true,
            HttpListenUrl: "http://127.0.0.1:17850",
            ApiKey: "token-test"));

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        var hostedServices = provider.GetServices<IHostedService>();

        Assert.DoesNotContain(hostedServices, hostedService => hostedService is TerminalPipeServer);
        Assert.NotNull(provider.GetRequiredService<HttpTerminalSessionEventPublisher>());
    }
}

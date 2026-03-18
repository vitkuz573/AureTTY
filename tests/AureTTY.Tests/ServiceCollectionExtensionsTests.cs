using AureTTY.Execution.Abstractions;
using AureTTY.Services;
using AureTTY.Windows.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AureTTY.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddAureTTYTerminalServices_WhenServicesNull_Throws()
    {
        var options = new TerminalServiceOptions(
            PipeName: "pipe-test",
            PipeToken: "token-test",
            EnablePipeApi: true,
            EnableHttpApi: true,
            HttpListenUrl: "http://127.0.0.1:17850",
            ApiKey: "token-test");

        Assert.Throws<ArgumentNullException>(() => ServiceCollectionExtensions.AddAureTTYTerminalServices(null!, options));
    }

    [Fact]
    public void AddAureTTYTerminalServices_WhenOptionsNull_Throws()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() => services.AddAureTTYTerminalServices(null!));
    }

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

    [Fact]
    public void AddAureTTYTerminalServices_WhenBuildingProvider_RegistersWindowsBackendServices()
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

        Assert.NotNull(provider.GetRequiredService<ISessionService>());
        Assert.NotNull(provider.GetRequiredService<INativeProcessFactory>());
        Assert.NotNull(provider.GetRequiredService<INativeProcessOptionsProvider>());
        Assert.NotNull(provider.GetRequiredService<ICommandLineProvider>());
    }
}

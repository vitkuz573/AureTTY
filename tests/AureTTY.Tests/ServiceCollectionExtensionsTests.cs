// Copyright © 2023-2026 Vitaly Kuzyaev. All rights reserved.
// This file is part of the AureTTY project.
// Licensed under the GNU Affero General Public License v3.0.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using AureTTY.Execution.Abstractions;
using AureTTY.Services;

namespace AureTTY.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddAureTTYTerminalServices_WhenBuildingProvider_ResolvesProcessService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAureTTYTerminalServices(new TerminalServiceOptions("pipe-test", "token-test"));

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
}

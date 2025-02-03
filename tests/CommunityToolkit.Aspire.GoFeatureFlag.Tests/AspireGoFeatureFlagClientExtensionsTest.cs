// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Components.Common.Tests;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using OpenFeature.Contrib.Providers.GOFeatureFlag;

namespace CommunityToolkit.Aspire.GoFeatureFlag.Tests;

public class AspireGoFeatureFlagClientExtensionsTest(GoFeatureFlagContainerFixture containerFixture) : IClassFixture<GoFeatureFlagContainerFixture>
{
    private const string DefaultConnectionName = "goff";

    private string DefaultConnectionString =>
            RequiresDockerAttribute.IsSupported ? containerFixture.GetConnectionString() : "http://localhost:27011";

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [RequiresDocker]
    public async Task AddGoFeatureFlagClient_HealthCheckShouldBeRegisteredWhenEnabled(bool useKeyed)
    {
        var key = DefaultConnectionName;

        var builder = CreateBuilder(DefaultConnectionString);

        if (useKeyed)
        {
            builder.AddKeyedGoFeatureFlagClient(key, settings =>
            {
                settings.DisableHealthChecks = false;
            });
        }
        else
        {
            builder.AddGoFeatureFlagClient(DefaultConnectionName, settings =>
            {
                settings.DisableHealthChecks = false;
            });
        }

        using var host = builder.Build();

        var healthCheckService = host.Services.GetRequiredService<HealthCheckService>();

        var healthCheckReport = await healthCheckService.CheckHealthAsync();

        var healthCheckName = useKeyed ? $"Goff_{key}" : "Goff";

        Assert.Contains(healthCheckReport.Entries, x => x.Key == healthCheckName);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AddGoFeatureFlagClient_HealthCheckShouldNotBeRegisteredWhenDisabled(bool useKeyed)
    {
        var builder = CreateBuilder(DefaultConnectionString);

        if (useKeyed)
        {
            builder.AddKeyedGoFeatureFlagClient(DefaultConnectionName, settings =>
            {
                settings.DisableHealthChecks = true;
            });
        }
        else
        {
            builder.AddGoFeatureFlagClient(DefaultConnectionName, settings =>
            {
                settings.DisableHealthChecks = true;
            });
        }

        using var host = builder.Build();

        var healthCheckService = host.Services.GetService<HealthCheckService>();

        Assert.Null(healthCheckService);
    }

    [Fact]
    public void CanAddMultipleKeyedServices()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>("ConnectionStrings:goff1", "http://localhost:19530"),
            new KeyValuePair<string, string?>("ConnectionStrings:goff2", "http://localhost:19531"),
            new KeyValuePair<string, string?>("ConnectionStrings:goff3", "http://localhost:19532"),
        ]);

        builder.AddGoFeatureFlagClient("goff1");
        builder.AddKeyedGoFeatureFlagClient("goff2");
        builder.AddKeyedGoFeatureFlagClient("goff3");

        using var host = builder.Build();

        var client1 = host.Services.GetRequiredService<GoFeatureFlagProvider>();
        var client2 = host.Services.GetRequiredKeyedService<GoFeatureFlagProvider>("goff2");
        var client3 = host.Services.GetRequiredKeyedService<GoFeatureFlagProvider>("goff3");

        Assert.NotSame(client1, client2);
        Assert.NotSame(client1, client3);
        Assert.NotSame(client2, client3);
    }

    [Fact]
    public void CanAddClientFromEncodedConnectionString()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>("ConnectionStrings:goff1", "Endpoint=http://localhost:19530"),
            new KeyValuePair<string, string?>("ConnectionStrings:goff2", "Endpoint=http://localhost:19531"),
        ]);

        builder.AddGoFeatureFlagClient("goff1");
        builder.AddKeyedGoFeatureFlagClient("goff2");

        using var host = builder.Build();

        var client1 = host.Services.GetRequiredService<GoFeatureFlagProvider>();
        var client2 = host.Services.GetRequiredKeyedService<GoFeatureFlagProvider>("goff2");

        Assert.NotSame(client1, client2);
    }

    private static HostApplicationBuilder CreateBuilder(string connectionString)
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>($"ConnectionStrings:{DefaultConnectionName}", connectionString)
        ]);
        return builder;
    }
}

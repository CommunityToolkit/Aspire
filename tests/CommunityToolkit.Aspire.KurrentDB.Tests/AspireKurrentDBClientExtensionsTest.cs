// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;
using KurrentDB.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace CommunityToolkit.Aspire.KurrentDB.Tests;

public class AspireKurrentDBClientExtensionsTest(KurrentDBContainerFixture containerFixture) : IClassFixture<KurrentDBContainerFixture>
{
    private const string DefaultConnectionName = "kurrentdb";

    private string DefaultConnectionString =>
            RequiresDockerAttribute.IsSupported ? containerFixture.GetConnectionString() : "kurrentdb://localhost:2113?tls=false";

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [RequiresDocker]
    public async Task AddKurrentDBClient_HealthCheckShouldBeRegisteredWhenEnabled(bool useKeyed)
    {
        var key = DefaultConnectionName;

        var builder = CreateBuilder(DefaultConnectionString);

        if (useKeyed)
        {
            builder.AddKeyedKurrentDBClient(key, settings =>
            {
                settings.DisableHealthChecks = false;
            });
        }
        else
        {
            builder.AddKurrentDBClient(DefaultConnectionName, settings =>
            {
                settings.DisableHealthChecks = false;
            });
        }

        using var host = builder.Build();

        var healthCheckService = host.Services.GetRequiredService<HealthCheckService>();

        var healthCheckReport = await healthCheckService.CheckHealthAsync();

        var healthCheckName = useKeyed ? $"KurrentDB.Client_{key}" : "KurrentDB.Client";

        Assert.Contains(healthCheckReport.Entries, x => x.Key == healthCheckName);
    }

    [Fact]
    public void CanAddMultipleKeyedServices()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>("ConnectionStrings:kurrentdb1", "kurrentdb://localhost:22113?tls=false"),
            new KeyValuePair<string, string?>("ConnectionStrings:kurrentdb2", "kurrentdb://localhost:22114?tls=false"),
            new KeyValuePair<string, string?>("ConnectionStrings:kurrentdb3", "kurrentdb://localhost:22115?tls=false"),
        ]);

        builder.AddKurrentDBClient("kurrentdb1");
        builder.AddKeyedKurrentDBClient("kurrentdb2");
        builder.AddKeyedKurrentDBClient("kurrentdb3");

        using var host = builder.Build();

        var client1 = host.Services.GetRequiredService<KurrentDBClient>();
        var client2 = host.Services.GetRequiredKeyedService<KurrentDBClient>("kurrentdb2");
        var client3 = host.Services.GetRequiredKeyedService<KurrentDBClient>("kurrentdb3");

        Assert.NotSame(client1, client2);
        Assert.NotSame(client1, client3);
        Assert.NotSame(client2, client3);
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

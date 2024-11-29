// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Components.Common.Tests;
using EventStore.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace CommunityToolkit.Aspire.EventStore.Tests;

public class AspireEventStoreClientExtensionsTest(EventStoreContainerFixture containerFixture) : IClassFixture<EventStoreContainerFixture>
{
    private const string DefaultConnectionName = "eventstore";

    private string DefaultConnectionString =>
            RequiresDockerAttribute.IsSupported ? containerFixture.GetConnectionString() : "esdb://localhost:2113?tls=false";

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [RequiresDocker]
    public async Task AddEventStoreClient_HealthCheckShouldBeRegisteredWhenEnabled(bool useKeyed)
    {
        var key = DefaultConnectionName;

        var builder = CreateBuilder(DefaultConnectionString);

        if (useKeyed)
        {
            builder.AddKeyedEventStoreClient(key, settings =>
            {
                settings.DisableHealthChecks = false;
            });
        }
        else
        {
            builder.AddEventStoreClient(DefaultConnectionName, settings =>
            {
                settings.DisableHealthChecks = false;
            });
        }

        using var host = builder.Build();

        var healthCheckService = host.Services.GetRequiredService<HealthCheckService>();

        var healthCheckReport = await healthCheckService.CheckHealthAsync();

        var healthCheckName = useKeyed ? $"EventStore.Client_{key}" : "EventStore.Client";

        Assert.Contains(healthCheckReport.Entries, x => x.Key == healthCheckName);
    }

    [Fact]
    public void CanAddMultipleKeyedServices()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>("ConnectionStrings:eventstore1", "esdb://localhost:22113?tls=false"),
            new KeyValuePair<string, string?>("ConnectionStrings:eventstore2", "esdb://localhost:22114?tls=false"),
            new KeyValuePair<string, string?>("ConnectionStrings:eventstore3", "esdb://localhost:22115?tls=false"),
        ]);

        builder.AddEventStoreClient("eventstore1");
        builder.AddKeyedEventStoreClient("eventstore2");
        builder.AddKeyedEventStoreClient("eventstore3");

        using var host = builder.Build();

        var client1 = host.Services.GetRequiredService<EventStoreClient>();
        var client2 = host.Services.GetRequiredKeyedService<EventStoreClient>("eventstore2");
        var client3 = host.Services.GetRequiredKeyedService<EventStoreClient>("eventstore3");

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

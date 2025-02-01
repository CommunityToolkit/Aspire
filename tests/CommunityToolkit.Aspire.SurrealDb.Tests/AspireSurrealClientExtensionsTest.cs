// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Components.Common.Tests;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using SurrealDb.Net;

namespace CommunityToolkit.Aspire.SurrealDb.Tests;

public class AspireSurrealClientExtensionsTest(SurrealDbContainerFixture containerFixture) : IClassFixture<SurrealDbContainerFixture>
{
    private const string DefaultConnectionName = "db";

    private string DefaultConnectionString =>
            RequiresDockerAttribute.IsSupported ? containerFixture.GetConnectionString() : "http://localhost:27011";

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [RequiresDocker]
    public async Task AddSurrealClient_HealthCheckShouldBeRegisteredWhenEnabled(bool useKeyed)
    {
        var key = DefaultConnectionName;

        var builder = CreateBuilder(DefaultConnectionString);

        if (useKeyed)
        {
            builder.AddKeyedSurrealClient(key, settings =>
            {
                settings.DisableHealthChecks = false;
            });
        }
        else
        {
            builder.AddSurrealClient(DefaultConnectionName, settings =>
            {
                settings.DisableHealthChecks = false;
            });
        }

        using var host = builder.Build();

        var healthCheckService = host.Services.GetRequiredService<HealthCheckService>();

        var healthCheckReport = await healthCheckService.CheckHealthAsync();

        var healthCheckName = useKeyed ? $"surrealdb_{key}" : "surrealdb";

        Assert.Contains(healthCheckReport.Entries, x => x.Key == healthCheckName);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AddSurrealClient_HealthCheckShouldNotBeRegisteredWhenDisabled(bool useKeyed)
    {
        var builder = CreateBuilder(DefaultConnectionString);

        if (useKeyed)
        {
            builder.AddKeyedSurrealClient(DefaultConnectionName, settings =>
            {
                settings.DisableHealthChecks = true;
            });
        }
        else
        {
            builder.AddSurrealClient(DefaultConnectionName, settings =>
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
            new KeyValuePair<string, string?>("ConnectionStrings:surreal1", "http://localhost:19530"),
            new KeyValuePair<string, string?>("ConnectionStrings:surreal2", "http://localhost:19531"),
            new KeyValuePair<string, string?>("ConnectionStrings:surreal3", "http://localhost:19532"),
        ]);

        builder.AddSurrealClient("surreal1");
        builder.AddKeyedSurrealClient("surreal2");
        builder.AddKeyedSurrealClient("surreal3");

        using var host = builder.Build();

        var client1 = host.Services.GetRequiredService<SurrealDbClient>();
        var client2 = host.Services.GetRequiredKeyedService<SurrealDbClient>("surreal2");
        var client3 = host.Services.GetRequiredKeyedService<SurrealDbClient>("surreal3");

        Assert.NotSame(client1, client2);
        Assert.NotSame(client1, client3);
        Assert.NotSame(client2, client3);
    }

    [Fact]
    public void CanAddClientFromEncodedConnectionString()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>("ConnectionStrings:surreal1", "Endpoint=http://localhost:19530"),
            new KeyValuePair<string, string?>("ConnectionStrings:surreal2", "Endpoint=http://localhost:19531"),
        ]);

        builder.AddSurrealClient("surreal1");
        builder.AddKeyedSurrealClient("surreal2");

        using var host = builder.Build();

        var client1 = host.Services.GetRequiredService<SurrealDbClient>();
        var client2 = host.Services.GetRequiredKeyedService<SurrealDbClient>("surreal2");

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
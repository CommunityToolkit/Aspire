// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Components.Common.Tests;
using Marten;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Xunit;

namespace CommunityToolkit.Aspire.Marten.Tests;

public class AspireMartenClientExtensionsTest(PostgreSQLContainerFixture containerFixture) : IClassFixture<PostgreSQLContainerFixture>
{
    private const string DefaultConnectionName = "postgres";

    private string DefaultConnectionString =>
            RequiresDockerAttribute.IsSupported ? containerFixture.GetConnectionString() : "Host=localhost;Database=test_aspire_marten";

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [RequiresDocker]
    public async Task AddMartenClient_HealthCheckShouldBeRegisteredWhenEnabled(bool useKeyed)
    {
        var key = DefaultConnectionName;

        var builder = CreateBuilder(DefaultConnectionString);

        if (useKeyed)
        {
            builder.AddKeyedMartenClient(key, settings =>
            {
                settings.DisableHealthChecks = false;
            });
        }
        else
        {
            builder.AddMartenClient(DefaultConnectionName, settings =>
            {
                settings.DisableHealthChecks = false;
            });
        }

        using var host = builder.Build();

        var healthCheckService = host.Services.GetRequiredService<HealthCheckService>();

        var healthCheckReport = await healthCheckService.CheckHealthAsync();

        var healthCheckName = useKeyed ? $"Aspire.Marten_{key}" : "Aspire.Marten";

        Assert.Contains(healthCheckReport.Entries, x => x.Key == healthCheckName);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AddMartenClient_HealthCheckShouldNotBeRegisteredWhenDisabled(bool useKeyed)
    {
        var builder = CreateBuilder(DefaultConnectionString);

        if (useKeyed)
        {
            builder.AddKeyedMartenClient(DefaultConnectionName, settings =>
            {
                settings.DisableHealthChecks = true;
            });
        }
        else
        {
            builder.AddMartenClient(DefaultConnectionName, settings =>
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
            new KeyValuePair<string, string?>("ConnectionStrings:postgres1", "Host=localhost1;Database=test_aspire_marten"),
            new KeyValuePair<string, string?>("ConnectionStrings:postgres2", "Host=localhost2;Database=test_aspire_marten"),
            new KeyValuePair<string, string?>("ConnectionStrings:postgres3", "Host=localhost3;Database=test_aspire_marten"),
        ]);

        builder.AddMartenClient("postgres1");
        builder.AddKeyedMartenClient("postgres2");
        builder.AddKeyedMartenClient("postgres3");

        using var host = builder.Build();

        var documentStore1 = host.Services.GetRequiredService<DocumentStore>();
        var documentStore2 = host.Services.GetRequiredKeyedService<DocumentStore>("postgres2");
        var documentStore3 = host.Services.GetRequiredKeyedService<DocumentStore>("postgres3");

        Assert.NotSame(documentStore1, documentStore2);
        Assert.NotSame(documentStore1, documentStore3);
        Assert.NotSame(documentStore2, documentStore3);

        var documentSession1 = host.Services.GetRequiredService<IDocumentSession>();
        var documentSession2 = host.Services.GetRequiredKeyedService<IDocumentSession>("postgres2");
        var documentSession3 = host.Services.GetRequiredKeyedService<IDocumentSession>("postgres3");

        Assert.NotSame(documentSession1, documentSession2);
        Assert.NotSame(documentSession1, documentSession3);
        Assert.NotSame(documentSession2, documentSession3);

        var querySession1 = host.Services.GetRequiredService<IQuerySession>();
        var querySession2 = host.Services.GetRequiredKeyedService<IQuerySession>("postgres2");
        var querySession3 = host.Services.GetRequiredKeyedService<IQuerySession>("postgres3");

        Assert.NotSame(querySession1, querySession2);
        Assert.NotSame(querySession1, querySession3);
        Assert.NotSame(querySession2, querySession3);

        var npgsql1 = host.Services.GetRequiredKeyedService<NpgsqlDataSource>("Aspire.Marten");
        var npgsql2 = host.Services.GetRequiredKeyedService<NpgsqlDataSource>("Aspire.Marten_postgres2");
        var npgsql3 = host.Services.GetRequiredKeyedService<NpgsqlDataSource>("Aspire.Marten_postgres3");

        Assert.NotSame(npgsql1, npgsql2);
        Assert.NotSame(npgsql1, npgsql3);
        Assert.NotSame(npgsql2, npgsql3);

        Assert.Equal(builder.Configuration["ConnectionStrings:postgres1"], npgsql1.ConnectionString);
        Assert.Equal(builder.Configuration["ConnectionStrings:postgres2"], npgsql2.ConnectionString);
        Assert.Equal(builder.Configuration["ConnectionStrings:postgres3"], npgsql3.ConnectionString);

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

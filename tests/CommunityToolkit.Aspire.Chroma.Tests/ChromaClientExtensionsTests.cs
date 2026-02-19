using ChromaDB.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace CommunityToolkit.Aspire.Chroma.Tests;

public class AspireChromaClientExtensionsTests
{
    private const string DefaultConnectionName = "chroma";
    private const string DefaultConnectionString = "http://localhost:8000";

    [Fact]
    public void AddChromaClient_ShouldRegisterClient()
    {
        var builder = CreateBuilder();

        builder.AddChromaClient(DefaultConnectionName);

        using var host = builder.Build();

        var client = host.Services.GetService<ChromaClient>();
        Assert.NotNull(client);
    }

    [Fact]
    public void AddKeyedChromaClient_ShouldRegisterKeyedClient()
    {
        var builder = CreateBuilder();

        builder.AddKeyedChromaClient(DefaultConnectionName);

        using var host = builder.Build();

        var client = host.Services.GetKeyedService<ChromaClient>(DefaultConnectionName);
        Assert.NotNull(client);
    }

    [Fact]
    public void AddChromaClient_HealthCheckShouldBeRegistered()
    {
        var builder = CreateBuilder();

        builder.AddChromaClient(DefaultConnectionName);

        using var host = builder.Build();

        var healthCheckService = host.Services.GetRequiredService<HealthCheckService>();
        // The health check name should match the connection name for non-keyed
        var registration = host.Services.GetRequiredService<IEnumerable<HealthCheckRegistration>>();
        Assert.Contains(registration, x => x.Name == DefaultConnectionName);
    }

    [Fact]
    public void AddKeyedChromaClient_HealthCheckShouldBeRegisteredWithSuffix()
    {
        var builder = CreateBuilder();

        builder.AddKeyedChromaClient(DefaultConnectionName);

        using var host = builder.Build();

        var healthCheckService = host.Services.GetRequiredService<HealthCheckService>();
        // The health check name should have _check suffix for keyed
        var registration = host.Services.GetRequiredService<IEnumerable<HealthCheckRegistration>>();
        Assert.Contains(registration, x => x.Name == $"{DefaultConnectionName}_check");
    }

    private static HostApplicationBuilder CreateBuilder()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>($"ConnectionStrings:{DefaultConnectionName}", DefaultConnectionString)
        ]);
        builder.Services.AddHttpClient();
        return builder;
    }
}

using ChromaDB.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace CommunityToolkit.Aspire.Chroma.Tests;

public class ChromaClientExtensionsTests
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
    public async Task AddChromaClient_HealthCheckShouldBeRegistered()
    {
        var builder = CreateBuilder();

        builder.AddChromaClient(DefaultConnectionName);

        using var host = builder.Build();

        var healthCheckService = host.Services.GetRequiredService<HealthCheckService>();
        var healthCheckReport = await healthCheckService.CheckHealthAsync();
        Assert.Contains(healthCheckReport.Entries, x => x.Key == DefaultConnectionName);
    }

    [Fact]
    public async Task AddKeyedChromaClient_HealthCheckShouldBeRegisteredWithSuffix()
    {
        var builder = CreateBuilder();

        builder.AddKeyedChromaClient(DefaultConnectionName);

        using var host = builder.Build();

        var healthCheckService = host.Services.GetRequiredService<HealthCheckService>();
        var healthCheckReport = await healthCheckService.CheckHealthAsync();
        Assert.Contains(healthCheckReport.Entries, x => x.Key == $"{DefaultConnectionName}_check");
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

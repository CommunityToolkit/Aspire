using Aspire.Hosting;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Flyway.Tests;

public sealed class FlywayResourceBuilderExtensionsTests
{
    [Fact]
    public async Task WithTelemetryOptInSetsEnvironmentVariable()
    {
        var builder = DistributedApplication.CreateBuilder();

        var flywayResourceBuilder = builder
            .AddFlyway("flyway", "./migrations")
            .WithTelemetryOptIn();

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var retrievedFlywayResource = appModel.Resources.OfType<FlywayResource>().SingleOrDefault();
        Assert.NotNull(retrievedFlywayResource);

        var environmentVariableValues = await retrievedFlywayResource.GetEnvironmentVariablesAsync();
        Assert.Equal("false", environmentVariableValues["REDGATE_DISABLE_TELEMETRY"]);
    }
}

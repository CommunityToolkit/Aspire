using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Flyway.Tests;

public sealed class DistributedApplicationBuilderExtensionsTests
{
    [Fact]
    public async Task AddFlywayWithMigrationScriptsPathAddsFlywayWithDefaultConfigurations()
    {
        var builder = DistributedApplication.CreateBuilder();

        const string migrationScriptsPath = "./Migrations";
        var flywayResourceBuilder = builder.AddFlyway("flyway", migrationScriptsPath);

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var retrievedFlywayResource = appModel.Resources.OfType<FlywayResource>().SingleOrDefault();
        Assert.NotNull(retrievedFlywayResource);

        var flywayResource = flywayResourceBuilder.Resource;
        Assert.Equal(flywayResource.Name, retrievedFlywayResource.Name);

        var defaultFlywayConfiguration = new FlywayResourceConfiguration { MigrationScriptsPath = "/Whatever" };
        var containerImageAnnotation = retrievedFlywayResource.Annotations.OfType<ContainerImageAnnotation>().SingleOrDefault();
        Assert.NotNull(containerImageAnnotation);
        Assert.Equal(defaultFlywayConfiguration.ImageName, containerImageAnnotation.Image);
        Assert.Equal(defaultFlywayConfiguration.ImageTag, containerImageAnnotation.Tag);
        Assert.Equal(defaultFlywayConfiguration.ImageRegistry, containerImageAnnotation.Registry);

        var containerMountAnnotation = retrievedFlywayResource.Annotations.OfType<ContainerMountAnnotation>().SingleOrDefault();
        Assert.NotNull(containerMountAnnotation);
        Assert.Equal(Path.GetFullPath(migrationScriptsPath), containerMountAnnotation.Source);
        Assert.Equal(FlywayResource.MigrationScriptsDirectory, containerMountAnnotation.Target);

        var environmentVariableValues = await retrievedFlywayResource.GetEnvironmentVariableValuesAsync();
        Assert.Equal($"filesystem:{FlywayResource.MigrationScriptsDirectory}", environmentVariableValues["FLYWAY_LOCATIONS"]);
        Assert.Equal("true", environmentVariableValues["REDGATE_DISABLE_TELEMETRY"]);
    }

    [Fact]
    public async Task AddFlywayWithFlywayResourceConfigurationAddsFlywayWithConfigurations()
    {
        var builder = DistributedApplication.CreateBuilder();

        var flywayConfiguration = new FlywayResourceConfiguration
        {
            ImageName = "redgate/flyway",
            ImageTag = "11.20-azure-mongo",
            ImageRegistry = "ghcr.io",
            MigrationScriptsPath = "/Whatever",
        };

        var flywayResourceBuilder = builder.AddFlyway("flyway", flywayConfiguration);

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var retrievedFlywayResource = appModel.Resources.OfType<FlywayResource>().SingleOrDefault();
        Assert.NotNull(retrievedFlywayResource);

        var flywayResource = flywayResourceBuilder.Resource;
        Assert.Equal(flywayResource.Name, retrievedFlywayResource.Name);

        var containerImageAnnotation = retrievedFlywayResource.Annotations.OfType<ContainerImageAnnotation>().SingleOrDefault();
        Assert.NotNull(containerImageAnnotation);
        Assert.Equal(flywayConfiguration.ImageName, containerImageAnnotation.Image);
        Assert.Equal(flywayConfiguration.ImageTag, containerImageAnnotation.Tag);
        Assert.Equal(flywayConfiguration.ImageRegistry, containerImageAnnotation.Registry);

        var containerMountAnnotation = retrievedFlywayResource.Annotations.OfType<ContainerMountAnnotation>().SingleOrDefault();
        Assert.NotNull(containerMountAnnotation);
        Assert.Equal(Path.GetFullPath(flywayConfiguration.MigrationScriptsPath), containerMountAnnotation.Source);
        Assert.Equal(FlywayResource.MigrationScriptsDirectory, containerMountAnnotation.Target);

        var environmentVariableValues = await retrievedFlywayResource.GetEnvironmentVariableValuesAsync();
        Assert.Equal($"filesystem:{FlywayResource.MigrationScriptsDirectory}", environmentVariableValues["FLYWAY_LOCATIONS"]);
        Assert.Equal("true", environmentVariableValues["REDGATE_DISABLE_TELEMETRY"]);
    }
}

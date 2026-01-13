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

        var containerImageAnnotation = retrievedFlywayResource.Annotations.OfType<ContainerImageAnnotation>().SingleOrDefault();
        Assert.NotNull(containerImageAnnotation);
        Assert.Equal(FlywayContainerImageTags.Image, containerImageAnnotation.Image);
        Assert.Equal(FlywayContainerImageTags.Tag, containerImageAnnotation.Tag);
        Assert.Equal(FlywayContainerImageTags.Registry, containerImageAnnotation.Registry);

        var containerMountAnnotation = retrievedFlywayResource.Annotations.OfType<ContainerMountAnnotation>().SingleOrDefault();
        Assert.NotNull(containerMountAnnotation);
        Assert.Equal(Path.GetFullPath(migrationScriptsPath), containerMountAnnotation.Source);
        Assert.Equal(FlywayResource.MigrationScriptsDirectory, containerMountAnnotation.Target);

        var environmentVariableValues = await retrievedFlywayResource.GetEnvironmentVariableValuesAsync();
        Assert.Equal($"filesystem:{FlywayResource.MigrationScriptsDirectory}", environmentVariableValues["FLYWAY_LOCATIONS"]);
        Assert.Equal("true", environmentVariableValues["REDGATE_DISABLE_TELEMETRY"]);
    }

    [Fact]
    public async Task AddFlywayWithContainerConfigurationAddsFlywayWithContainerConfigurations()
    {
        var builder = DistributedApplication.CreateBuilder();

        const string image = "redgate/flyway";
        const string tag = "11.20-azure-mongo";
        const string registry = "ghcr.io";

        var flywayResourceBuilder = builder
            .AddFlyway("flyway", "/Whatever")
            .WithImageRegistry(registry)
            .WithImage(image)
            .WithImageTag(tag);

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var retrievedFlywayResource = appModel.Resources.OfType<FlywayResource>().SingleOrDefault();
        Assert.NotNull(retrievedFlywayResource);

        var flywayResource = flywayResourceBuilder.Resource;
        Assert.Equal(flywayResource.Name, retrievedFlywayResource.Name);

        var containerImageAnnotation = retrievedFlywayResource.Annotations.OfType<ContainerImageAnnotation>().SingleOrDefault();
        Assert.NotNull(containerImageAnnotation);
        Assert.Equal(image, containerImageAnnotation.Image);
        Assert.Equal(tag, containerImageAnnotation.Tag);
        Assert.Equal(registry, containerImageAnnotation.Registry);
    }
}

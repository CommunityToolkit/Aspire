using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.RavenDB.Tests;

public class AddRavenDBTests
{
    [Fact]
    public void AddRavenServerResource()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.AddRavenDB("ravenServer");
        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var serverResource = Assert.Single(appModel.Resources.OfType<RavenDBServerResource>());
        Assert.Equal("ravenServer", serverResource.Name);
    }

    [Fact]
    public void AddRavenServerAndDatabaseResource()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.AddRavenDB("ravenServer").AddDatabase(name: "ravenDatabase", databaseName: "TestDatabase");
        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var serverResource = Assert.Single(appModel.Resources.OfType<RavenDBServerResource>());
        var databaseResource = Assert.Single(appModel.Resources.OfType<RavenDBDatabaseResource>());

        Assert.Equal("ravenServer", serverResource.Name);
        Assert.Equal("ravenDatabase", databaseResource.Name);
        Assert.Equal("TestDatabase", databaseResource.DatabaseName);
        Assert.True(serverResource.Databases.TryGetValue("ravenDatabase", out var databaseName));
        Assert.Equal("TestDatabase", databaseName);
    }

    [Fact]
    public void VerifyNonDefaultImageTag()
    {
        var tag = "windows-latest-lts";

        var builder = DistributedApplication.CreateBuilder();
        builder.AddRavenDB("raven").WithImageTag(tag);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<RavenDBServerResource>());

        Assert.True(resource.TryGetAnnotationsOfType<ContainerImageAnnotation>(out var annotations));
        var annotation = Assert.Single(annotations);
        Assert.NotNull(annotation.Tag);
        Assert.Equal(tag, annotation.Tag);
    }

    [Fact]
    public void VerifyDefaultPort()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddRavenDB("raven");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<RavenDBServerResource>());

        var endpoint = Assert.Single(resource.Annotations.OfType<EndpointAnnotation>());

        Assert.Equal(8080, endpoint.TargetPort);
    }

    [Fact]
    public void SpecifiedDataVolumeNameIsUsed()
    {
        var builder = DistributedApplication.CreateBuilder();
        _ = builder.AddRavenDB("raven").WithDataVolume("data");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<RavenDBServerResource>());

        Assert.True(resource.TryGetAnnotationsOfType<ContainerMountAnnotation>(out var annotations));

        var annotation = Assert.Single(annotations);

        Assert.Equal("data", annotation.Source);
    }

    [Theory]
    [InlineData("data")]
    [InlineData(null)]
    public void CorrectTargetPathOnVolumeMount(string? volumeName)
    {
        var builder = DistributedApplication.CreateBuilder();
        _ = builder.AddRavenDB("raven").WithDataVolume(volumeName);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<RavenDBServerResource>());

        Assert.True(resource.TryGetAnnotationsOfType<ContainerMountAnnotation>(out var annotations));

        var annotation = Assert.Single(annotations);

        Assert.Equal("/var/lib/ravendb/data", annotation.Target);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReadOnlyVolumeMount(bool isReadOnly)
    {
        var builder = DistributedApplication.CreateBuilder();
        _ = builder.AddRavenDB("raven").WithDataVolume(isReadOnly: isReadOnly);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<RavenDBServerResource>());

        Assert.True(resource.TryGetAnnotationsOfType<ContainerMountAnnotation>(out var annotations));

        var annotation = Assert.Single(annotations);

        Assert.Equal(isReadOnly, annotation.IsReadOnly);
    }
}

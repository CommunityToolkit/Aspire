using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Supabase.Tests;

public class AddSupabaseTests
{
    [Fact]
    public void AddSupabaseResource()
    {
        IDistributedApplicationBuilder appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.AddAllSupabase("supabase");
        using DistributedApplication app = appBuilder.Build();

        DistributedApplicationModel appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        SupabaseResource serverResource = Assert.Single(appModel.Resources.OfType<SupabaseResource>());
        Assert.Equal("supabase", serverResource.Name);
    }

    /*[Fact]
    public void AddRavenServerAndDatabaseResource()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.AddRavenDB("ravenServer")
            .AddDatabase(name: "ravenDatabase", databaseName: "TestDatabase");
        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var serverResource = Assert.Single(appModel.Resources.OfType<RavenDBServerResource>());
        var databaseResource = Assert.Single(appModel.Resources.OfType<RavenDBDatabaseResource>());

        Assert.Equal("ravenServer", serverResource.Name);
        Assert.Equal("ravenDatabase", databaseResource.Name);
        Assert.Equal("TestDatabase", databaseResource.DatabaseName);
        Assert.True(serverResource.Databases.TryGetValue("ravenDatabase", out var databaseName));
        Assert.Equal("TestDatabase", databaseName);
    }*/

    [Fact]
    public void VerifyDefaultPorts()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddAllSupabase("supabase");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<SupabaseResource>());

        var endpoint = Assert.Single(resource.Annotations.OfType<EndpointAnnotation>(), x => x.Name == resource.PrimaryEndpoint.EndpointName);
        var databaseEndpoint = Assert.Single(resource.Annotations.OfType<EndpointAnnotation>(), x => x.Name == resource.DatabaseEndpoint.EndpointName);

        Assert.Equal(8080, endpoint.TargetPort);
        Assert.Equal(38888, databaseEndpoint.TargetPort);
    }

    /*[Fact]
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
    }*/
}

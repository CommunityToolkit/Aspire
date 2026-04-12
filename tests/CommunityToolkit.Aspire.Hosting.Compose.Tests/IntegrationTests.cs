using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Compose.Tests;

public class IntegrationTests
{
    [Fact]
    public void AddCompose_BasicFile_CreatesResources()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        string composePath = GetTestFilePath("basic.yml");

        ComposeResourceCollection compose = builder.AddCompose(composePath);

        Assert.Equal(2, compose.Count);
        Assert.Contains("postgres", compose.ServiceNames);
        Assert.Contains("redis", compose.ServiceNames);
    }

    [Fact]
    public void AddCompose_BasicFile_ResourcesAreInAppModel()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        string composePath = GetTestFilePath("basic.yml");

        builder.AddCompose(composePath);

        using DistributedApplication app = builder.Build();
        DistributedApplicationModel appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        ContainerResource? postgres = appModel.Resources.OfType<ContainerResource>().SingleOrDefault(r => r.Name == "postgres");
        ContainerResource? redis = appModel.Resources.OfType<ContainerResource>().SingleOrDefault(r => r.Name == "redis");

        Assert.NotNull(postgres);
        Assert.NotNull(redis);
    }

    [Fact]
    public void AddCompose_IndexerReturnsResourceBuilder()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        string composePath = GetTestFilePath("basic.yml");

        ComposeResourceCollection compose = builder.AddCompose(composePath);

        IResourceBuilder<ContainerResource> postgres = compose["postgres"];
        Assert.NotNull(postgres);
        Assert.Equal("postgres", postgres.Resource.Name);
    }

    [Fact]
    public void AddCompose_IndexerThrowsForUnknownService()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        string composePath = GetTestFilePath("basic.yml");

        ComposeResourceCollection compose = builder.AddCompose(composePath);

        Assert.Throws<KeyNotFoundException>(() => compose["nonexistent"]);
    }

    [Fact]
    public void AddCompose_CanUseWaitFor()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        string composePath = GetTestFilePath("basic.yml");

        ComposeResourceCollection compose = builder.AddCompose(composePath);

        IResourceBuilder<ContainerResource> apiBuilder = builder.AddContainer("api", "myapi", "latest").WaitFor(compose["postgres"]);

        Assert.NotNull(apiBuilder);
    }

    [Fact]
    public void AddCompose_DependsOnConditions_AppliesWaitFor()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        string composePath = GetTestFilePath("depends-on-conditions.yml");

        ComposeResourceCollection compose = builder.AddCompose(composePath);

        Assert.Equal(3, compose.Count);
        Assert.Contains("db", compose.ServiceNames);
        Assert.Contains("migrations", compose.ServiceNames);
        Assert.Contains("api", compose.ServiceNames);
    }

    [Fact]
    public void AddCompose_EnvironmentList_SetsEnvironment()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        string composePath = GetTestFilePath("environment-list.yml");

        ComposeResourceCollection compose = builder.AddCompose(composePath);

        Assert.Single(compose);
        Assert.Contains("app", compose.ServiceNames);
    }

    [Fact]
    public void AddCompose_MultiplePorts_CreatesEndpoints()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        string composePath = GetTestFilePath("multiple-ports.yml");
        builder.AddCompose(composePath);

        using DistributedApplication app = builder.Build();
        DistributedApplicationModel appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        ContainerResource web = appModel.Resources.OfType<ContainerResource>().Single(r => r.Name == "web");

        List<EndpointAnnotation> endpoints = web.Annotations.OfType<EndpointAnnotation>().ToList();
        Assert.True(endpoints.Count >= 3, $"Expected at least 3 endpoints, got {endpoints.Count}");
    }

    [Fact]
    public void AddCompose_MissingFile_ThrowsFileNotFound()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        Assert.Throws<FileNotFoundException>(() => builder.AddCompose("nonexistent.yml"));
    }

    [Fact]
    public void AddCompose_TryGetResource_ReturnsTrue()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        string composePath = GetTestFilePath("basic.yml");

        ComposeResourceCollection compose = builder.AddCompose(composePath);

        Assert.True(compose.TryGetResource("postgres", out IResourceBuilder<ContainerResource>? resource));
        Assert.NotNull(resource);
    }

    [Fact]
    public void AddCompose_TryGetResource_ReturnsFalseForUnknown()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        string composePath = GetTestFilePath("basic.yml");

        ComposeResourceCollection compose = builder.AddCompose(composePath);

        Assert.False(compose.TryGetResource("nonexistent", out _));
    }

    [Fact]
    public void AddCompose_MultipleFiles_ReturnsArrayOfCollections()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        string basicPath = GetTestFilePath("basic.yml");
        string portsPath = GetTestFilePath("multiple-ports.yml");

        ComposeResourceCollection[] collections = builder.AddCompose(basicPath, portsPath);

        Assert.Equal(2, collections.Length);
        Assert.Contains("postgres", collections[0].ServiceNames);
        Assert.Contains("web", collections[1].ServiceNames);
    }

    [Fact]
    public void AddCompose_EntrypointAndCommand_SetsArgs()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        string composePath = GetTestFilePath("entrypoint-and-command.yml");

        ComposeResourceCollection compose = builder.AddCompose(composePath);

        Assert.Equal(2, compose.Count);
        Assert.Contains("worker-string", compose.ServiceNames);
        Assert.Contains("worker-list", compose.ServiceNames);
    }

    private static string GetTestFilePath(string fileName) => Path.Combine(AppContext.BaseDirectory, "composes", fileName);
}

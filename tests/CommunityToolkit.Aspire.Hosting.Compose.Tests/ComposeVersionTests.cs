using CommunityToolkit.Aspire.Hosting.Compose.Parsing;
using Aspire.Hosting;
using CommunityToolkit.Aspire.Hosting.Compose.Parsing.Contracts;

namespace CommunityToolkit.Aspire.Hosting.Compose.Tests;

/// <summary>
/// Tests for all Docker Compose file format versions:
/// v1 (legacy, services at top level), v2.x, v3.x, and modern Compose Spec (no version field).
/// </summary>
public class ComposeVersionTests
{

    [Fact]
    public void Parse_V1Format_DetectsServicesAtTopLevel()
    {
        string composePath = GetTestFilePath("v1-format.yml");
        ComposeFile composeFile = ComposeParser.Parse(composePath);

        Assert.Equal(2, composeFile.Services.Count);
        Assert.Contains("postgres", composeFile.Services.Keys);
        Assert.Contains("redis", composeFile.Services.Keys);
    }

    [Fact]
    public void Parse_V1Format_ParsesImageCorrectly()
    {
        string composePath = GetTestFilePath("v1-format.yml");
        ComposeFile composeFile = ComposeParser.Parse(composePath);

        Assert.Equal("postgres:14", composeFile.Services["postgres"].Image);
        Assert.Equal("redis:6", composeFile.Services["redis"].Image);
    }

    [Fact]
    public void Parse_V1Format_ParsesPortsAndEnvironment()
    {
        string composePath = GetTestFilePath("v1-format.yml");
        ComposeFile composeFile = ComposeParser.Parse(composePath);

        Assert.NotNull(composeFile.Services["postgres"].Ports);
        Assert.Single(composeFile.Services["postgres"].Ports!);
        Assert.NotNull(composeFile.Services["postgres"].Environment);
    }

    [Fact]
    public void AddCompose_V1Format_CreatesResources()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        string composePath = GetTestFilePath("v1-format.yml");

        ComposeResourceCollection compose = builder.AddCompose(composePath);

        Assert.Equal(2, compose.Count);
        Assert.Equal("postgres", compose["postgres"].Resource.Name);
        Assert.Equal("redis", compose["redis"].Resource.Name);
    }


    [Fact]
    public void Parse_V2Format_ParsesVersionField()
    {
        string composePath = GetTestFilePath("v2-format.yml");
        ComposeFile composeFile = ComposeParser.Parse(composePath);

        Assert.Equal("2.4", composeFile.Version);
    }

    [Fact]
    public void Parse_V2Format_ParsesServices()
    {
        string composePath = GetTestFilePath("v2-format.yml");
        ComposeFile composeFile = ComposeParser.Parse(composePath);

        Assert.Equal(2, composeFile.Services.Count);
        Assert.Contains("postgres", composeFile.Services.Keys);
        Assert.Contains("redis", composeFile.Services.Keys);
    }

    [Fact]
    public void Parse_V2Format_ParsesVolumes()
    {
        string composePath = GetTestFilePath("v2-format.yml");
        ComposeFile composeFile = ComposeParser.Parse(composePath);

        Assert.NotNull(composeFile.Volumes);
        Assert.Contains("pgdata", composeFile.Volumes.Keys);
    }

    [Fact]
    public void AddCompose_V2Format_CreatesResources()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        string composePath = GetTestFilePath("v2-format.yml");

        ComposeResourceCollection compose = builder.AddCompose(composePath);

        Assert.Equal(2, compose.Count);

        using DistributedApplication app = builder.Build();
        DistributedApplicationModel appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        ContainerResource postgres = appModel.Resources.OfType<ContainerResource>().Single(r => r.Name == "postgres");
        Assert.NotNull(postgres);
    }


    [Fact]
    public void Parse_V3Format_ParsesVersionField()
    {
        string composePath = GetTestFilePath("v3-format.yml");
        ComposeFile composeFile = ComposeParser.Parse(composePath);

        Assert.Equal("3.8", composeFile.Version);
    }

    [Fact]
    public void Parse_V3Format_ParsesAllServices()
    {
        string composePath = GetTestFilePath("v3-format.yml");
        ComposeFile composeFile = ComposeParser.Parse(composePath);

        Assert.Equal(3, composeFile.Services.Count);
        Assert.Contains("web", composeFile.Services.Keys);
        Assert.Contains("api", composeFile.Services.Keys);
        Assert.Contains("db", composeFile.Services.Keys);
    }

    [Fact]
    public void Parse_V3Format_ParsesDependsOnWithConditions()
    {
        string composePath = GetTestFilePath("v3-format.yml");
        ComposeFile composeFile = ComposeParser.Parse(composePath);

        Assert.NotNull(composeFile.Services["api"].DependsOn);
        Assert.NotNull(composeFile.Services["db"].Healthcheck);
    }

    [Fact]
    public void AddCompose_V3Format_CreatesResourcesWithDependencies()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        string composePath = GetTestFilePath("v3-format.yml");

        ComposeResourceCollection compose = builder.AddCompose(composePath);

        Assert.Equal(3, compose.Count);
        Assert.Equal("web", compose["web"].Resource.Name);
        Assert.Equal("api", compose["api"].Resource.Name);
        Assert.Equal("db", compose["db"].Resource.Name);
    }


    [Fact]
    public void Parse_ModernFormat_NoVersionField()
    {
        string composePath = GetTestFilePath("modern-no-version.yml");
        ComposeFile composeFile = ComposeParser.Parse(composePath);

        Assert.Null(composeFile.Version);
    }

    [Fact]
    public void Parse_ModernFormat_ParsesAllServices()
    {
        string composePath = GetTestFilePath("modern-no-version.yml");
        ComposeFile composeFile = ComposeParser.Parse(composePath);

        Assert.Equal(3, composeFile.Services.Count);
        Assert.Contains("app", composeFile.Services.Keys);
        Assert.Contains("db", composeFile.Services.Keys);
        Assert.Contains("cache", composeFile.Services.Keys);
    }

    [Fact]
    public void Parse_ModernFormat_ParsesHealthcheckStartPeriod()
    {
        string composePath = GetTestFilePath("modern-no-version.yml");
        ComposeFile composeFile = ComposeParser.Parse(composePath);

        Assert.NotNull(composeFile.Services["db"].Healthcheck);
        Assert.Equal("10s", composeFile.Services["db"].Healthcheck!.StartPeriod);
        Assert.Equal(10, composeFile.Services["db"].Healthcheck!.Retries);
    }

    [Fact]
    public void AddCompose_ModernFormat_CreatesResources()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        string composePath = GetTestFilePath("modern-no-version.yml");

        ComposeResourceCollection compose = builder.AddCompose(composePath);

        Assert.Equal(3, compose.Count);

        using DistributedApplication app = builder.Build();
        DistributedApplicationModel appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        Assert.Equal(3, appModel.Resources.OfType<ContainerResource>().Count());
    }


    [Fact]
    public void AllFormats_ProduceValidAspireResources()
    {
        string[] files = ["v1-format.yml", "v2-format.yml", "v3-format.yml", "modern-no-version.yml"];

        foreach (string file in files)
        {
            IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
            string composePath = GetTestFilePath(file);

            ComposeResourceCollection compose = builder.AddCompose(composePath);

            Assert.True(compose.Count > 0, $"File '{file}' should produce at least one resource");

            Assert.True(compose.TryGetResource("postgres", out _) || compose.TryGetResource("db", out _),$"File '{file}' should contain a postgres/db service");
        }
    }

    private static string GetTestFilePath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "composes", fileName);
}

using CommunityToolkit.Aspire.Hosting.Compose.Mapping;
using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Compose.Tests;

public class HealthcheckTests
{
    [Fact]
    public void AddCompose_WithHealthcheck_AddsHealthCheckAnnotation()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        string composePath = GetTestFilePath("healthcheck.yml");

        builder.AddCompose(composePath);

        using DistributedApplication app = builder.Build();
        DistributedApplicationModel appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        ContainerResource db = appModel.Resources.OfType<ContainerResource>().Single(r => r.Name == "db");
        List<HealthCheckAnnotation> healthChecks = db.Annotations.OfType<HealthCheckAnnotation>().ToList();

        Assert.Single(healthChecks);
        Assert.Equal("compose-db", healthChecks[0].Key);
    }

    [Fact]
    public void AddCompose_WithCmdHealthcheck_AddsHealthCheckAnnotation()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        string composePath = GetTestFilePath("healthcheck.yml");

        builder.AddCompose(composePath);

        using DistributedApplication app = builder.Build();
        DistributedApplicationModel appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        ContainerResource cache = appModel.Resources.OfType<ContainerResource>().Single(r => r.Name == "cache");
        List<HealthCheckAnnotation> healthChecks = cache.Annotations.OfType<HealthCheckAnnotation>().ToList();

        Assert.Single(healthChecks);
        Assert.Equal("compose-cache", healthChecks[0].Key);
    }

    [Fact]
    public void AddCompose_WithDisabledHealthcheck_NoHealthCheckAnnotation()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        string composePath = GetTestFilePath("healthcheck.yml");

        builder.AddCompose(composePath);

        using DistributedApplication app = builder.Build();
        DistributedApplicationModel appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        ContainerResource worker = appModel.Resources.OfType<ContainerResource>().Single(r => r.Name == "worker");
        List<HealthCheckAnnotation> healthChecks = worker.Annotations.OfType<HealthCheckAnnotation>().ToList();

        Assert.Empty(healthChecks);
    }

    [Theory]
    [InlineData("10s", 10)]
    [InlineData("5s", 5)]
    [InlineData("30s", 30)]
    [InlineData("1.5s", 1.5)]
    [InlineData("2m", 120)]
    [InlineData("500ms", 0.5)]
    public void ParseDuration_ValidFormats_ReturnsCorrectTimeSpan(string input, double expectedSeconds)
    {
        TimeSpan? result = HealthcheckMapper.ParseDuration(input);

        Assert.NotNull(result);
        Assert.Equal(expectedSeconds, result.Value.TotalSeconds, precision: 1);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid")]
    public void ParseDuration_InvalidFormats_ReturnsNull(string? input)
    {
        TimeSpan? result = HealthcheckMapper.ParseDuration(input);
        Assert.Null(result);
    }

    private static string GetTestFilePath(string fileName) => Path.Combine(AppContext.BaseDirectory, "composes", fileName);
}

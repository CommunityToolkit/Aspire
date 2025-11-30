using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Logto.Tests;

public class ResourceCreationTests
{
    [Fact]
    public void LogtoResourceGetsAdded()
    {
        var builder = DistributedApplication.CreateBuilder();

        var postgres = builder.AddPostgres("postgres");

        builder.AddLogtoContainer("logto", postgres, "logtoDb");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<LogtoContainerResource>());

        Assert.Equal("logto", resource.Name);
    }

    [Fact]
    public void LogtoResourceHealthChecks()
    {
        var builder = DistributedApplication.CreateBuilder();

        var postgres = builder.AddPostgres("postgres");

        builder.AddLogtoContainer("logto", postgres, "logtoDb");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<LogtoContainerResource>());

        var result = resource.TryGetAnnotationsOfType<HealthCheckAnnotation>(out var annotations);
        Assert.True(result);
        Assert.NotNull(annotations);
    }
}
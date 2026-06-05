using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Logto.Tests;

public class ResourceCreationTests
{
    [Fact]
    public void LogtoResourceGetsAdded()
    {
        var builder = DistributedApplication.CreateBuilder();

        var postgres = builder.AddPostgres("postgres");

        builder.AddLogto("logto", postgres);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<LogtoResource>());

        Assert.Equal("logto", resource.Name);
    }

    [Fact]
    public void LogtoResourceHealthChecks()
    {
        var builder = DistributedApplication.CreateBuilder();

        var postgres = builder.AddPostgres("postgres");

        builder.AddLogto("logto", postgres);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<LogtoResource>());

        var result = resource.TryGetAnnotationsOfType<HealthCheckAnnotation>(out var annotations);
        Assert.True(result);
        Assert.NotNull(annotations);
    }

    [Fact]
    public void LogtoResourceDoesNotOverrideEntrypointByDefault()
    {
        var builder = DistributedApplication.CreateBuilder();

        var postgres = builder.AddPostgres("postgres");

        builder.AddLogto("logto", postgres);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<LogtoResource>());

        Assert.False(resource.TryGetAnnotationsOfType<CommandLineArgsCallbackAnnotation>(out _));
    }

    [Fact]
    public void LogtoResourceCanUseSeededStartup()
    {
        var builder = DistributedApplication.CreateBuilder();

        var postgres = builder.AddPostgres("postgres");

        builder.AddLogto("logto", postgres)
            .WithDatabaseSeeding();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<LogtoResource>());

        Assert.True(resource.TryGetAnnotationsOfType<CommandLineArgsCallbackAnnotation>(out _));
    }
}

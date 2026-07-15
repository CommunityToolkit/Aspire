using Aspire.Hosting;
using Aspire.Hosting.Utils;
using CommunityToolkit.Aspire.Testing;

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
    public void AdminEndpointIsNotExportedAsAReferenceEndpoint()
    {
        var builder = DistributedApplication.CreateBuilder();
        var postgres = builder.AddPostgres("postgres");

        var logto = builder.AddLogto("logto", postgres);

        var adminEndpoint = Assert.Single(logto.Resource.Annotations.OfType<EndpointAnnotation>(),
            endpoint => endpoint.Name == "admin");
        Assert.True(adminEndpoint.ExcludeReferenceEndpoint);
    }

    [Fact]
    public async Task LogtoResourceUsesOneShotDatabaseSetup()
    {
        var builder = DistributedApplication.CreateBuilder();

        var postgres = builder.AddPostgres("postgres");

        builder.AddLogto("logto", postgres)
            .WithDatabaseSeeding();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<LogtoResource>());
        var setup = Assert.Single(appModel.Resources.OfType<ContainerResource>(), candidate =>
            candidate.Name == "logto-database-setup");

        Assert.False(resource.TryGetAnnotationsOfType<CommandLineArgsCallbackAnnotation>(out _));
        Assert.Equal(["-c", "npm run cli db seed -- --swe && npm run alteration deploy latest"], await setup.GetArgumentListAsync());
        Assert.Contains(ManifestPublishingCallbackAnnotation.Ignore, setup.Annotations);
        Assert.Contains(resource.Annotations.OfType<WaitAnnotation>(), wait =>
            wait.Resource == setup && wait.WaitType == WaitType.WaitForCompletion);
    }

    [Fact]
    public async Task LogtoDatabaseSetupCanDisablePwnedPasswordCheck()
    {
        var builder = DistributedApplication.CreateBuilder();
        var postgres = builder.AddPostgres("postgres");

        builder.AddLogto("logto", postgres)
            .WithDatabaseSeeding(disableAdminPwnedPasswordCheck: true);

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var setup = Assert.Single(appModel.Resources.OfType<ContainerResource>(), candidate =>
            candidate.Name == "logto-database-setup");

        Assert.Equal(["-c", "npm run cli db seed -- --swe --dapc && npm run alteration deploy latest"], await setup.GetArgumentListAsync());
    }

    [Fact]
    public void LogtoDatabaseSetupIsIdempotent()
    {
        var builder = DistributedApplication.CreateBuilder();
        var postgres = builder.AddPostgres("postgres");

        var logto = builder.AddLogto("logto", postgres);
        logto.WithDatabaseSeeding();
        logto.WithDatabaseSeeding();

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        Assert.Single(appModel.Resources.OfType<ContainerResource>(), candidate =>
            candidate.Name == "logto-database-setup");
        Assert.Single(logto.Resource.Annotations.OfType<WaitAnnotation>(), wait =>
            wait.WaitType == WaitType.WaitForCompletion);
    }

    [Fact]
    public void LogtoDatabaseSetupIsNotAddedInPublishMode()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var postgres = builder.AddPostgres("postgres");

        var logto = builder.AddLogto("logto", postgres)
            .WithDatabaseSeeding();

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        Assert.DoesNotContain(appModel.Resources, resource => resource.Name == "logto-database-setup");
        Assert.DoesNotContain(logto.Resource.Annotations.OfType<WaitAnnotation>(), wait =>
            wait.WaitType == WaitType.WaitForCompletion);
    }
}

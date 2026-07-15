using Aspire.Hosting;

using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Logto.Tests;

public class ResourceCreationTests
{
    [Fact]
    public async Task LogtoResourceUsesAllocatedPublicEndpoints()
    {
        var builder = DistributedApplication.CreateBuilder();
        var postgres = builder.AddPostgres("postgres");

        var logto = builder.AddLogto("logto", postgres)
            .WithEndpoint("http", endpoint =>
                endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, "localhost", 49101))
            .WithEndpoint("admin", endpoint =>
                endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, "localhost", 49102));

        var environment = await logto.Resource.GetEnvironmentVariablesAsync();

        Assert.Equal("http://localhost:49101", environment["ENDPOINT"]);
        Assert.Equal("http://127.0.0.1:49102", environment["ADMIN_ENDPOINT"]);
    }

    [Fact]
    public async Task ExplicitAdminEndpointOverridesAllocatedEndpoint()
    {
        var builder = DistributedApplication.CreateBuilder();
        var postgres = builder.AddPostgres("postgres");

        var logto = builder.AddLogto("logto", postgres)
            .WithEndpoint("http", endpoint =>
                endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, "localhost", 49101))
            .WithEndpoint("admin", endpoint =>
                endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, "localhost", 49102))
            .WithAdminEndpoint("https://admin.example.com");

        var environment = await logto.Resource.GetEnvironmentVariablesAsync();

        Assert.Equal("https://admin.example.com", environment["ADMIN_ENDPOINT"]);
    }

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
}

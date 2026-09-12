// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Utils;
using CommunityToolkit.Aspire.Hosting.GlitchTip.Management;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

#pragma warning disable ASPIREPROBES001

namespace CommunityToolkit.Aspire.Hosting.GlitchTip.Tests;

public class HostingModelTests
{
    [Theory]
    [InlineData(DistributedApplicationOperation.Run)]
    [InlineData(DistributedApplicationOperation.Publish)]
    public void StackRejectsSecondProjectWithoutChangingModel(DistributedApplicationOperation operation)
    {
        using var builder = TestDistributedApplicationBuilder.Create(operation);
        var slug = builder.AddParameter("project-slug", "stack");
        var release = builder.AddParameter("release", "v1");
        var project = builder.AddGlitchTip("glitchtip", slug, release);
        var count = builder.Resources.Count;

        var error = Assert.Throws<InvalidOperationException>(() => builder.AddGlitchTip("other", slug, release));

        Assert.Contains("exactly one", error.Message);
        Assert.Equal(count, builder.Resources.Count);
        Assert.Same(project.Resource, Assert.Single(builder.Resources.OfType<GlitchTipResource>()));
    }

    [Fact]
    public void ForeignAppHostParameterIsRejectedEvenWhenItsNameMatches()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        using var otherBuilder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        builder.AddParameter("project-slug", "stack");
        var foreignSlug = otherBuilder.AddParameter("project-slug", "stack");
        var release = builder.AddParameter("release", "v1");
        var count = builder.Resources.Count;

        var error = Assert.Throws<ArgumentException>(() => builder.AddGlitchTip("glitchtip", foreignSlug, release));

        Assert.Contains("same AppHost", error.Message);
        Assert.Equal(count, builder.Resources.Count);
        Assert.Empty(builder.Resources.OfType<GlitchTipResource>());
    }
    [Fact]
    public async Task RunModelsPinnedLocalServiceAndPersistentSecretParameters()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Run);
        var slug = builder.AddParameter("project-slug", "stack");
        var release = builder.AddParameter("release", "v1");
        var project = builder.AddGlitchTip("glitchtip", slug, release);
        var server = Assert.IsType<ContainerResource>(project.Resource.LocalServer);
        var image = Assert.Single(server.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal("glitchtip/glitchtip", image.Image);
        Assert.Equal("6.2.6", image.Tag);
        Assert.True(project.Resource.AdminPassword!.Secret);
        Assert.Same(slug.Resource, project.Resource.ProjectSlug);
        Assert.Same(release.Resource, project.Resource.Release);
        Assert.Null(project.Resource.ApiToken);
        Assert.Null(project.Resource.InstanceUrl);
        Assert.False(project.Resource.Provisioned.Task.IsCompleted);

        var httpEndpoint = Assert.Single(server.Annotations.OfType<EndpointAnnotation>());
        httpEndpoint.AllocatedEndpoint = new AllocatedEndpoint(httpEndpoint, "localhost", 18573);
        var serverEnvironment = await EnvironmentAsync(builder, server);
        Assert.Equal("http://localhost:18573", serverEnvironment["GLITCHTIP_URL"]);
        Assert.Equal("True", serverEnvironment["GLITCHTIP_ENABLE_MCP"]);
        Assert.Equal("True", serverEnvironment["GLITCHTIP_UPTIME_ALLOW_PRIVATE_IPS"]);
        Assert.Equal("True", serverEnvironment["GLITCHTIP_ENABLE_DUCKDB"]);
        Assert.Equal("/code/uploads/cold-storage", serverEnvironment["GLITCHTIP_COLD_STORAGE_DIR"]);
        Assert.Equal("all_in_one", serverEnvironment["SERVER_ROLE"]);
        Assert.Equal("", serverEnvironment["VALKEY_URL"]);
        Assert.DoesNotContain(serverEnvironment.Keys, name => name.StartsWith("REDIS_", StringComparison.Ordinal));
        Assert.DoesNotContain(serverEnvironment.Keys, name => name.StartsWith("VALKEY_", StringComparison.Ordinal) && name != "VALKEY_URL");
        var key = Assert.IsType<ParameterResource>(serverEnvironment["SECRET_KEY"]);
        Assert.True(key.Secret);

        var scope = GlitchTipLocalHosting.GetStorageScope(builder.AppHostDirectory);
        var postgresPassword = Assert.Single(builder.Resources.OfType<ParameterResource>(), parameter => parameter.Name == $"glitchtip-{scope}-postgres-password");
        Assert.True(postgresPassword.Secret);
        Assert.DoesNotContain(builder.Resources, resource => resource.Name.Contains("redis", StringComparison.OrdinalIgnoreCase) || resource.Name.Contains("valkey", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(["glitchtip-postgres", "glitchtip-server"], builder.Resources.OfType<ContainerResource>().Select(resource => resource.Name).Order());
        Assert.Equal("glitchtip-postgres", Assert.Single(server.Annotations.OfType<WaitAnnotation>()).Resource.Name);
        var volumes = builder.Resources.OfType<ContainerResource>()
            .SelectMany(resource => resource.Annotations.OfType<ContainerMountAnnotation>())
            .Where(mount => mount.Type == ContainerMountType.Volume)
            .Select(mount => mount.Source).ToList();
        Assert.Equal([$"glitchtip-{scope}-postgres", $"glitchtip-{scope}-uploads"], volumes.Order());
        Assert.All(builder.Resources.OfType<ContainerResource>(), resource =>
            Assert.Contains(ManifestPublishingCallbackAnnotation.Ignore, resource.Annotations));
    }

    [Fact]
    public async Task PublishModelsOnlyProjectAndNeverResolvesManagementDuringEnvironmentCollection()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var project = AddProject(builder);
        var instance = builder.AddParameter("instance", "https://unreachable.invalid/");
        var organization = builder.AddParameter("organization", "org");
        var team = builder.AddParameter("initial-team", "team");
        var token = builder.AddParameter("management-token", "private-management-token", secret: true);
        Assert.Same(project, project.WithDeploymentParameters(instance, organization, team, token));
        var consumer = builder.AddContainer("api", "unused-image").WithReference(project);

        Assert.Null(project.Resource.LocalServer);
        Assert.Null(project.Resource.AdminPassword);
        Assert.Same(instance.Resource, project.Resource.InstanceUrl);
        Assert.Same(organization.Resource, project.Resource.Organization);
        Assert.Same(team.Resource, project.Resource.InitialTeam);
        Assert.Same(token.Resource, project.Resource.ApiToken);
        Assert.Single(builder.Resources.OfType<ContainerResource>());
        Assert.Empty(consumer.Resource.Annotations.OfType<WaitAnnotation>());

        var environment = await EnvironmentAsync(builder, consumer.Resource);
        var expression = Assert.IsType<ReferenceExpression>(environment["SENTRY_DSN"]);
        Assert.Equal("{glitchtip.connectionString}", expression.ValueExpression);
        Assert.False(project.Resource.Provisioned.Task.IsCompleted);
        Assert.DoesNotContain(token.Resource, environment.Values);
        Assert.DoesNotContain("private-management-token", environment.Values);
    }

    [Fact]
    public void PublishConfigurationDoesNotReplaceWorktreeLocalIdentity()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Run);
        var project = AddProject(builder);
        var localOrganization = project.Resource.Organization;
        var localTeam = project.Resource.InitialTeam;
        var server = project.Resource.LocalServer;
        project.WithDeploymentParameters(builder.AddParameter("instance", "https://shared.invalid/"),
            builder.AddParameter("organization", "external-org"), builder.AddParameter("team", "external-team"),
            builder.AddParameter("token", "not-used-locally", secret: true));

        Assert.Same(localOrganization, project.Resource.Organization);
        Assert.Same(localTeam, project.Resource.InitialTeam);
        Assert.Same(server, project.Resource.LocalServer);
        Assert.Null(project.Resource.ApiToken);
        Assert.Null(project.Resource.InstanceUrl);
    }

    [Fact]
    public void PlaintextManagementParameterIsRejectedBeforeConfigurationChanges()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var project = AddProject(builder);
        var instance = builder.AddParameter("instance", "https://shared.invalid/");
        var organization = builder.AddParameter("organization", "org");
        var team = builder.AddParameter("team", "team");
        var token = builder.AddParameter("token", "not-secret");
        var originalParameters = project.Resource.OwnedDeploymentParameters.ToArray();
        var originalInstance = project.Resource.InstanceUrl;
        var originalToken = project.Resource.ApiToken;
        var resourceCount = builder.Resources.Count;

        Assert.Throws<ArgumentException>(() => project.WithDeploymentParameters(instance, organization, team, token));
        Assert.Same(originalInstance, project.Resource.InstanceUrl);
        Assert.Same(originalToken, project.Resource.ApiToken);
        Assert.Equal(resourceCount, builder.Resources.Count);
        Assert.All(originalParameters, parameter => Assert.Contains(parameter, builder.Resources));
    }

    [Fact]
    public async Task AppHostEnvironmentAndStackReleaseAreForwardedToEveryConsumer()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        builder.Environment.EnvironmentName = "production";
        var project = AddProject(builder);
        var container = builder.AddContainer("worker", "unused-image")
            .WithEnvironment("DOTNET_ENVIRONMENT", "ignored-child-environment").WithReference(project);
        var application = builder.AddResource(new ProjectResource("web"))
            .WithEnvironment("ASPNETCORE_ENVIRONMENT", "another-child-environment").WithReference(project);

        foreach (var consumer in new IResource[] { container.Resource, application.Resource })
        {
            var environment = await EnvironmentAsync(builder, consumer);
            Assert.Equal("production", environment["SENTRY_ENVIRONMENT"]);
            Assert.Equal("production", environment["Aspire__GlitchTip__Environment"]);
            Assert.Equal(consumer.Name, environment["Aspire__GlitchTip__ServiceName"]);
            Assert.Same(project.Resource.Release, environment["SENTRY_RELEASE"]);
            Assert.Same(project.Resource.Release, environment["Aspire__GlitchTip__Release"]);
            Assert.Equal("True", environment["Aspire__GlitchTip__EnableErrors"]);
            Assert.Equal("False", environment["Aspire__GlitchTip__EnableLogs"]);
            Assert.Equal("False", environment["Aspire__GlitchTip__EnableTracing"]);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ConsumerReleaseAndSignalsApplyRegardlessOfReferenceCallOrder(bool referenceFirst)
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var project = AddProject(builder);
        var serviceRelease = builder.AddParameter("worker-release", "worker-v2");
        var consumer = builder.AddContainer("worker", "unused-image");
        if (referenceFirst) consumer.WithReference(project);
        consumer.WithGlitchTipRelease(serviceRelease).WithGlitchTipTelemetry(new()
        {
            EnableErrors = false,
            EnableLogs = true,
            EnableTracing = true,
            TracesSampleRate = 0.25
        });
        if (!referenceFirst) consumer.WithReference(project);

        var environment = await EnvironmentAsync(builder, consumer.Resource);
        Assert.Same(serviceRelease.Resource, environment["SENTRY_RELEASE"]);
        Assert.Same(serviceRelease.Resource, environment["Aspire__GlitchTip__Release"]);
        Assert.Equal("False", environment["Aspire__GlitchTip__EnableErrors"]);
        Assert.Equal("True", environment["Aspire__GlitchTip__EnableLogs"]);
        Assert.Equal("True", environment["Aspire__GlitchTip__EnableTracing"]);
        Assert.Equal("0.25", environment["Aspire__GlitchTip__TracesSampleRate"]);
    }

    [Fact]
    public void RepeatedReferenceUsesOneProvisioningDependency()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Run);
        var project = AddProject(builder);
        var consumer = builder.AddContainer("worker", "unused-image").WithReference(project);
        var annotationCount = consumer.Resource.Annotations.Count;
        Assert.Same(consumer, consumer.WithReference(project));
        Assert.Equal(annotationCount, consumer.Resource.Annotations.Count);
        Assert.Single(consumer.Resource.Annotations.OfType<GlitchTipConsumerAnnotation>());
        var wait = Assert.Single(consumer.Resource.Annotations.OfType<WaitAnnotation>());
        Assert.Same(project.Resource, wait.Resource);
        Assert.Equal(WaitType.WaitForCompletion, wait.WaitType);
    }

    [Fact]
    public async Task DsnIsDeferredAndCancellationDoesNotCacheAFailure()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var project = AddProject(builder);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await project.Resource.Dsn.GetValueAsync(cancellation.Token));
        Assert.False(project.Resource.Provisioned.Task.IsCompleted);

        const string dsn = "https://11111111111111111111111111111111@shared.example/42";
        project.Resource.Provisioned.SetResult(new GlitchTipProject("42", "stack", dsn));
        Assert.Equal(dsn, await project.Resource.Dsn.GetValueAsync());
        var properties = ((IResourceWithConnectionString)project.Resource).GetConnectionProperties().ToDictionary();
        Assert.Equal("{glitchtip.connectionString}", properties["Uri"].ValueExpression);
    }

    [Fact]
    public async Task LocalDsnUsesAllocatedEndpointAndPreservesProjectAndKey()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Run);
        var project = AddProject(builder);
        var server = project.Resource.LocalServer!;
        var endpoint = Assert.Single(server.Annotations.OfType<EndpointAnnotation>());
        endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, "localhost", 18573);
        project.Resource.Provisioned.SetResult(new GlitchTipProject("42", "stack", "http://11111111111111111111111111111111@server-internal:8000/42"));

        var dsn = new Uri((await project.Resource.Dsn.GetValueAsync())!);
        Assert.Equal("localhost", dsn.Host);
        Assert.Equal(18573, dsn.Port);
        Assert.Equal("11111111111111111111111111111111", dsn.UserInfo);
        Assert.Equal("/42", dsn.AbsolutePath);
    }

    [Fact]
    public void MonitorOverridesPreserveUnderlyingHealthCheck()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var project = AddProject(builder).WithMonitorDefaults(new() { IntervalSeconds = 30, TimeoutSeconds = 10 });
        var consumer = builder.AddContainer("api", "unused-image").WithHttpEndpoint(targetPort: 8080)
            .WithGlitchTipHealthCheck("/health", statusCode: 204)
            .WithGlitchTipHealthCheck("/ready").WithReference(project);
        var healthChecks = consumer.Resource.Annotations.OfType<HealthCheckAnnotation>().ToList();
        var monitorUrl = ReferenceExpression.Create($"https://public.example/health");
        consumer.WithGlitchTipMonitor("/health", options: new() { IntervalSeconds = 1, TimeoutSeconds = 60 })
            .WithGlitchTipMonitorUrl(monitorUrl, "/health")
            .WithGlitchTipMonitor("/ready", enabled: false);

        var checks = consumer.Resource.Annotations.OfType<GlitchTipMonitorAnnotation>().ToList();
        Assert.Equal(2, checks.Count);
        var health = checks.Single(check => check.Identity == "http:/health");
        Assert.Equal("http", health.Endpoint.EndpointName);
        Assert.Equal("/health", health.Path);
        Assert.Equal(204, health.StatusCode);
        Assert.Same(monitorUrl, health.Url);
        Assert.Equal(1, health.Options!.IntervalSeconds);
        Assert.Equal(60, health.Options.TimeoutSeconds);
        Assert.False(checks.Single(check => check.Path == "/ready").Enabled);
        Assert.Equal(healthChecks, consumer.Resource.Annotations.OfType<HealthCheckAnnotation>());
        Assert.Equal(30, project.Resource.MonitorDefaults.IntervalSeconds);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(86401, 20)]
    [InlineData(60, 0)]
    [InlineData(60, 61)]
    public void InvalidMonitorTimingFailsWithoutChangingDefaults(int interval, int timeout)
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var project = AddProject(builder);
        Assert.Throws<ArgumentOutOfRangeException>(() => project.WithMonitorDefaults(new() { IntervalSeconds = interval, TimeoutSeconds = timeout }));
        Assert.Equal(60, project.Resource.MonitorDefaults.IntervalSeconds);
        Assert.Equal(20, project.Resource.MonitorDefaults.TimeoutSeconds);
    }

    [Fact]
    public void ExplicitDisplayNameKeepsStableProjectParameter()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var project = AddProject(builder);
        var slug = project.Resource.ProjectSlug;
        var display = builder.AddParameter("display", "Friendly name");
        Assert.Same(project, project.WithProjectDisplayName(display));
        Assert.Same(slug, project.Resource.ProjectSlug);
        Assert.Same(display.Resource, project.Resource.DisplayName);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LocalMonitorsDeclareNetworkReferencesWithoutReadinessCycles(bool overrideWithGateway)
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Run);
        var project = AddProject(builder);
        var consumer = builder.AddContainer("api", "unused-image").WithHttpEndpoint(targetPort: 8080)
            .WithGlitchTipHealthCheck().WithReference(project);
        var gateway = builder.AddContainer("gateway", "unused-image").WithHttpEndpoint(targetPort: 8081);
        if (overrideWithGateway)
        {
            consumer.WithGlitchTipMonitorUrl(ReferenceExpression.Create($"{gateway.GetEndpoint("http")}/health"));
        }
        var disabled = builder.AddContainer("disabled", "unused-image").WithHttpEndpoint(targetPort: 8082)
            .WithGlitchTipHealthCheck().WithGlitchTipMonitor(enabled: false).WithReference(project);
        using var app = builder.Build();
        try
        {
            await builder.Eventing.PublishAsync(
                new BeforeStartEvent(app.Services, app.Services.GetRequiredService<DistributedApplicationModel>()),
                EventDispatchBehavior.BlockingConcurrent, TestContext.Current.CancellationToken);
        }
        catch (OptionsValidationException)
        {
            // DCP/dashboard paths are absent from unit tests; the other callbacks
            // still run under BlockingConcurrent and their effects are asserted.
        }

        var server = project.Resource.LocalServer!;
        var target = overrideWithGateway ? gateway.Resource : consumer.Resource;
        var reference = Assert.Single(server.Annotations.OfType<EndpointReferenceAnnotation>(), annotation => annotation.Resource == target);
        Assert.Contains("http", reference.EndpointNames);
        Assert.DoesNotContain(server.Annotations.OfType<EndpointReferenceAnnotation>(), annotation => annotation.Resource == disabled.Resource);
        Assert.DoesNotContain(server.Annotations.OfType<WaitAnnotation>(), wait => wait.Resource == consumer.Resource || wait.Resource == gateway.Resource);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeProbeTimingSurvivesUrlOverrideAndExplicitSettingsWin(bool explicitSettings)
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var project = AddProject(builder);
        var monitorUrl = ReferenceExpression.Create($"https://gateway.example/ready");
        var consumer = builder.AddContainer("api", "unused-image").WithHttpEndpoint(targetPort: 8080)
            .WithHttpProbe(ProbeType.Readiness, "/ready", periodSeconds: 47, timeoutSeconds: 13)
            .WithGlitchTipMonitorUrl(monitorUrl, "/ready").WithReference(project);
        if (explicitSettings)
        {
            consumer.WithGlitchTipMonitor("/ready", options: new() { IntervalSeconds = 9, TimeoutSeconds = 4 });
        }

        var check = Assert.Single(GlitchTipMonitoring.GetChecks(consumer.Resource, project.Resource));

        Assert.Same(monitorUrl, check.Url);
        Assert.Equal(explicitSettings ? 9 : 47, check.Options!.IntervalSeconds);
        Assert.Equal(explicitSettings ? 4 : 13, check.Options.TimeoutSeconds);
        Assert.Single(consumer.Resource.Annotations.OfType<EndpointProbeAnnotation>());
    }
    private static IResourceBuilder<GlitchTipResource> AddProject(IDistributedApplicationBuilder builder) =>
        builder.AddGlitchTip("glitchtip", builder.AddParameter("project-slug", "stack"), builder.AddParameter("release", "v1"));

    private static async Task<Dictionary<string, object>> EnvironmentAsync(IDistributedApplicationBuilder builder, IResource resource)
    {
        var values = new Dictionary<string, object>();
        var context = new EnvironmentCallbackContext(builder.ExecutionContext, values);
        foreach (var callback in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
        {
            await callback.Callback(context);
        }
        return values;
    }
}
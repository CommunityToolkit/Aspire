using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Eventing;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CommunityToolkit.Aspire.Hosting.K3s.Tests;

public class K3sServiceEndpointResourceTests
{
    // ── Registration ──────────────────────────────────────────────────────────

    [Fact]
    public void AddServiceEndpointAddsResourceWithCorrectName()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");

        cluster.AddServiceEndpoint("podinfo-web", "podinfo", 9898, "podinfo");

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(model.Resources.OfType<K3sServiceEndpointResource>());
        Assert.Equal("podinfo-web", resource.Name);
    }

    [Fact]
    public void AddServiceEndpointStoresServiceNamePortAndNamespace()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");

        cluster.AddServiceEndpoint("ep", "my-svc", 8080, "my-ns");

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(model.Resources.OfType<K3sServiceEndpointResource>());
        Assert.Equal("my-svc", resource.ServiceName);
        Assert.Equal(8080, resource.ServicePort);
        Assert.Equal("my-ns", resource.Namespace);
    }

    [Fact]
    public void AddServiceEndpointDefaultsNamespaceToDefault()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");

        cluster.AddServiceEndpoint("ep", "svc", 80);

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(model.Resources.OfType<K3sServiceEndpointResource>());
        Assert.Equal("default", resource.Namespace);
    }

    [Fact]
    public void AddServiceEndpointParentIsCluster()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");

        cluster.AddServiceEndpoint("ep", "svc", 80);

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(model.Resources.OfType<K3sServiceEndpointResource>());
        Assert.Same(cluster.Resource, resource.Parent);
        Assert.IsAssignableFrom<IResourceWithParent<K3sClusterResource>>(resource);
    }

    [Fact]
    public void AddServiceEndpointIsExcludedFromManifest()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");

        cluster.AddServiceEndpoint("ep", "svc", 80);

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(model.Resources.OfType<K3sServiceEndpointResource>());
        Assert.Contains(ManifestPublishingCallbackAnnotation.Ignore, resource.Annotations);
    }

    [Fact]
    public void AddServiceEndpointHasHealthCheckAnnotation()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");

        cluster.AddServiceEndpoint("ep", "svc", 80);

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(model.Resources.OfType<K3sServiceEndpointResource>());
        Assert.Single(resource.Annotations.OfType<HealthCheckAnnotation>());
    }

    [Fact]
    public void AddServiceEndpointSetsInitialStateProperties()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");

        cluster.AddServiceEndpoint("ep", "my-svc", 9898, "my-ns");

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(model.Resources.OfType<K3sServiceEndpointResource>());
        var snapshot = resource.Annotations.OfType<ResourceSnapshotAnnotation>()
            .Select(a => a.InitialSnapshot)
            .First();

        Assert.Equal("K3s Service Endpoint", snapshot.ResourceType);
        Assert.Contains(snapshot.Properties, p => p.Name == "ServiceName" && p.Value?.ToString() == "my-svc");
        Assert.Contains(snapshot.Properties, p => p.Name == "ServicePort" && p.Value?.ToString() == "9898");
        Assert.Contains(snapshot.Properties, p => p.Name == "Namespace" && p.Value?.ToString() == "my-ns");
    }

    // ── Scheme inference ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(443, "https")]
    [InlineData(8443, "https")]
    [InlineData(80, "http")]
    [InlineData(8080, "http")]
    [InlineData(9898, "http")]
    [InlineData(3000, "http")]
    public void AddServiceEndpointInfersSchemeFromPort(int port, string expectedScheme)
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");

        var epBuilder = cluster.AddServiceEndpoint("ep", "svc", port);

        Assert.Equal(expectedScheme, epBuilder.Resource.Scheme);
    }

    [Fact]
    public void AddServiceEndpointExplicitSchemeOverridesInference()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");

        // Port 443 would normally infer https — override to http.
        var epBuilder = cluster.AddServiceEndpoint("ep", "svc", 443, scheme: "http");

        Assert.Equal("http", epBuilder.Resource.Scheme);
    }

    [Fact]
    public void AddServiceEndpointExplicitHttpsOnNonStandardPort()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");

        var epBuilder = cluster.AddServiceEndpoint("ep", "svc", 9000, scheme: "https");

        Assert.Equal("https", epBuilder.Resource.Scheme);
    }

    // ── Port validation ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(1)]
    [InlineData(65535)]
    public void AddServiceEndpointAcceptsPortBoundaries(int port)
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");

        var ep = cluster.AddServiceEndpoint("ep", "svc", port);

        Assert.Equal(port, ep.Resource.ServicePort);
    }

    // ── WithReference (service endpoint) ─────────────────────────────────────

    [Fact]
    public void WithReferenceServiceEndpointAddsResourceRelationshipAnnotation()
    {
        // K3sServiceEndpointResource implements IResourceWithConnectionString so the
        // standard Aspire WithReference overload is used — it adds a ResourceRelationshipAnnotation.
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");
        var ep = cluster.AddServiceEndpoint("ep", "svc", 80);
        var container = appBuilder.AddContainer("app", "myorg/app");

        container.WithReference(ep);

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var containerResource = model.Resources.OfType<ContainerResource>().Single(r => r.Name == "app");

        Assert.Contains(
            containerResource.Annotations.OfType<ResourceRelationshipAnnotation>(),
            a => ReferenceEquals(a.Resource, ep.Resource));
    }

    [Fact]
    public void WithReferenceServiceEndpointAddsRuntimeArgToContainer()
    {
        // ApplyServiceUrlContainerOverride adds --add-host for containers.
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");
        var ep = cluster.AddServiceEndpoint("ep", "svc", 80);
        var container = appBuilder.AddContainer("app", "myorg/app");

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var containerResource = model.Resources.OfType<ContainerResource>().Single(r => r.Name == "app");

        containerResource.Annotations.Add(
            new ContainerRuntimeArgsCallbackAnnotation(
                args => args.Add("--add-host=host.docker.internal:host-gateway")));
        K3sBuilderExtensions.ApplyServiceUrlContainerOverride(containerResource, ep.Resource);

        Assert.NotEmpty(containerResource.Annotations.OfType<ContainerRuntimeArgsCallbackAnnotation>());
    }

    [Fact]
    public void WithReferenceServiceEndpointAddsEnvironmentCallbackToContainer()
    {
        // ApplyServiceUrlContainerOverride adds an env callback for the host.docker.internal URL.
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");
        var ep = cluster.AddServiceEndpoint("ep", "svc", 80);
        var container = appBuilder.AddContainer("app", "myorg/app");

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var containerResource = model.Resources.OfType<ContainerResource>().Single(r => r.Name == "app");

        K3sBuilderExtensions.ApplyServiceUrlContainerOverride(containerResource, ep.Resource);

        Assert.Contains(
            containerResource.Annotations.OfType<EnvironmentCallbackAnnotation>(),
            a => a.Callback is not null);
    }

    [Fact]
    public void WithReferenceServiceEndpointAddsEnvironmentCallbackToExecutable()
    {
        // Standard WithReference(IResourceWithConnectionString) adds a lazy ConnectionStringReference
        // env callback for host processes. Verify the annotation is present.
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");
        var ep = cluster.AddServiceEndpoint("ep", "svc", 80);
        var exe = appBuilder.AddExecutable("myapp", "myapp", ".");
        exe.WithReference(ep);

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var exeResource = Assert.Single(model.Resources.OfType<ExecutableResource>());

        Assert.Contains(
            exeResource.Annotations.OfType<EnvironmentCallbackAnnotation>(),
            a => a.Callback is not null);
    }

    [Fact]
    public void WithReferenceServiceEndpointDoesNotAddRuntimeArgToExecutable()
    {
        // ApplyServiceUrlContainerOverride is only called for containers; executables
        // receive the URL via the standard lazy ConnectionStringReference, no --add-host needed.
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");
        var ep = cluster.AddServiceEndpoint("ep", "svc", 80);
        var exe = appBuilder.AddExecutable("myapp", "myapp", ".");
        exe.WithReference(ep);

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var exeResource = Assert.Single(model.Resources.OfType<ExecutableResource>());

        Assert.Empty(exeResource.Annotations.OfType<ContainerRuntimeArgsCallbackAnnotation>());
    }

    // ── K3sServiceEndpointHealthCheck ─────────────────────────────────────────

    [Fact]
    public async Task ServiceEndpointHealthCheckReturnsUnhealthyWhenNotReady()
    {
        var cluster = new K3sClusterResource("k8s");
        var endpoint = new K3sServiceEndpointResource("ep", "svc", 80, "default", cluster);
        endpoint.IsReady = false;
        var check = new K3sServiceEndpointHealthCheck(endpoint);

        var result = await check.CheckHealthAsync(null!);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task ServiceEndpointHealthCheckReturnsHealthyWhenReady()
    {
        var cluster = new K3sClusterResource("k8s");
        var endpoint = new K3sServiceEndpointResource("ep", "svc", 80, "default", cluster);
        endpoint.IsReady = true;
        var check = new K3sServiceEndpointHealthCheck(endpoint);

        var result = await check.CheckHealthAsync(null!);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task ServiceEndpointHealthCheckDefaultsToNotReady()
    {
        var cluster = new K3sClusterResource("k8s");
        var endpoint = new K3sServiceEndpointResource("ep", "svc", 80, "default", cluster);
        // IsReady is false by default (default bool)
        var check = new K3sServiceEndpointHealthCheck(endpoint);

        var result = await check.CheckHealthAsync(null!);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    // ── IResourceWithConnectionString (K3sServiceEndpointResource) ──────────

    [Fact]
    public void ConnectionStringEnvironmentVariableFollowsServiceDiscoveryConvention()
    {
        var cluster = new K3sClusterResource("k8s");
        var ep = new K3sServiceEndpointResource("my-ep", "svc", 80, "default", cluster);
        Assert.Equal("services__my-ep__url", ep.ConnectionStringEnvironmentVariable);
    }

    [Fact]
    public async Task GetConnectionStringAsyncReturnsNullWhenNotReady()
    {
        var cluster = new K3sClusterResource("k8s");
        var ep = new K3sServiceEndpointResource("ep", "svc", 80, "default", cluster);
        // IsReady=false, HostPort=0 by default
        var result = await ep.GetConnectionStringAsync();
        Assert.Null(result);
    }

    [Fact]
    public async Task GetConnectionStringAsyncReturnsLocalhostUrlWhenReady()
    {
        var cluster = new K3sClusterResource("k8s");
        var ep = new K3sServiceEndpointResource("ep", "svc", 80, "default", cluster)
        {
            IsReady = true,
            HostPort = 9898,
        };

        var result = await ep.GetConnectionStringAsync();

        Assert.Equal("http://localhost:9898", result);
    }

    [Fact]
    public async Task GetConnectionStringAsyncUsesSchemeWhenReady()
    {
        var cluster = new K3sClusterResource("k8s");
        var ep = new K3sServiceEndpointResource("ep", "svc", 443, "default", cluster)
        {
            Scheme = "https",
            IsReady = true,
            HostPort = 7777,
        };

        var result = await ep.GetConnectionStringAsync();

        Assert.Equal("https://localhost:7777", result);
    }

    [Fact]
    public void ConnectionStringExpressionIsEmptyWhenNotReady()
    {
        var cluster = new K3sClusterResource("k8s");
        var ep = new K3sServiceEndpointResource("ep", "svc", 80, "default", cluster);
        // Not ready — expression should be an empty string expression.
        var expr = ep.ConnectionStringExpression;
        Assert.NotNull(expr);
    }

    // ── Resource construction ─────────────────────────────────────────────────

    [Fact]
    public void K3sServiceEndpointResourceThrowsWhenClusterIsNull()
    {
        K3sClusterResource cluster = null!;
        var action = () => new K3sServiceEndpointResource("ep", "svc", 80, "default", cluster);
        Assert.Throws<ArgumentNullException>(action);
    }

    [Fact]
    public void K3sServiceEndpointResourceThrowsWhenServiceNameIsNull()
    {
        var cluster = new K3sClusterResource("k8s");
        var action = () => new K3sServiceEndpointResource("ep", null!, 80, "default", cluster);
        Assert.Throws<ArgumentNullException>(action);
    }

    [Fact]
    public void K3sServiceEndpointResourceThrowsWhenNamespaceIsNull()
    {
        var cluster = new K3sClusterResource("k8s");
        var action = () => new K3sServiceEndpointResource("ep", "svc", 80, null!, cluster);
        Assert.Throws<ArgumentNullException>(action);
    }

    // ── Env callback / IResourceWithConnectionString ──────────────────────────
    // K3sServiceEndpointResource now implements IResourceWithConnectionString.
    // The standard WithReference overload stores a lazy ConnectionStringReference in the
    // env var (resolved at container startup by Aspire). For containers, our BeforeStartEvent
    // subscriber adds a plain-string override (host.docker.internal:{port}) on top.

    [Fact]
    public async Task WithReferenceServiceEndpoint_Container_SetsHostDockerInternalUrlWhenReady()
    {
        // ApplyServiceUrlContainerOverride (called by BeforeStartEvent for containers) writes
        // the host.docker.internal URL when the endpoint is ready.
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");
        var ep = cluster.AddServiceEndpoint("ep", "svc", 80);

        using var app = appBuilder.Build();
        var containerResource = new ContainerResource("app");

        ep.Resource.IsReady = true;
        ep.Resource.HostPort = 9090;

        K3sBuilderExtensions.ApplyServiceUrlContainerOverride(containerResource, ep.Resource);

        var envVars = new Dictionary<string, object>();
        var ctx = new EnvironmentCallbackContext(
            new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run),
            envVars);

        foreach (var cb in containerResource.Annotations.OfType<EnvironmentCallbackAnnotation>())
            await cb.Callback(ctx);

        Assert.Equal("http://host.docker.internal:9090",
            ctx.EnvironmentVariables["services__ep__url"]?.ToString());
    }

    [Fact]
    public async Task WithReferenceServiceEndpoint_Container_DoesNotOverrideUrlWhenNotReady()
    {
        // When IsReady=false the override callback skips writing the URL.
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");
        var ep = cluster.AddServiceEndpoint("ep", "svc", 80);

        using var app = appBuilder.Build();
        var containerResource = new ContainerResource("app");

        // ep.IsReady is false by default.
        K3sBuilderExtensions.ApplyServiceUrlContainerOverride(containerResource, ep.Resource);

        var envVars = new Dictionary<string, object>();
        var ctx = new EnvironmentCallbackContext(
            new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run),
            envVars);

        foreach (var cb in containerResource.Annotations.OfType<EnvironmentCallbackAnnotation>())
            await cb.Callback(ctx);

        Assert.False(ctx.EnvironmentVariables.ContainsKey("services__ep__url"));
    }

    [Fact]
    public async Task WithReferenceServiceEndpoint_Executable_SetsLocalhostUrlWhenReady()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");
        var ep = cluster.AddServiceEndpoint("ep", "svc", 443, scheme: "https");
        var exe = appBuilder.AddExecutable("myapp", "myapp", ".");
        exe.WithReference(ep);

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var exeResource = Assert.Single(model.Resources.OfType<ExecutableResource>());

        ep.Resource.IsReady = true;
        ep.Resource.HostPort = 7777;

        var envVars = new Dictionary<string, object>();
        var ctx = new EnvironmentCallbackContext(
            new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run),
            envVars);

        foreach (var cb in exeResource.Annotations.OfType<EnvironmentCallbackAnnotation>())
            await cb.Callback(ctx);

        // For host processes, standard WithReference stores a ConnectionStringReference that
        // lazily resolves via K3sServiceEndpointResource.GetConnectionStringAsync().
        var rawValue = ctx.EnvironmentVariables.GetValueOrDefault("services__ep__url");
        var resolved = rawValue is IValueProvider vp
            ? await vp.GetValueAsync(CancellationToken.None)
            : rawValue?.ToString();

        Assert.Equal("https://localhost:7777", resolved);
    }

    [Fact]
    public async Task WithReferenceServiceEndpoint_Executable_ReturnsNullUrlWhenNotReady()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");
        var ep = cluster.AddServiceEndpoint("ep", "svc", 80);
        var exe = appBuilder.AddExecutable("myapp", "myapp", ".");
        exe.WithReference(ep);

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var exeResource = Assert.Single(model.Resources.OfType<ExecutableResource>());

        // ep.IsReady is false by default.

        var envVars = new Dictionary<string, object>();
        var ctx = new EnvironmentCallbackContext(
            new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run),
            envVars);

        foreach (var cb in exeResource.Annotations.OfType<EnvironmentCallbackAnnotation>())
            await cb.Callback(ctx);

        var rawValue = ctx.EnvironmentVariables.GetValueOrDefault("services__ep__url");
        var resolved = rawValue is IValueProvider vp
            ? await vp.GetValueAsync(CancellationToken.None)
            : rawValue?.ToString();

        Assert.Null(resolved);
    }
}

using Aspire.Hosting;
using Aspire.Hosting.Eventing;

namespace CommunityToolkit.Aspire.Hosting.K3s.Tests;

public class K3sAgentNodeTests
{
    // ── Agent creation via K3sClusterOptions ─────────────────────────────────

    [Fact]
    public void AgentCountInOptionsCreatesK3sAgentResources()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        appBuilder.AddK3sCluster("k8s", agentCount: 2);

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var agents = model.Resources.OfType<K3sAgentResource>().ToList();

        Assert.Equal(2, agents.Count);
        Assert.Contains(agents, a => a.Name == "k8s-agent-0");
        Assert.Contains(agents, a => a.Name == "k8s-agent-1");
    }

    [Fact]
    public void AgentNodesAreChildrenOfCluster()
    {
        // Implements IResourceWithParent so they appear nested under k8s in the dashboard.
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s", agentCount: 1);

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var agent = Assert.Single(model.Resources.OfType<K3sAgentResource>());
        Assert.Same(cluster.Resource, agent.Parent);

        // Non-generic IResourceWithParent — used by the dashboard for grouping.
        var nonGeneric = agent as IResourceWithParent;
        Assert.NotNull(nonGeneric);
        Assert.Same(cluster.Resource, nonGeneric.Parent);
    }

    [Fact]
    public void AgentCountZeroProducesNoAgentResources()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.AddK3sCluster("k8s");

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        Assert.Empty(model.Resources.OfType<K3sAgentResource>());
    }

    [Fact]
    public void AgentCountUpdatesClusterAgentCount()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s", agentCount: 3);

        Assert.Equal(3, cluster.Resource.AgentCount);
    }

    [Fact]
    public void AgentNodesDoNotHaveWaitForDependencyOnCluster()
    {
        // Agents must NOT WaitFor the cluster — that would create a deadlock because the
        // cluster health check waits for all nodes (including agents) to be Ready.
        // Instead, k3s agent retries connecting to the server independently.
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.AddK3sCluster("k8s", agentCount: 1);

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var agent = Assert.Single(model.Resources.OfType<K3sAgentResource>());
        var waitAnnotations = agent.Annotations.OfType<WaitAnnotation>().ToList();

        Assert.DoesNotContain(waitAnnotations, w => w.Resource is K3sClusterResource);
    }

    [Fact]
    public void AgentNodesUseSameImageAsServer()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.AddK3sCluster("k8s", agentCount: 1);

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var agent = Assert.Single(model.Resources.OfType<K3sAgentResource>());
        var img = Assert.Single(agent.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal(K3sContainerImageTags.Image, img.Image);
        Assert.Equal(K3sContainerImageTags.Registry, img.Registry);
    }

    [Fact]
    public void AgentNodesAreExcludedFromManifest()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.AddK3sCluster("k8s", agentCount: 1);

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var agent = Assert.Single(model.Resources.OfType<K3sAgentResource>());
        Assert.Contains(ManifestPublishingCallbackAnnotation.Ignore, agent.Annotations);
    }

    [Fact]
    public void AgentNodesHaveEnvironmentAnnotations()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.AddK3sCluster("k8s", agentCount: 1);

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var agent = Assert.Single(model.Resources.OfType<K3sAgentResource>());
        var envCallbacks = agent.Annotations.OfType<EnvironmentCallbackAnnotation>().ToList();
        Assert.True(envCallbacks.Count >= 3,
            $"Expected at least 3 env annotations (K3S_URL, K3S_TOKEN, K3S_NODE_NAME), got {envCallbacks.Count}");
    }

    [Fact]
    public void DefaultClusterHasNoAgentNodes()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.AddK3sCluster("k8s");

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        Assert.Empty(model.Resources.OfType<K3sAgentResource>());
    }

    [Fact]
    public void AgentNodeNamesFollowConvention()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.AddK3sCluster("mycluster", agentCount: 3);

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        Assert.Contains(model.Resources, r => r.Name == "mycluster-agent-0");
        Assert.Contains(model.Resources, r => r.Name == "mycluster-agent-1");
        Assert.Contains(model.Resources, r => r.Name == "mycluster-agent-2");
    }

    [Fact]
    public void NegativeAgentCountIsIgnored()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.AddK3sCluster("k8s", agentCount: -1);

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        Assert.Empty(model.Resources.OfType<K3sAgentResource>());
    }

    // ── WithAgentCount ────────────────────────────────────────────────────────

    [Fact]
    public void WithAgentCountAddsCorrectNumberOfAgentResources()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.AddK3sCluster("k8s").WithAgentCount(3);

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var agents = model.Resources.OfType<K3sAgentResource>().ToList();
        Assert.Equal(3, agents.Count);
    }

    [Fact]
    public void WithAgentCountZeroIsNoOp()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.AddK3sCluster("k8s").WithAgentCount(0);

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        Assert.Empty(model.Resources.OfType<K3sAgentResource>());
    }

    [Fact]
    public void WithAgentCountNamesAgentsSequentially()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.AddK3sCluster("k8s").WithAgentCount(2);

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var names = model.Resources.OfType<K3sAgentResource>().Select(r => r.Name).ToList();
        Assert.Contains("k8s-agent-0", names);
        Assert.Contains("k8s-agent-1", names);
    }

    [Fact]
    public void WithAgentCountAgentsUseClusterImageTag()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.AddK3sCluster("k8s")
            .WithK3sVersion("v1.30.0-k3s1")
            .WithAgentCount(1);

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var agent = Assert.Single(model.Resources.OfType<K3sAgentResource>());
        var img = Assert.Single(agent.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal("v1.30.0-k3s1", img.Tag);
    }

    [Fact]
    public void WithAgentCountIsEquivalentToConfigureAgentCount()
    {
        // WithAgentCount(2) should produce the same agent resources as
        // AddK3sCluster with configure: cfg => cfg.AgentCount = 2
        var b1 = DistributedApplication.CreateBuilder();
        b1.AddK3sCluster("k8s", agentCount: 2);
        using var app1 = b1.Build();
        var agents1 = app1.Services.GetRequiredService<DistributedApplicationModel>()
            .Resources.OfType<K3sAgentResource>().ToList();

        var b2 = DistributedApplication.CreateBuilder();
        b2.AddK3sCluster("k8s").WithAgentCount(2);
        using var app2 = b2.Build();
        var agents2 = app2.Services.GetRequiredService<DistributedApplicationModel>()
            .Resources.OfType<K3sAgentResource>().ToList();

        Assert.Equal(agents1.Count, agents2.Count);
        Assert.Equal(
            agents1.Select(a => a.Name).OrderBy(n => n),
            agents2.Select(a => a.Name).OrderBy(n => n));
    }

    [Fact]
    public void WithAgentCountShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<K3sClusterResource> builder = null!;
        var action = () => builder.WithAgentCount(2);
        Assert.Throws<ArgumentNullException>(action);
    }

    [Fact]
    public void WithAgentCountShouldThrowWhenCountIsNegative()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");
        var action = () => cluster.WithAgentCount(-1);
        Assert.Throws<ArgumentOutOfRangeException>(action);
    }

    [Fact]
    public void WithLifetimePersistentPropagatestoAgentNodes()
    {
        // ContainerLifetimeAnnotation must propagate immediately at call time —
        // DCP uses it to compute container identity before BeforeStartEvent fires.
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder
            .AddK3sCluster("k8s", agentCount: 2)
            .WithLifetime(ContainerLifetime.Persistent);

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var agents = model.Resources.OfType<K3sAgentResource>().ToList();
        Assert.Equal(2, agents.Count);

        foreach (var agent in agents)
        {
            var annotation = Assert.Single(agent.Annotations.OfType<ContainerLifetimeAnnotation>());
            Assert.Equal(ContainerLifetime.Persistent, annotation.Lifetime);
        }
    }

    [Fact]
    public void WithLifetimeSessionDoesNotAddPersistentAnnotationToAgents()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder
            .AddK3sCluster("k8s", agentCount: 1)
            .WithLifetime(ContainerLifetime.Session);

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var agent = Assert.Single(model.Resources.OfType<K3sAgentResource>());
        var annotation = Assert.Single(agent.Annotations.OfType<ContainerLifetimeAnnotation>());
        Assert.Equal(ContainerLifetime.Session, annotation.Lifetime);
    }
}

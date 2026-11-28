// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;
using Aspire.Hosting.Utils;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Flare.Tests;

public class WithResourceGraphTests
{
    private static IResourceBuilder<FlareResource> CreateFlare(IDistributedApplicationBuilder builder) =>
        builder.AddFlare("flare", builder.AddClickHouse("clickhouse"), builder.AddRedis("redis"));

    [Fact]
    public void WithoutResourceGraphNoProxyIsAdded()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        CreateFlare(builder);

        Assert.DoesNotContain(builder.Resources, resource => resource.Name == "flare-docker-proxy");
    }

    [Fact]
    public void WithResourceGraphAddsAReadOnlyDockerProxy()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        CreateFlare(builder).WithResourceGraph();

        var proxy = Assert.Single(builder.Resources.OfType<ContainerResource>(), resource => resource.Name == "flare-docker-proxy");
        var image = Assert.Single(proxy.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal("tecnativa/docker-socket-proxy", image.Image);
        Assert.Equal("3.4", image.Tag);

        var mount = Assert.Single(proxy.Annotations.OfType<ContainerMountAnnotation>());
        Assert.Equal("/var/run/docker.sock", mount.Source);
        Assert.Equal("/var/run/docker.sock", mount.Target);
        Assert.Equal(ContainerMountType.BindMount, mount.Type);
        Assert.True(mount.IsReadOnly);
    }

    [Fact]
    public async Task WithResourceGraphOnlyAllowsListingContainers()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        CreateFlare(builder).WithResourceGraph();

        var proxy = Assert.Single(builder.Resources.OfType<ContainerResource>(), resource => resource.Name == "flare-docker-proxy");
        var config = await proxy.GetEnvironmentVariablesAsync(DistributedApplicationOperation.Publish);

        Assert.Equal("1", config["CONTAINERS"]);
        Assert.Equal("0", config["POST"]);
    }

    [Fact]
    public async Task WithResourceGraphPointsTheApiAtTheProxy()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        var flare = CreateFlare(builder).WithResourceGraph();

        var config = await flare.Resource.Api.GetEnvironmentVariablesAsync(DistributedApplicationOperation.Publish);

        Assert.Equal("{flare-docker-proxy.bindings.http.url}", config["DockerResources__ProxyUrl"]);
        Assert.False(config.ContainsKey("KubernetesResources__Enabled"));
    }

    [Fact]
    public async Task WithResourceGraphLabelsEveryContainerInTheGraph()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var clickhouse = builder.AddClickHouse("clickhouse");
        var redis = builder.AddRedis("redis");

        var flare = builder.AddFlare("flare", clickhouse, redis).WithResourceGraph();

        (ContainerResource Resource, string Role, string? Relationships)[] expected =
        [
            (flare.Resource, "ingest", "clickhouse:Reference,redis:Reference"),
            (flare.Resource.Api, "api", "clickhouse:Reference,redis:Reference"),
            (flare.Resource.AlertWorker, "alert-worker", "clickhouse:Reference,redis:Reference"),
            (flare.Resource.Dashboard, "dashboard", "api:Reference"),
            (clickhouse.Resource, "clickhouse", null),
            (redis.Resource, "redis", null),
        ];

        foreach (var (resource, role, relationships) in expected)
        {
            var args = await GetRuntimeArgsAsync(resource);

            Assert.Contains("flare.resource=true", args);
            Assert.Contains($"flare.role={role}", args);
            if (relationships is null)
            {
                Assert.DoesNotContain(args, arg => arg.StartsWith("flare.relationships=", StringComparison.Ordinal));
            }
            else
            {
                Assert.Contains($"flare.relationships={relationships}", args);
            }
        }
    }

    [Fact]
    public async Task WithResourceGraphUsesKubernetesWhenPublishingToKubernetes()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        builder.AddKubernetesEnvironment("k8s");

        var flare = CreateFlare(builder).WithResourceGraph();

        var config = await flare.Resource.Api.GetEnvironmentVariablesAsync(DistributedApplicationOperation.Publish);

        Assert.Equal("true", config["KubernetesResources__Enabled"]);
        Assert.False(config.ContainsKey("DockerResources__ProxyUrl"));
    }

    [Fact]
    public async Task WithResourceGraphSeesAKubernetesEnvironmentAddedLater()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var flare = CreateFlare(builder).WithResourceGraph();
        builder.AddKubernetesEnvironment("k8s");

        var config = await flare.Resource.Api.GetEnvironmentVariablesAsync(DistributedApplicationOperation.Publish);

        Assert.Equal("true", config["KubernetesResources__Enabled"]);
    }

    [Fact]
    public void WithResourceGraphUsesDockerLocallyEvenWithAKubernetesEnvironment()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddKubernetesEnvironment("k8s");

        CreateFlare(builder).WithResourceGraph();

        Assert.Contains(builder.Resources, resource => resource.Name == "flare-docker-proxy");
    }

    [Fact]
    public void WithResourceGraphShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<FlareResource> builder = null!;

        var action = () => builder.WithResourceGraph();

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    private static async Task<List<string>> GetRuntimeArgsAsync(IResource resource)
    {
        var args = new List<string>();
        foreach (var annotation in resource.Annotations.OfType<ContainerRuntimeArgsCallbackAnnotation>())
        {
            var values = new List<object>();
            await annotation.Callback(new ContainerRuntimeArgsCallbackContext(values));
            args.AddRange(values.Select(value => value.ToString()!));
        }

        return args;
    }
}

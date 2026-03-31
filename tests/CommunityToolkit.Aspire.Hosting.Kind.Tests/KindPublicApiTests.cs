// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Kind;

namespace CommunityToolkit.Aspire.Hosting.Kind.Tests;

public class KindPublicApiTests
{
    [Fact]
    public void AddKindClusterShouldThrowWhenBuilderIsNull()
    {
        IDistributedApplicationBuilder builder = null!;

        var action = () => builder.AddKindCluster("test");

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void AddKindClusterShouldThrowWhenNameIsNull()
    {
        var builder = DistributedApplication.CreateBuilder();
        string name = null!;

        var action = () => builder.AddKindCluster(name);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(name), exception.ParamName);
    }

    [Fact]
    public void WithKubernetesVersionShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<KindClusterResource> builder = null!;

        var action = () => builder.WithKubernetesVersion("v1.32.2");

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void WithWorkerNodesShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<KindClusterResource> builder = null!;

        var action = () => builder.WithWorkerNodes(2);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void WithClusterLifetimeShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<KindClusterResource> builder = null!;

        var action = () => builder.WithClusterLifetime(ClusterLifetime.Persistent);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void WithReferenceShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<IResourceWithEnvironment> builder = null!;
        IResourceBuilder<KindClusterResource> kind = null!;

        var action = () => builder.WithReference(kind);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void WithKindNetworkShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<ContainerResource> builder = null!;

        var action = () => builder.WithKindNetwork();

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void WithKindConfigShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<KindClusterResource> builder = null!;

        var action = () => builder.WithKindConfig(_ => { });

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void WithKindConfigShouldThrowWhenConfigureIsNull()
    {
        var builder = DistributedApplication.CreateBuilder();
        var cluster = builder.AddKindCluster("test");
        Action<KindConfigModel> configure = null!;

        var action = () => cluster.WithKindConfig(configure);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(configure), exception.ParamName);
    }

    [Fact]
    public void CtorKindClusterResourceShouldThrowWhenNameIsNull()
    {
        string name = null!;

        var action = () => new KindClusterResource(name);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(name), exception.ParamName);
    }
}

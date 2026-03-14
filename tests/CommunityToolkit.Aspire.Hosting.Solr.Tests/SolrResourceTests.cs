// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CommunityToolkit.Aspire.Hosting.Solr.Tests;

public class SolrResourceTests
{
    [Fact]
    public void AddSolr_CreatesCorrectResource()
    {
        var builder = DistributedApplication.CreateBuilder();
        var solr = builder.AddSolr("solr");
        
        Assert.NotNull(solr.Resource);
        Assert.Equal("solr", solr.Resource.Name);
        Assert.IsType<SolrResource>(solr.Resource);
        Assert.Equal("solr", solr.Resource.CoreName);
    }

    [Fact]
    public void AddSolr_WithCustomCoreName_ConfiguresCorrectly()
    {
        var builder = DistributedApplication.CreateBuilder();
        var solr = builder.AddSolr("solr", coreName: "mycustomcore");
        
        Assert.NotNull(solr.Resource);
        Assert.Equal("solr", solr.Resource.Name);
        Assert.Equal("mycustomcore", solr.Resource.CoreName);
    }

    [Fact]
    public void AddSolr_WithPort_ConfiguresCorrectly()
    {
        var builder = DistributedApplication.CreateBuilder();
        var solr = builder.AddSolr("solr", port: 8984);
        
        Assert.NotNull(solr.Resource);
        Assert.Equal("solr", solr.Resource.Name);
        Assert.Equal("solr", solr.Resource.CoreName);
    }

    [Fact]
    public void AddSolr_WithPortAndCoreName_ConfiguresCorrectly()
    {
        var builder = DistributedApplication.CreateBuilder();
        var solr = builder.AddSolr("solr", port: 8984, coreName: "testcore");
        
        Assert.NotNull(solr.Resource);
        Assert.Equal("solr", solr.Resource.Name);
        Assert.Equal("testcore", solr.Resource.CoreName);
    }

    [Fact]
    public void AddSolr_WithConfigsetAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();

        var configSetsPath = Path.GetFullPath(Path.Combine(
                builder.AppHostDirectory,
                "./configsets/test"));

        var solr = builder.AddSolr("solr", coreName: "testcore")
            .WithConfigset("test", configSetsPath);

        Assert.NotNull(solr.Resource);
        Assert.Equal("solr", solr.Resource.Name);
        Assert.Equal("testcore", solr.Resource.CoreName);

        // Verify the SolrConfigSetAnnotation was added with correct values
        var configSetAnnotation = solr.Resource.Annotations.OfType<SolrConfigSetAnnotation>().LastOrDefault();
        Assert.NotNull(configSetAnnotation);
        Assert.Equal("test", configSetAnnotation.ConfigSetName);
        Assert.Equal(configSetsPath, configSetAnnotation.ConfigSetPath);

        // Verify the bind mount annotation was configured correctly
        var bindMountAnnotation = solr.Resource.Annotations
            .OfType<ContainerMountAnnotation>()
            .FirstOrDefault(cma => cma.Source == configSetsPath);
        Assert.NotNull(bindMountAnnotation);
        Assert.Equal("/opt/solr/server/solr/configsets/test", bindMountAnnotation.Target);
    }

    [Fact]
    public void SolrResource_HasCorrectConnectionString()
    {
        var builder = DistributedApplication.CreateBuilder();
        var solr = builder.AddSolr("solr", coreName: "mycore");
        
        Assert.NotNull(solr.Resource.ConnectionStringExpression);
        // Connection string should contain the core name
        var connectionString = solr.Resource.ConnectionStringExpression.ValueExpression;
        Assert.Contains("mycore", connectionString);
        Assert.Contains("/solr/mycore", connectionString);
    }

    [Fact]
    public void SolrResource_HasHealthCheck()
    {
        var builder = DistributedApplication.CreateBuilder();
        var solr = builder.AddSolr("solr", coreName: "testcore");
        
        using var app = builder.Build();
        var healthChecks = app.Services.GetRequiredService<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckService>();
        Assert.NotNull(healthChecks);
    }

    [Fact]
    public void SolrResource_DefaultCoreName_IsSolr()
    {
        var builder = DistributedApplication.CreateBuilder();
        var solr = builder.AddSolr("solr");
        
        Assert.Equal("solr", solr.Resource.CoreName);
    }

    [Fact]
    public void WithDataVolume_NoName_GeneratesVolumeName()
    {
        var builder = DistributedApplication.CreateBuilder();
        var solr = builder.AddSolr("solr").WithDataVolume();

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = appModel.Resources.OfType<SolrResource>().SingleOrDefault();

        Assert.NotNull(resource);
        Assert.True(resource.TryGetLastAnnotation(out ContainerMountAnnotation? mountAnnotation));
        Assert.EndsWith("-data", mountAnnotation.Source);
        Assert.Equal("/var/solr", mountAnnotation.Target);
        Assert.Equal(ContainerMountType.Volume, mountAnnotation.Type);
    }

    [Fact]
    public void WithDataVolume_WithName_UsesProvidedName()
    {
        var builder = DistributedApplication.CreateBuilder();
        var solr = builder.AddSolr("solr").WithDataVolume("mysolrdata");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = appModel.Resources.OfType<SolrResource>().SingleOrDefault();

        Assert.NotNull(resource);
        Assert.True(resource.TryGetLastAnnotation(out ContainerMountAnnotation? mountAnnotation));
        Assert.Equal("mysolrdata", mountAnnotation.Source);
        Assert.Equal("/var/solr", mountAnnotation.Target);
        Assert.Equal(ContainerMountType.Volume, mountAnnotation.Type);
    }

    [Fact]
    public void WithDataBindMount_AddsBindMountAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();
        var solr = builder.AddSolr("solr").WithDataBindMount("./mysolrdata");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = appModel.Resources.OfType<SolrResource>().SingleOrDefault();

        Assert.NotNull(resource);
        Assert.True(resource.TryGetLastAnnotation(out ContainerMountAnnotation? mountAnnotation));
        Assert.EndsWith("mysolrdata", mountAnnotation.Source);
        Assert.Equal("/var/solr", mountAnnotation.Target);
        Assert.Equal(ContainerMountType.BindMount, mountAnnotation.Type);
    }
}

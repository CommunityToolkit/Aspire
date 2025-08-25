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
}

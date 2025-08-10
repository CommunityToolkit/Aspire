using Xunit;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

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
    }

    [Fact]
    public void AddSolr_WithPort_ConfiguresCorrectly()
    {
        var builder = DistributedApplication.CreateBuilder();
        var solr = builder.AddSolr("solr", port: 8984);
        
        Assert.NotNull(solr.Resource);
        Assert.Equal("solr", solr.Resource.Name);
    }

    [Fact]
    public void AddSolrCore_AddsCore()
    {
        var builder = DistributedApplication.CreateBuilder();
        var solr = builder.AddSolr("solr");
        var core = solr.AddSolrCore("mycore");
        
        Assert.Single(solr.Resource.Cores);
        Assert.Equal("mycore", core.Resource.Name);
        Assert.Equal(solr.Resource, core.Resource.Parent);
    }

    [Fact]
    public void SolrCoreResource_HasCorrectConnectionString()
    {
        var builder = DistributedApplication.CreateBuilder();
        var solr = builder.AddSolr("solr");
        var core = solr.AddSolrCore("mycore");
        
        Assert.NotNull(core.Resource.ConnectionStringExpression);
        Assert.Contains("mycore", core.Resource.ConnectionStringExpression.ValueExpression);
    }
}

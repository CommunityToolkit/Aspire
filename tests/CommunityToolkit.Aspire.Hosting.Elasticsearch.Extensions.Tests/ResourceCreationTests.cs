using Aspire.Hosting;
using System.Text.Json;

namespace CommunityToolkit.Aspire.Hosting.Elasticsearch.Extensions.Tests;

public class ResourceCreationTests
{
    [Fact]
    public async Task WithElasticvueEnablesCorsOnElasticResource()
    {
        var builder = DistributedApplication.CreateBuilder();

        _ = builder.AddElasticsearch("elasticsearch")
            .WithEndpoint("http", e => e.AllocatedEndpoint = new AllocatedEndpoint(e, "localhost", 9201))
            .WithElasticvue(e => e.WithEndpoint("http", ep => ep.AllocatedEndpoint = new AllocatedEndpoint(ep, "localhost", 8069)));
        
        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var elasticsearchResource = appModel.Resources.OfType<ElasticsearchResource>().Single();

        var environmentVariables = await elasticsearchResource.GetEnvironmentVariableValuesAsync();

        Assert.True(environmentVariables.ContainsKey("http.cors.enabled"));
        Assert.Equal("true", environmentVariables["http.cors.enabled"]);

        Assert.True(environmentVariables.ContainsKey("http.cors.allow-origin"));
        Assert.Equal("\"http://localhost:8069\"", environmentVariables["http.cors.allow-origin"]);
    }

    [Fact]
    public async Task WithElasticvueAddsAnnotations()
    {
        var builder = DistributedApplication.CreateBuilder();

        var elasticsearchResourceBuilder = builder.AddElasticsearch("elasticsearch")
            .WithEndpoint("http", e => e.AllocatedEndpoint = new AllocatedEndpoint(e, "localhost", 9201))
            .WithElasticvue(e => e.WithEndpoint("http", ep => ep.AllocatedEndpoint = new AllocatedEndpoint(ep, "localhost", 8069)));

        var elasticsearchResource = elasticsearchResourceBuilder.Resource;

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var elasticvueResource = appModel.Resources.OfType<ElasticvueContainerResource>().SingleOrDefault();

        Assert.NotNull(elasticvueResource);

        Assert.Equal("elasticvue", elasticvueResource.Name);

        var envs = await elasticvueResource.GetEnvironmentVariableValuesAsync();

        Assert.NotEmpty(envs);

        var clustersSetting = envs["ELASTICVUE_CLUSTERS"];

        var clusters = JsonSerializer.Deserialize<List<ElasticvueEnvironmentSettings>>(clustersSetting);

        Assert.NotNull(clusters);
        Assert.Single(clusters);

        var cluster = clusters.First();

        Assert.Equal(elasticsearchResource.Name, cluster.Name);
        Assert.Equal(elasticsearchResource.GetEndpoint("http").Url, cluster.Uri);
        Assert.Equal("elastic", cluster.Username);
        Assert.Equal(await elasticsearchResource.PasswordParameter.GetValueAsync(default), cluster.Password);
    }

    [Fact]
    public void MultipleWithElasticvueCallsAddsOneElasticvueResource()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddElasticsearch("elasticsearch1").WithElasticvue();
        builder.AddElasticsearch("elasticsearch2").WithElasticvue();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var elasticvueResource = appModel.Resources.OfType<ElasticvueContainerResource>().SingleOrDefault();
        Assert.NotNull(elasticvueResource);

        Assert.Equal("elasticvue", elasticvueResource.Name);
    }

    [Fact]
    public void WithElasticvueShouldChangeElasticvueHostPort()
    {
        var builder = DistributedApplication.CreateBuilder();
        _ = builder.AddElasticsearch("elasticsearch")
            .WithElasticvue(c => c.WithHostPort(8068));

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var elasticvueResource = appModel.Resources.OfType<ElasticvueContainerResource>().SingleOrDefault();
        Assert.NotNull(elasticvueResource);

        var primaryEndpoint = elasticvueResource.Annotations.OfType<EndpointAnnotation>().Single();
        Assert.Equal(8068, primaryEndpoint.Port);
    }

    [Fact]
    public void WithElasticvueShouldChangeElasticvueContainerImageTag()
    {
        var builder = DistributedApplication.CreateBuilder();
        _ = builder.AddElasticsearch("elasticsearch")
            .WithElasticvue(c => c.WithImageTag("manualTag"));
        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var elasticvueResource = appModel.Resources.OfType<ElasticvueContainerResource>().SingleOrDefault();
        Assert.NotNull(elasticvueResource);

        var containerImageAnnotation = elasticvueResource.Annotations.OfType<ContainerImageAnnotation>().Single();
        Assert.Equal("manualTag", containerImageAnnotation.Tag);
    }

    [Fact]
    public async Task WithElasticvueAddsAnnotationsForMultipleElasticsearchResource()
    {
        var builder = DistributedApplication.CreateBuilder();

        var elasticsearchResourceBuilder1 = builder.AddElasticsearch("elasticsearch1")
            .WithEndpoint("http", e => e.AllocatedEndpoint = new AllocatedEndpoint(e, "localhost", 9201))
            .WithElasticvue(e => e.WithEndpoint("http", ep => ep.AllocatedEndpoint = new AllocatedEndpoint(ep, "localhost", 8069)));

        var elasticsearchResource1 = elasticsearchResourceBuilder1.Resource;

        var elasticsearchResourceBuilder2 = builder.AddElasticsearch("elasticsearch2")
            .WithEndpoint("http", e => e.AllocatedEndpoint = new AllocatedEndpoint(e, "localhost", 9202))
            .WithElasticvue();

        var elasticsearchResource2 = elasticsearchResourceBuilder2.Resource;

        // This resource should not be included in Elasticvue configuration
        var elasticsearchResourceBuilder3 = builder.AddElasticsearch("elasticsearch3")
            .WithEndpoint("http", e => e.AllocatedEndpoint = new AllocatedEndpoint(e, "localhost", 9202));

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var elasticvueResource = appModel.Resources.OfType<ElasticvueContainerResource>().SingleOrDefault();

        Assert.NotNull(elasticvueResource);

        Assert.Equal("elasticvue", elasticvueResource.Name);

        var envs = await elasticvueResource.GetEnvironmentVariableValuesAsync();

        Assert.NotEmpty(envs);

        var clustersSetting = envs["ELASTICVUE_CLUSTERS"];

        var clusters = JsonSerializer.Deserialize<List<ElasticvueEnvironmentSettings>>(clustersSetting);

        Assert.NotNull(clusters);
        Assert.Equal(2, clusters.Count);

        var cluster1 = clusters.First(x => x.Name == elasticsearchResource1.Name);

        Assert.Equal(elasticsearchResource1.Name, cluster1.Name);
        Assert.Equal(elasticsearchResource1.GetEndpoint("http").Url, cluster1.Uri);
        Assert.Equal("elastic", cluster1.Username);
        Assert.Equal(await elasticsearchResource1.PasswordParameter.GetValueAsync(default), cluster1.Password);

        var cluster2 = clusters.First(x => x.Name == elasticsearchResource2.Name);

        Assert.Equal(elasticsearchResource2.Name, cluster2.Name);
        Assert.Equal(elasticsearchResource2.GetEndpoint("http").Url, cluster2.Uri);
        Assert.Equal("elastic", cluster2.Username);
        Assert.Equal(await elasticsearchResource2.PasswordParameter.GetValueAsync(default), cluster2.Password);
    }
}

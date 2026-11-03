// Bound each node's heap so the two containers do not size themselves to the full host memory.
const string elasticsearchJavaOptions = "-Xms1g -Xmx1g";

var builder = DistributedApplication.CreateBuilder(args);

var elasticsearch1 = builder.AddElasticsearch("elasticsearch1")
    .WithEnvironment("ES_JAVA_OPTS", elasticsearchJavaOptions)
    .WithElasticvue(c => c.WithHostPort(8068));

var elasticsearch2 = builder.AddElasticsearch("elasticsearch2")
    .WithEnvironment("ES_JAVA_OPTS", elasticsearchJavaOptions)
    .WithElasticvue();

builder.Build().Run();

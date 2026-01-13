var builder = DistributedApplication.CreateBuilder(args);

var elasticsearch1 = builder.AddElasticsearch("elasticsearch1")
    .WithElasticvue(c => c.WithHostPort(8068));

var elasticsearch2 = builder.AddElasticsearch("elasticsearch2")
    .WithElasticvue();

builder.Build().Run();

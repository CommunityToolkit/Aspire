var builder = DistributedApplication.CreateBuilder(args);

var clickhouse = builder.AddClickHouse("clickhouse");
var redis = builder.AddRedis("redis");

var flare = builder.AddFlare("flare", clickhouse, redis)
    .WithDataVolumes();

// A small service that places an order every couple of seconds. Its logs, traces (including failed
// orders) and metrics go to Flare: open the flare-dashboard URL to explore them.
#pragma warning disable ASPIREDOTNETPROJECT001
builder.AddDotnetProject("apiservice", new Projects.CommunityToolkit_Aspire_Hosting_Flare_ApiService().ProjectPath)
    .WithOtlpExporter(flare)
    .WaitFor(flare);
#pragma warning restore ASPIREDOTNETPROJECT001

builder.Build().Run();

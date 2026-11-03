var builder = DistributedApplication.CreateBuilder(args);

var clickhouse = builder.AddClickHouse("clickhouse");
var redis = builder.AddRedis("redis");

var flare = builder.AddFlare("flare", clickhouse, redis)
    .WithDataVolumes();

builder.Build().Run();

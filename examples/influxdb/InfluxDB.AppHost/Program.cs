using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var influxdb = builder.AddInfluxDB("influxdb");

builder.AddProject<InfluxDB_ApiService>("apiservice")
    .WithReference(influxdb)
    .WaitFor(influxdb);

builder.Build().Run();

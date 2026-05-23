using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

var httpPort = builder.Configuration.GetValue<int?>("Jellyfin:HttpPort");

builder.AddJellyfin("jellyfin", httpPort: httpPort)
    .WithDataVolume()
    .WithCacheVolume();

builder.Build().Run();

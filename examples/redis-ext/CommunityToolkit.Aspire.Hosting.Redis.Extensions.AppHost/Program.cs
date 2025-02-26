var builder = DistributedApplication.CreateBuilder(args);

builder.AddRedis("redis1").WithDbGate(c => c.WithHostPort(8068));
builder.AddRedis("redis2").WithDbGate();

builder.Build().Run();

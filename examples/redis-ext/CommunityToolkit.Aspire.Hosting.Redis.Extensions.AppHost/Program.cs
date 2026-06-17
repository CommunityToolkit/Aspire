var builder = DistributedApplication.CreateBuilder(args);

builder.AddRedis("redis1")
    .WithDbGate(c => c.WithHostPort(8068))
    .WithDbx(c => c.WithHostPort(8069));
builder.AddRedis("redis2")
    .WithDbGate()
    .WithDbx();

builder.Build().Run();

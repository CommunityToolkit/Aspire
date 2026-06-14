var builder = DistributedApplication.CreateBuilder(args);

builder.AddRedPanda("redpanda")
    .WithConsole();

builder.Build().Run();

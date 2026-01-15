var builder = DistributedApplication.CreateBuilder(args);

builder.AddPermify("permify");

builder.Build().Run();
var builder = DistributedApplication.CreateBuilder(args);

builder.AddRustApp("rust-app", "../actix_api");

builder.Build().Run();

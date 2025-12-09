var builder = DistributedApplication.CreateBuilder(args);

builder.AddOpenFga("openfga").WithInMemoryDatastore();

builder.Build().Run();

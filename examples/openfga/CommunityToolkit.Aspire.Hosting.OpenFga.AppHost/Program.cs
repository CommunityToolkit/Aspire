var builder = DistributedApplication.CreateBuilder(args);

// OpenFGA with in-memory storage (for development/testing)
var openfga = builder.AddOpenFga("openfga")
    .WithInMemoryDatastore();

builder.Build().Run();

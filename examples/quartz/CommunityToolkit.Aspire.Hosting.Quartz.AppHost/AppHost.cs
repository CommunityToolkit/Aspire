var builder = DistributedApplication.CreateBuilder(args);

// PostgreSQL for Quartz job storage
var postgres = builder.AddPostgres("postgres")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithPgAdmin()
    .AddDatabase("quartzdb");

// Add API service with Quartz.NET scheduling
var apiService = builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Quartz_ApiService>("apiservice")
    .WithReference(postgres);

// Add web frontend
builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Quartz_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();

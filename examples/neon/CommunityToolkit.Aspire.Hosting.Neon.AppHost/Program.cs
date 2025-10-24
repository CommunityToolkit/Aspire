var builder = DistributedApplication.CreateBuilder(args);

// Add Neon project resource
var neon = builder.AddNeonProject("neon");

// Add a Neon database
var neonDb = neon.AddDatabase("neondb");

// Reference the Neon database in a project
builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Neon_ApiService>("apiservice")
    .WithReference(neonDb)
    .WaitFor(neonDb)
    .WithHttpHealthCheck("/health");

builder.Build().Run();

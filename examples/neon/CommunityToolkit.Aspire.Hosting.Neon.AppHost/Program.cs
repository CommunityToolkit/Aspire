using CommunityToolkit.Aspire.Hosting.Neon;

var builder = DistributedApplication.CreateBuilder(args);

var neonApiKey = builder.AddParameter("neon-api-key", "neon-key", secret: true);

var neon = builder.AddNeon("neon", neonApiKey)
    .AddProject("aspire-neon")
    .AddBranch("dev")
    .AddProvisioner(NeonProvisionerMode.Provision);

neon.AddDatabase("appdb", "appdb");

builder.Build().Run();
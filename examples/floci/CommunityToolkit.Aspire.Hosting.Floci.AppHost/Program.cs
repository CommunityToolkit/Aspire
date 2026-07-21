var builder = DistributedApplication.CreateBuilder(args);

var floci = builder.AddFloci("floci")
    .WithHttpsDeveloperCertificate()
    .WithFlociUI();

builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Floci_ApiService>("floci-api")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(floci)
    .WaitFor(floci);

builder.Build().Run();

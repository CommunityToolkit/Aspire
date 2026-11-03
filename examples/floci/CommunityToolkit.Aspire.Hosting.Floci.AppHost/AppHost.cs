var builder = DistributedApplication.CreateBuilder(args);

var flociAws = builder.AddFlociAws("floci-aws")
    .WithHttpsDeveloperCertificate();
var flociAzure = builder.AddFlociAzure("floci-az")
    .WithHttpsDeveloperCertificate();
var flociGcp = builder.AddFlociGcp("floci-gcp");

// A single Floci UI console browses all three clouds — flociAws.WithFlociUI() creates the
// console wired to AWS, then WithReference attaches the Azure and GCP resources to it.
// Named "floci-ui" (not "floci-aws-ui") since it isn't AWS-specific once the other clouds
// are plugged in.
flociAws.WithFlociUI(configureContainer: ui =>
{
    ui.WithReference(flociAzure);
    ui.WithReference(flociGcp);
}, containerName: "floci-ui");

#pragma warning disable ASPIREDOTNETPROJECT001
builder.AddDotnetProject("floci-api", new Projects.CommunityToolkit_Aspire_Hosting_Floci_ApiService().ProjectPath)
#pragma warning restore ASPIREDOTNETPROJECT001
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(flociAws)
    .WithReference(flociAzure)
    .WithReference(flociGcp)
    .WaitFor(flociAws)
    .WaitFor(flociAzure)
    .WaitFor(flociGcp);

builder.Build().Run();

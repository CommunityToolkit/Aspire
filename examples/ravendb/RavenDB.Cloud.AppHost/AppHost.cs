using CommunityToolkit.Aspire.Hosting.RavenDB.Cloud;
using Projects;

var builder = DistributedApplication.CreateBuilder(args);

// The applications run in Docker Compose; RavenDB Cloud gets them the product URL and a certificate of their own.
builder.AddDockerComposeEnvironment("compose");

var apiKey = builder.AddParameter("ravendb-cloud-api-key", secret: true);

// aspire run starts a local RavenDB container; aspire deploy finds or creates the RavenDB Cloud product.
var ravendb = builder.AddRavenDB("ravendb")
    .PublishAsRavenDBCloud(
        apiKey,
        productName: "aspire-ravendb-example",
        RavenDBCloudSpec.Development(
            RavenDBCloudProvider.Aws,
            region: "eu-central-1",
            instanceType: "Dev10",
            diskSizeInGb: 10,
            RavenDBCloudStorageType.SsdStandard,
            RavenDBCloudDiskLayout.SingleDataDisk,
            releaseChannel: "Stable72"),
        cloud => cloud.WithAllowedIps("203.0.113.0/24"));

ravendb.AddDatabase("ravenDatabase", ensureCreated: true);

#pragma warning disable ASPIREDOTNETPROJECT001
builder.AddDotnetProject("apiservice", new CommunityToolkit_Aspire_Hosting_RavenDB_ApiService().ProjectPath)
#pragma warning restore ASPIREDOTNETPROJECT001
    .WithReference(ravendb)
    .WaitFor(ravendb);

builder.Build().Run();

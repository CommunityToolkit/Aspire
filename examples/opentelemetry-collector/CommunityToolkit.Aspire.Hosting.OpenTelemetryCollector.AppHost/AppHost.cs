var builder = DistributedApplication.CreateBuilder(args);

#pragma warning disable ASPIREDOTNETPROJECT001
builder.AddDotnetProject("api", new Projects.CommunityToolkit_Aspire_Hosting_OpenTelemetryCollector_Api().ProjectPath);
#pragma warning restore ASPIREDOTNETPROJECT001

var collector = builder.AddOpenTelemetryCollector("opentelemetry-collector")
    .WithAppForwarding()
    .WithConfig("./config.yaml");

builder.Build().Run();


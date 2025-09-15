var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_OpenTelemetryCollector_Api>("api");

builder.AddOpenTelemetryCollector("opentelemetry-collector")
    .WithAppForwarding()
    .WithConfig("./config.yaml");

builder.Build().Run();

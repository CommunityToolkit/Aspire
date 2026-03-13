var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Grafana_OtelLgtm_Api>("api");

var lgtm = builder.AddGrafanaOtelLgtm("grafana-lgtm")
    .WithAppForwarding()
    .WithCollectorConfig("./otelcol-config.yaml");

builder.Build().Run();

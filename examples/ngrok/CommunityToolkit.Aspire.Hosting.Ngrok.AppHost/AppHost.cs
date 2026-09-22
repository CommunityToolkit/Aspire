var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Ngrok_ApiService>("api");

var authToken = builder
    .AddParameter("ngrok-auth-token", "your-ngrok-auth-token", secret: true);

builder.AddNgrok("ngrok")
    .WithAuthToken(authToken)
    .WithTunnelEndpoint(apiService, "http");

await builder.Build().RunAsync();
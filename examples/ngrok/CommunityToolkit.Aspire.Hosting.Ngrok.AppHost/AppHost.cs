var builder = DistributedApplication.CreateBuilder(args);

#pragma warning disable ASPIREDOTNETPROJECT001
var apiService = builder.AddDotnetProject("api", new Projects.CommunityToolkit_Aspire_Hosting_Ngrok_ApiService().ProjectPath);
#pragma warning restore ASPIREDOTNETPROJECT001

var authToken = builder
    .AddParameter("ngrok-auth-token", "your-ngrok-auth-token", secret: true);

builder.AddNgrok("ngrok")
    .WithAuthToken(authToken)
    .WithTunnelEndpoint(apiService, "http");

await builder.Build().RunAsync();